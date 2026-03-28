using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Tokenizer.Core.Models;
using Tokenizer.Infrastructure.Windows;

namespace Tokenizer.App.Views;

public sealed class FloatingBallWindow
{
    private const int CommandOpen = 2001;
    private const int CommandTogglePause = 2002;
    private const int CommandExit = 2003;
    private const uint MessageRedraw = NativeMethods.WmApp + 100;
    private const uint MessageApplySettings = NativeMethods.WmApp + 101;
    private const uint MessageShow = NativeMethods.WmApp + 102;
    private const uint MessageHide = NativeMethods.WmApp + 103;
    private const uint MessageClose = NativeMethods.WmApp + 104;
    private const nuint TopMostRefreshTimerId = 1;
    private const uint TopMostRefreshIntervalMilliseconds = 1000;
    private const int MinimumCircularSize = 60;
    private const int MaximumCircularSize = 120;
    private const int MinimumOpacityPercent = 20;
    private const int MaximumOpacityPercent = 100;
    private const int SnapThreshold = 32;
    private const int EdgeClipPixels = 3;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TaskCompletionSource _readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly NativeMethods.WindowProc _windowProc;
    private readonly object _stateLock = new();

    private Thread? _thread;
    private uint _threadId;
    private nint _windowHandle;
    private string _displayText = "0 c/s";
    private bool _isPaused;
    private AppSettingsModel _settings = new();
    private NativeMethods.Point _dragCursorOrigin;
    private NativeMethods.Rect _windowOrigin;
    private bool _dragging;
    private bool _movedDuringDrag;
    private bool _hasAppliedExternalSettings;
    private bool _preferCursorMonitorForNextPlacement;
    private bool _isVisible;
    private nint _arrowCursorHandle;

    public FloatingBallWindow()
    {
        _windowProc = WndProc;
    }

    public event EventHandler? OpenRequested;

    public event EventHandler? TogglePauseRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler<FloatingBallPositionChangedEventArgs>? PositionCommitted;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_thread is { IsAlive: true })
            {
                return;
            }

            _thread = new Thread(RunMessageLoop)
            {
                IsBackground = true,
                Name = "Tokenizer.FloatingBall"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();

            using var registration = cancellationToken.Register(() => _readyTcs.TrySetCanceled(cancellationToken));
            await _readyTcs.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ShowWindow()
    {
        if (_windowHandle != 0)
        {
            NativeMethods.PostMessage(_windowHandle, MessageShow, 0, 0);
        }
    }

    public void HideWindow()
    {
        if (_windowHandle != 0)
        {
            NativeMethods.PostMessage(_windowHandle, MessageHide, 0, 0);
        }
    }

    public void CloseWindow()
    {
        if (_windowHandle != 0)
        {
            NativeMethods.PostMessage(_windowHandle, MessageClose, 0, 0);
        }
    }

    public void ApplySettings(AppSettingsModel settings)
    {
        lock (_stateLock)
        {
            _settings = settings;
            if (!_hasAppliedExternalSettings)
            {
                _preferCursorMonitorForNextPlacement = true;
                _hasAppliedExternalSettings = true;
            }
        }

        if (_windowHandle != 0)
        {
            NativeMethods.PostMessage(_windowHandle, MessageApplySettings, 0, 0);
        }
    }

    public void ApplySnapshot(RealtimeStatsSnapshot snapshot)
    {
        lock (_stateLock)
        {
            _isPaused = snapshot.IsPaused;
            _displayText = snapshot.IsPaused ? "PAUSE" : $"{snapshot.CurrentCps} c/s";
        }

        if (_windowHandle != 0)
        {
            NativeMethods.PostMessage(_windowHandle, MessageRedraw, 0, 0);
        }
    }

    private void RunMessageLoop()
    {
        _threadId = NativeMethods.GetCurrentThreadId();
        var className = $"TokenizerFloatingBall_{Guid.NewGuid():N}";
        var wndClass = new NativeMethods.WndClassEx
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.WndClassEx>(),
            LpfnWndProc = Marshal.GetFunctionPointerForDelegate(_windowProc),
            HInstance = NativeMethods.GetModuleHandle(null),
            HCursor = NativeMethods.LoadCursor(0, NativeMethods.IdcArrow),
            LpszClassName = className
        };

        NativeMethods.RegisterClassEx(ref wndClass);
        _arrowCursorHandle = wndClass.HCursor;

        var ballSize = GetBallSize();
        _windowHandle = NativeMethods.CreateWindowEx(
            NativeMethods.WsExToolWindow | NativeMethods.WsExLayered | NativeMethods.WsExNoActivate,
            className,
            string.Empty,
            NativeMethods.WsPopup,
            0,
            0,
            ballSize,
            ballSize,
            0,
            0,
            wndClass.HInstance,
            0);

        if (_windowHandle == 0)
        {
            _readyTcs.TrySetException(new InvalidOperationException("Failed to create floating ball window."));
            return;
        }

        ApplySettingsCore();
        _readyTcs.TrySetResult();

        while (NativeMethods.GetMessage(out var message, 0, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref message);
            NativeMethods.DispatchMessage(ref message);
        }

        if (_windowHandle != 0)
        {
            NativeMethods.DestroyWindow(_windowHandle);
            _windowHandle = 0;
        }

        _threadId = 0;
    }

    private nint WndProc(nint hWnd, uint msg, nuint wParam, nint lParam)
    {
        switch (msg)
        {
            case MessageRedraw:
                NativeMethods.InvalidateRect(hWnd, 0, false);
                return 0;
            case MessageApplySettings:
                ApplySettingsCore();
                EnsureTopMostCore();
                return 0;
            case MessageShow:
                ApplySettingsCore();
                _isVisible = true;
                StartTopMostRefreshTimer();
                NativeMethods.ShowWindow(hWnd, NativeMethods.SwShowNoActivate);
                EnsureTopMostCore(showWindow: true);
                return 0;
            case MessageHide:
                _isVisible = false;
                StopTopMostRefreshTimer();
                NativeMethods.ShowWindow(hWnd, NativeMethods.SwHide);
                return 0;
            case MessageClose:
                _isVisible = false;
                StopTopMostRefreshTimer();
                NativeMethods.DestroyWindow(hWnd);
                return 0;
            case NativeMethods.WmDisplayChange:
            case NativeMethods.WmSettingChange:
                if (_isVisible)
                {
                    ApplySettingsCore();
                    EnsureTopMostCore();
                }

                return 0;
            case NativeMethods.WmTimer:
                if (wParam == TopMostRefreshTimerId)
                {
                    EnsureTopMostCore();
                    return 0;
                }

                break;
            case NativeMethods.WmPaint:
                PaintWindow(hWnd);
                return 0;
            case NativeMethods.WmSetCursor:
                if (_arrowCursorHandle != 0)
                {
                    NativeMethods.SetCursor(_arrowCursorHandle);
                    return 1;
                }

                break;
            case NativeMethods.WmEraseBkgnd:
                return 1;
            case NativeMethods.WmLButtonDown:
                OnLeftButtonDown(hWnd);
                return 0;
            case NativeMethods.WmMouseMove:
                OnMouseMove(hWnd);
                return 0;
            case NativeMethods.WmLButtonUp:
                OnLeftButtonUp(hWnd);
                return 0;
            case NativeMethods.WmRButtonUp:
                ShowContextMenu(hWnd);
                return 0;
            case NativeMethods.WmCommand:
                switch ((int)(wParam & 0xFFFF))
                {
                    case CommandOpen:
                        OpenRequested?.Invoke(this, EventArgs.Empty);
                        return 0;
                    case CommandTogglePause:
                        TogglePauseRequested?.Invoke(this, EventArgs.Empty);
                        return 0;
                    case CommandExit:
                        ExitRequested?.Invoke(this, EventArgs.Empty);
                        return 0;
                }

                break;
            case NativeMethods.WmDestroy:
                _isVisible = false;
                StopTopMostRefreshTimer();
                NativeMethods.PostQuitMessage(0);
                return 0;
        }

        return NativeMethods.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ApplySettingsCore()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        var settings = GetSettings();
        var ballSize = Clamp(settings.FloatingBallSize, MinimumCircularSize, MaximumCircularSize);
        var opacity = ConvertOpacity(settings.FloatingBallOpacityPercent);

        NativeMethods.SetLayeredWindowAttributes(_windowHandle, 0, opacity, NativeMethods.LwaAlpha);
        NativeMethods.SetWindowPos(
            _windowHandle,
            NativeMethods.HwndTopMost,
            0,
            0,
            ballSize,
            ballSize,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoActivate);

        var regionInset = Math.Clamp(EdgeClipPixels, 0, Math.Max(0, (ballSize / 2) - 1));
        var region = NativeMethods.CreateEllipticRgn(
            regionInset,
            regionInset,
            ballSize - regionInset,
            ballSize - regionInset);
        if (NativeMethods.SetWindowRgn(_windowHandle, region, true) == 0)
        {
            NativeMethods.DeleteObject(region);
        }

        ApplyPlacementCore(settings.FloatingEdge, settings.FloatingOffsetX, settings.FloatingOffsetY, ballSize);
        EnsureTopMostCore();
        NativeMethods.InvalidateRect(_windowHandle, 0, false);
    }

    private void PaintWindow(nint hWnd)
    {
        var displayText = GetDisplayText();

        var hdc = NativeMethods.BeginPaint(hWnd, out var paintStruct);
        try
        {
            NativeMethods.GetClientRect(hWnd, out var rect);
            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;

            using var graphics = Graphics.FromHdc(hdc);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            using var fill = new SolidBrush(Color.FromArgb(17, 26, 34));
            graphics.FillEllipse(fill, -1, -1, width + 2, height + 2);

            var titleFontSize = Math.Max(13f, width * 0.20f);
            var valueFontSize = Math.Max(14f, width * 0.21f);
            var spacing = Math.Max(2f, width * 0.03f);

            using var titleFont = new Font("Bahnschrift SemiBold", titleFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            using var valueFont = new Font("Bahnschrift SemiBold", valueFontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            using var titleBrush = new SolidBrush(Color.White);
            using var valueBrush = new SolidBrush(Color.FromArgb(255, 180, 84));

            var title = "Typing";
            var titleSize = graphics.MeasureString(title, titleFont);
            var valueSize = graphics.MeasureString(displayText, valueFont);
            var totalHeight = titleSize.Height + valueSize.Height + spacing;
            var originY = (height - totalHeight) / 2f;

            graphics.DrawString(title, titleFont, titleBrush, (width - titleSize.Width) / 2f, originY);
            graphics.DrawString(displayText, valueFont, valueBrush, (width - valueSize.Width) / 2f, originY + titleSize.Height + spacing);
        }
        finally
        {
            NativeMethods.EndPaint(hWnd, ref paintStruct);
        }
    }

    private void OnLeftButtonDown(nint hWnd)
    {
        _dragging = true;
        _movedDuringDrag = false;
        NativeMethods.GetCursorPos(out _dragCursorOrigin);
        NativeMethods.GetWindowRect(hWnd, out _windowOrigin);
        NativeMethods.SetCapture(hWnd);
    }

    private void OnMouseMove(nint hWnd)
    {
        if (!_dragging)
        {
            return;
        }

        NativeMethods.GetCursorPos(out var currentCursor);
        var deltaX = currentCursor.X - _dragCursorOrigin.X;
        var deltaY = currentCursor.Y - _dragCursorOrigin.Y;
        if (Math.Abs(deltaX) > 2 || Math.Abs(deltaY) > 2)
        {
            _movedDuringDrag = true;
        }

        NativeMethods.SetWindowPos(
            hWnd,
            NativeMethods.HwndTopMost,
            _windowOrigin.Left + deltaX,
            _windowOrigin.Top + deltaY,
            0,
            0,
            NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
    }

    private void OnLeftButtonUp(nint hWnd)
    {
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        NativeMethods.ReleaseCapture();

        if (!_movedDuringDrag)
        {
            OpenRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        NativeMethods.GetWindowRect(hWnd, out var windowRect);
        var settings = GetSettings();
        var ballSize = Clamp(settings.FloatingBallSize, MinimumCircularSize, MaximumCircularSize);
        var position = new Point(windowRect.Left, windowRect.Top);
        var monitorInfo = GetMonitorWorkAreaForWindow(position, ballSize);
        var workArea = monitorInfo.RcWork;

        var targetMode = ResolveSnapMode(position, workArea, ballSize);
        var offsetX = windowRect.Left - workArea.Left;
        var offsetY = windowRect.Top - workArea.Top;
        ApplyPlacementCore(targetMode, offsetX, offsetY, ballSize);

        NativeMethods.GetWindowRect(hWnd, out var finalRect);
        PositionCommitted?.Invoke(
            this,
            new FloatingBallPositionChangedEventArgs(
                targetMode,
                finalRect.Left - workArea.Left,
                finalRect.Top - workArea.Top));
    }

    private void ShowContextMenu(nint hWnd)
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            return;
        }

        var menu = NativeMethods.CreatePopupMenu();
        try
        {
            NativeMethods.AppendMenu(menu, NativeMethods.MfString, CommandOpen, "Open");
            NativeMethods.AppendMenu(menu, NativeMethods.MfString, CommandTogglePause, IsPaused() ? "Resume" : "Pause");
            NativeMethods.AppendMenu(menu, NativeMethods.MfSeparator, 0, null);
            NativeMethods.AppendMenu(menu, NativeMethods.MfString, CommandExit, "Exit");

            NativeMethods.SetForegroundWindow(hWnd);
            NativeMethods.TrackPopupMenu(
                menu,
                NativeMethods.TpmLeftAlign | NativeMethods.TpmBottomAlign | NativeMethods.TpmRightButton,
                point.X,
                point.Y,
                0,
                hWnd,
                0);
            NativeMethods.PostMessage(hWnd, NativeMethods.WmNull, 0, 0);
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private void ApplyPlacementCore(FloatingEdge mode, double offsetX, double offsetY, int ballSize)
    {
        if (_windowHandle == 0)
        {
            return;
        }

        NativeMethods.GetWindowRect(_windowHandle, out var currentRect);
        var preferCursorMonitor = GetPreferCursorMonitorForNextPlacement();
        var monitorInfo = preferCursorMonitor
            ? GetMonitorWorkAreaForCursor(ballSize)
            : GetMonitorWorkAreaForWindow(new Point(currentRect.Left, currentRect.Top), ballSize);
        var workArea = monitorInfo.RcWork;

        var x = workArea.Left;
        var y = workArea.Top;

        switch (mode)
        {
            case FloatingEdge.Left:
                x = workArea.Left;
                y = workArea.Top + Clamp(offsetY, 0, workArea.Bottom - workArea.Top - ballSize);
                break;
            case FloatingEdge.Right:
                x = workArea.Right - ballSize;
                y = workArea.Top + Clamp(offsetY, 0, workArea.Bottom - workArea.Top - ballSize);
                break;
            case FloatingEdge.Top:
                x = workArea.Left + Clamp(offsetX, 0, workArea.Right - workArea.Left - ballSize);
                y = workArea.Top;
                break;
            case FloatingEdge.Bottom:
                x = workArea.Left + Clamp(offsetX, 0, workArea.Right - workArea.Left - ballSize);
                y = workArea.Bottom - ballSize;
                break;
            case FloatingEdge.None:
            default:
                x = workArea.Left + Clamp(offsetX, 0, workArea.Right - workArea.Left - ballSize);
                y = workArea.Top + Clamp(offsetY, 0, workArea.Bottom - workArea.Top - ballSize);
                break;
        }

        NativeMethods.SetWindowPos(
            _windowHandle,
            NativeMethods.HwndTopMost,
            x,
            y,
            ballSize,
            ballSize,
            NativeMethods.SwpNoActivate);
        ClearPreferCursorMonitorForNextPlacement();
    }

    private void StartTopMostRefreshTimer()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        NativeMethods.SetTimer(_windowHandle, TopMostRefreshTimerId, TopMostRefreshIntervalMilliseconds, 0);
    }

    private void StopTopMostRefreshTimer()
    {
        if (_windowHandle == 0)
        {
            return;
        }

        NativeMethods.KillTimer(_windowHandle, TopMostRefreshTimerId);
    }

    private void EnsureTopMostCore(bool showWindow = false)
    {
        if (_windowHandle == 0 || !_isVisible && !showWindow)
        {
            return;
        }

        var flags = NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate;
        if (showWindow)
        {
            flags |= NativeMethods.SwpShowWindow;
        }

        NativeMethods.SetWindowPos(
            _windowHandle,
            NativeMethods.HwndTopMost,
            0,
            0,
            0,
            0,
            flags);
    }

    private AppSettingsModel GetSettings()
    {
        lock (_stateLock)
        {
            return _settings;
        }
    }

    private string GetDisplayText()
    {
        lock (_stateLock)
        {
            return _displayText;
        }
    }

    private bool GetPreferCursorMonitorForNextPlacement()
    {
        lock (_stateLock)
        {
            return _preferCursorMonitorForNextPlacement;
        }
    }

    private void ClearPreferCursorMonitorForNextPlacement()
    {
        lock (_stateLock)
        {
            _preferCursorMonitorForNextPlacement = false;
        }
    }

    private bool IsPaused()
    {
        lock (_stateLock)
        {
            return _isPaused;
        }
    }

    private static FloatingEdge ResolveSnapMode(Point position, NativeMethods.Rect workArea, int ballSize)
    {
        var distanceLeft = Math.Abs(position.X - workArea.Left);
        var distanceRight = Math.Abs(workArea.Right - position.X - ballSize);
        var distanceTop = Math.Abs(position.Y - workArea.Top);
        var distanceBottom = Math.Abs(workArea.Bottom - position.Y - ballSize);

        var distances = new Dictionary<FloatingEdge, int>
        {
            [FloatingEdge.Left] = distanceLeft,
            [FloatingEdge.Right] = distanceRight,
            [FloatingEdge.Top] = distanceTop,
            [FloatingEdge.Bottom] = distanceBottom
        };

        var best = distances.MinBy(static item => item.Value);
        return best.Value <= SnapThreshold ? best.Key : FloatingEdge.None;
    }

    private static NativeMethods.MonitorInfo GetMonitorWorkAreaForWindow(Point position, int ballSize)
    {
        var point = new NativeMethods.Point
        {
            X = position.X + (ballSize / 2),
            Y = position.Y + (ballSize / 2)
        };

        var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MonitorDefaultToNearest);
        var info = new NativeMethods.MonitorInfo
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>()
        };
        NativeMethods.GetMonitorInfo(monitor, ref info);
        return info;
    }

    private static NativeMethods.MonitorInfo GetMonitorWorkAreaForCursor(int ballSize)
    {
        if (NativeMethods.GetCursorPos(out var cursor))
        {
            var point = new NativeMethods.Point
            {
                X = cursor.X,
                Y = cursor.Y
            };

            var monitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MonitorDefaultToNearest);
            var info = new NativeMethods.MonitorInfo
            {
                CbSize = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>()
            };
            NativeMethods.GetMonitorInfo(monitor, ref info);
            return info;
        }

        return GetMonitorWorkAreaForWindow(new Point(0, 0), ballSize);
    }

    private static int Clamp(double value, int minimum, int maximum)
    {
        if (maximum < minimum)
        {
            return minimum;
        }

        return (int)Math.Clamp(Math.Round(value), minimum, maximum);
    }

    private static byte ConvertOpacity(int opacityPercent)
    {
        var clamped = Clamp(opacityPercent, MinimumOpacityPercent, MaximumOpacityPercent);
        return (byte)Math.Round(clamped * 255d / 100d);
    }

    private int GetBallSize()
    {
        var settings = GetSettings();
        return Clamp(settings.FloatingBallSize, MinimumCircularSize, MaximumCircularSize);
    }
}

