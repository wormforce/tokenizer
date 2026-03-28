using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Tokenizer.App.Diagnostics;
using Tokenizer.App.Services;
using Tokenizer.App.ViewModels;
using Tokenizer.App.Views;
using Tokenizer.Infrastructure.Windows;
using WinRT.Interop;
using Windows.Graphics;

namespace Tokenizer.App;

public sealed partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AppWindowService _windowService;
    private readonly NativeMethods.HookProc _mouseHookProc;
    private AppWindow? _appWindow;
    private int _layoutSyncVersion;
    private nint _mouseHookHandle;
    private nint _windowHandle;

    public MainWindow(IServiceProvider serviceProvider, AppWindowService windowService, ShellViewModel viewModel)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;
        _windowService = windowService;
        _mouseHookProc = MouseHookCallback;
        if (Content is FrameworkElement root)
        {
            root.DataContext = viewModel;
        }

        WheelDiagnostics.Log("window", $"ctor log={WheelDiagnostics.LogPath}");
        PageViewport.SizeChanged += OnPageViewportSizeChanged;
        PageHostContainer.SizeChanged += OnPageHostContainerSizeChanged;
        WindowRoot.AddHandler(
            UIElement.PointerWheelChangedEvent,
            new PointerEventHandler(OnWindowRootPointerWheelChanged),
            true);
        ShellContentArea.AddHandler(
            UIElement.PointerWheelChangedEvent,
            new PointerEventHandler(OnPageHostPointerWheelChanged),
            true);
        PageViewport.ViewChanged += (_, e) =>
            WheelDiagnostics.Log(
                "window",
                $"viewport-view-changed offset={PageViewport.VerticalOffset:F1} scrollable={PageViewport.ScrollableHeight:F1} intermediate={e.IsIntermediate}");
        ConfigureWindow();
        ShowPage("today");
    }

    public void ShowWindow()
    {
        _appWindow?.Show();
        Activate();
        NativeMethods.ShowWindow(_windowHandle, NativeMethods.SwShow);
        NativeMethods.SetForegroundWindow(_windowHandle);
    }

    public void HideWindow()
    {
        NativeMethods.ShowWindow(_windowHandle, NativeMethods.SwHide);
    }

    private void ConfigureWindow()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        _windowHandle = WindowNative.GetWindowHandle(this);

        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_windowHandle);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        ApplyWindowIcon();
        _appWindow.Resize(new SizeInt32(1340, 860));
        _appWindow.Closing += OnAppWindowClosing;
        InstallMouseHook();
    }

    private void ApplyWindowIcon()
    {
        if (_appWindow is null)
        {
            return;
        }

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (!File.Exists(iconPath))
        {
            return;
        }

        _appWindow.SetIcon(iconPath);
        _appWindow.SetTaskbarIcon(iconPath);
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_windowService.IsExitRequested)
        {
            args.Cancel = true;
            HideWindow();
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag })
        {
            WheelDiagnostics.Log("window", $"nav-selection tag={tag}");
            ShowPage(tag);
        }
    }

    private void ShowPage(string tag)
    {
        PageHost.Content = tag switch
        {
            "today" => _serviceProvider.GetRequiredService<Views.TodayPage>(),
            "history" => _serviceProvider.GetRequiredService<Views.HistoryPage>(),
            "settings" => _serviceProvider.GetRequiredService<Views.SettingsPage>(),
            _ => _serviceProvider.GetRequiredService<Views.TodayPage>()
        };

        WheelDiagnostics.Log(
            "window",
            $"show-page tag={tag} content={WheelDiagnostics.DescribeObject(PageHost.Content)}");

        UpdateNavigationSelection(tag);
        ScheduleLayoutWidthSync(resetScroll: true);
    }

    private void OnPageViewportSizeChanged(object sender, SizeChangedEventArgs e)
        => ScheduleLayoutWidthSync();

    private void OnPageHostContainerSizeChanged(object sender, SizeChangedEventArgs e)
        => ScheduleLayoutWidthSync();

    private void InstallMouseHook()
    {
        if (_mouseHookHandle != 0)
        {
            return;
        }

        var module = NativeMethods.GetModuleHandle(null);
        _mouseHookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WhMouseLl, _mouseHookProc, module, 0);
        WheelDiagnostics.Log("window", $"mouse-hook handle={_mouseHookHandle}");
    }

    private nint MouseHookCallback(int nCode, nuint wParam, nint lParam)
    {
        if (nCode < 0 || wParam != NativeMethods.WmMouseWheel || _windowHandle == 0)
        {
            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        if (NativeMethods.GetForegroundWindow() != _windowHandle)
        {
            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        var hook = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);
        var clientPoint = hook.Pt;
        if (!NativeMethods.ScreenToClient(_windowHandle, ref clientPoint))
        {
            return NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        var delta = unchecked((short)((hook.MouseData >> 16) & 0xFFFF));
        var handled = HandleMouseWheelHook(delta, clientPoint);
        return handled
            ? 1
            : NativeMethods.CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
    }

    private bool HandleMouseWheelHook(int delta, NativeMethods.Point clientPoint)
    {
        if (delta == 0 || PageViewport.ScrollableHeight <= 0)
        {
            return false;
        }

        var dipPoint = ConvertClientPointToDips(clientPoint);

        if (!IsPointEligibleForContentScroll(dipPoint.X, dipPoint.Y))
        {
            WheelDiagnostics.Log(
                "window",
                $"hook-wheel-skip delta={delta} client=({clientPoint.X},{clientPoint.Y}) dip=({dipPoint.X:F1},{dipPoint.Y:F1}) reason=outside-shell");
            return false;
        }

        if (IsPointOverInteractiveControl(dipPoint.X, dipPoint.Y))
        {
            WheelDiagnostics.Log(
                "window",
                $"hook-wheel-skip delta={delta} client=({clientPoint.X},{clientPoint.Y}) dip=({dipPoint.X:F1},{dipPoint.Y:F1}) reason=interactive");
            return false;
        }

        var handled = PageScrollWheelRouter.TryScroll(PageViewport, delta, out var outcome);
        WheelDiagnostics.Log(
            "window",
            $"hook-wheel delta={delta} client=({clientPoint.X},{clientPoint.Y}) dip=({dipPoint.X:F1},{dipPoint.Y:F1}) handled={handled} outcome={outcome} now={PageViewport.VerticalOffset:F1}/{PageViewport.ScrollableHeight:F1}");
        return handled;
    }

    private void OnPageHostPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(ShellContentArea);
        var delta = point.Properties.MouseWheelDelta;
        WheelDiagnostics.Log(
            "window",
            $"host-pointer-wheel src={WheelDiagnostics.DescribeObject(e.OriginalSource)} delta={delta} handledBefore={e.Handled} offsetBefore={PageViewport.VerticalOffset:F1}/{PageViewport.ScrollableHeight:F1} focus={WheelDiagnostics.DescribeFocus(PageHostContainer.XamlRoot)}");

        if (delta == 0 || PageViewport.ScrollableHeight <= 0)
        {
            return;
        }

        var originalSource = e.OriginalSource as DependencyObject;
        if (ShouldSkipHostWheel(originalSource))
        {
            WheelDiagnostics.Log(
                "window",
                $"host-pointer-wheel-skip src={WheelDiagnostics.DescribeObject(e.OriginalSource)} reason=interactive-control");
            return;
        }

        var handled = PageScrollWheelRouter.TryScroll(PageViewport, delta, out var outcome);
        WheelDiagnostics.Log(
            "window",
            $"host-pointer-wheel-custom delta={delta} handled={handled} outcome={outcome} now={PageViewport.VerticalOffset:F1}/{PageViewport.ScrollableHeight:F1}");
        if (handled)
        {
            e.Handled = true;
        }
    }

    private void OnWindowRootPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(WindowRoot);
        var delta = point.Properties.MouseWheelDelta;
        WheelDiagnostics.Log(
            "window",
            $"root-pointer-wheel src={WheelDiagnostics.DescribeObject(e.OriginalSource)} delta={delta} handledBefore={e.Handled} pos=({point.Position.X:F1},{point.Position.Y:F1}) offsetBefore={PageViewport.VerticalOffset:F1}/{PageViewport.ScrollableHeight:F1}");

        if (e.Handled || delta == 0 || PageViewport.ScrollableHeight <= 0)
        {
            return;
        }

        if (!IsPointEligibleForContentScroll(point.Position.X, point.Position.Y))
        {
            WheelDiagnostics.Log(
                "window",
                $"root-pointer-wheel-skip src={WheelDiagnostics.DescribeObject(e.OriginalSource)} reason=outside-content-area");
            return;
        }

        var originalSource = e.OriginalSource as DependencyObject;
        if (ShouldSkipHostWheel(originalSource))
        {
            WheelDiagnostics.Log(
                "window",
                $"root-pointer-wheel-skip src={WheelDiagnostics.DescribeObject(e.OriginalSource)} reason=interactive-control");
            return;
        }

        var handled = PageScrollWheelRouter.TryScroll(PageViewport, delta, out var outcome);
        WheelDiagnostics.Log(
            "window",
            $"root-pointer-wheel-backup delta={delta} handled={handled} outcome={outcome} now={PageViewport.VerticalOffset:F1}/{PageViewport.ScrollableHeight:F1}");
        if (handled)
        {
            e.Handled = true;
        }
    }

    private void UpdateNavigationSelection(string tag)
    {
        ApplyNavigationState(TodayNavButton, tag == "today");
        ApplyNavigationState(HistoryNavButton, tag == "history");
        ApplyNavigationState(SettingsNavButton, tag == "settings");
    }

    private void ApplyNavigationState(Button button, bool isActive)
    {
        button.Background = (Brush)Application.Current.Resources[isActive ? "AccentMutedBrush" : "SurfaceSoftBrush"];
        button.BorderBrush = (Brush)Application.Current.Resources[isActive ? "AccentBrush" : "OutlineBrush"];
        button.Foreground = (Brush)Application.Current.Resources["SurfaceContrastBrush"];
        button.Opacity = isActive ? 1 : 0.78;
    }

    private void ScheduleLayoutWidthSync(bool resetScroll = false)
    {
        var version = ++_layoutSyncVersion;

        DispatcherQueue.TryEnqueue(() => ApplyLayoutWidthSync(version, resetScroll));

        _ = Task.Run(async () =>
        {
            foreach (var delay in new[] { 40, 120, 260, 520 })
            {
                await Task.Delay(delay);
                DispatcherQueue.TryEnqueue(() => ApplyLayoutWidthSync(version, false));
            }
        });
    }

    private void ApplyLayoutWidthSync(int version, bool resetScroll)
    {
        if (version != _layoutSyncVersion)
        {
            return;
        }

        var synchronized = UpdateActivePageLayoutWidth();
        if (!synchronized)
        {
            return;
        }

        PageHost.UpdateLayout();
        PageViewport.UpdateLayout();

        if (resetScroll)
        {
            PageViewport.ChangeView(null, 0, null, true);
        }
    }

    private bool UpdateActivePageLayoutWidth()
    {
        var containerContentWidth = PageHostContainer.ActualWidth;
        var viewportWidth = PageViewport.ViewportWidth;
        var targetWidth = containerContentWidth;

        if (viewportWidth > 0)
        {
            targetWidth = targetWidth > 0
                ? Math.Min(targetWidth, viewportWidth)
                : viewportWidth;
        }

        if (targetWidth <= 0)
        {
            targetWidth = PageViewport.ActualWidth;
        }

        if (targetWidth <= 0)
        {
            return false;
        }

        PageHost.Width = targetWidth;
        PageHost.MinWidth = targetWidth;

        var layoutRoot = FindNamedDescendant<FrameworkElement>(PageHost.Content as DependencyObject, "LayoutRoot");
        if (layoutRoot is null)
        {
            return false;
        }

        layoutRoot.Width = targetWidth;
        layoutRoot.MinWidth = targetWidth;
        layoutRoot.UpdateLayout();
        WheelDiagnostics.Log(
            "window",
            $"layout-width target={targetWidth:F1} viewport={viewportWidth:F1} container={containerContentWidth:F1} host={PageHost.ActualWidth:F1} root={layoutRoot.ActualWidth:F1}");
        return true;
    }

    private bool IsPointEligibleForContentScroll(double x, double y)
    {
        if (ShellContentArea.XamlRoot is null)
        {
            return false;
        }

        var transform = ShellContentArea.TransformToVisual(WindowRoot);
        var origin = transform.TransformPoint(new Windows.Foundation.Point(0, 0));
        // Treat the whole right-side work area as scrollable, including blank space below content.
        // Only exclude the left nav rail and the title bar region.
        return x >= origin.X
            && y >= origin.Y;
    }

    private Windows.Foundation.Point ConvertClientPointToDips(NativeMethods.Point clientPoint)
    {
        var scale = WindowRoot.XamlRoot?.RasterizationScale ?? 1.0;
        if (scale <= 0)
        {
            scale = 1.0;
        }

        return new Windows.Foundation.Point(clientPoint.X / scale, clientPoint.Y / scale);
    }

    private bool IsPointOverInteractiveControl(double x, double y)
    {
        if (WindowRoot.XamlRoot is null)
        {
            return false;
        }

        var point = new Windows.Foundation.Point(x, y);
        var elements = VisualTreeHelper.FindElementsInHostCoordinates(point, WindowRoot, true);
        foreach (var element in elements)
        {
            if (element is Slider
                or DatePicker
                or ComboBox
                or CalendarView
                or Microsoft.UI.Xaml.Controls.Primitives.ScrollBar)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldSkipHostWheel(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Slider
                or DatePicker
                or ComboBox
                or CalendarView
                or Microsoft.UI.Xaml.Controls.Primitives.ScrollBar)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private static T? FindNamedDescendant<T>(DependencyObject? root, string name)
        where T : FrameworkElement
    {
        if (root is null)
        {
            return null;
        }

        if (root is T typedRoot && typedRoot.Name == name)
        {
            return typedRoot;
        }

        var childCount = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < childCount; index++)
        {
            var result = FindNamedDescendant<T>(VisualTreeHelper.GetChild(root, index), name);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }
}

