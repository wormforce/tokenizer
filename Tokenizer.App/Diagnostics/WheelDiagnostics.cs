using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Tokenizer.App.Diagnostics;

internal static class WheelDiagnostics
{
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Tokenizer",
        "logs");
    private static readonly string LogPathValue = Path.Combine(LogDirectory, "wheel-diagnostics.log");
    private static readonly string SessionId = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

    static WheelDiagnostics()
    {
        Directory.CreateDirectory(LogDirectory);

        if (File.Exists(LogPathValue))
        {
            var fileInfo = new FileInfo(LogPathValue);
            if (fileInfo.Length > 512 * 1024)
            {
                File.WriteAllText(LogPathValue, string.Empty);
            }
        }

        Log("diag", $"session-start id={SessionId}");
    }

    public static string LogPath => LogPathValue;

    public static void StartSession(string sessionName)
    {
        Log("diag", $"session={sessionName} id={SessionId} log={LogPathValue}");
    }

    public static void AttachPage(string pageName, UIElement eventSource, ScrollViewer scrollViewer)
    {
        eventSource.PointerEntered += (_, e) =>
            Log(
                pageName,
                $"pointer-entered src={DescribeObject(e.OriginalSource)} focus={DescribeFocus(eventSource.XamlRoot)}");

        eventSource.PointerExited += (_, e) =>
            Log(
                pageName,
                $"pointer-exited src={DescribeObject(e.OriginalSource)} focus={DescribeFocus(eventSource.XamlRoot)}");

        eventSource.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler((_, e) =>
            {
                var point = e.GetCurrentPoint(eventSource);
                Log(
                    pageName,
                    $"pointer-pressed src={DescribeObject(e.OriginalSource)} pos={FormatPoint(point.Position.X, point.Position.Y)} handled={e.Handled} focus={DescribeFocus(eventSource.XamlRoot)}");
            }),
            true);

        eventSource.AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler((_, e) =>
            {
                var point = e.GetCurrentPoint(eventSource);
                Log(
                    pageName,
                    $"pointer-released src={DescribeObject(e.OriginalSource)} pos={FormatPoint(point.Position.X, point.Position.Y)} handled={e.Handled} focus={DescribeFocus(eventSource.XamlRoot)}");
            }),
            true);

        scrollViewer.GotFocus += (_, e) =>
            Log(pageName, $"scrollviewer-got-focus src={DescribeObject(e.OriginalSource)} focus={DescribeFocus(scrollViewer.XamlRoot)}");

        scrollViewer.LostFocus += (_, e) =>
            Log(pageName, $"scrollviewer-lost-focus src={DescribeObject(e.OriginalSource)} focus={DescribeFocus(scrollViewer.XamlRoot)}");

        scrollViewer.ViewChanged += (_, e) =>
            Log(
                pageName,
                $"view-changed offset={scrollViewer.VerticalOffset:F1} scrollable={scrollViewer.ScrollableHeight:F1} intermediate={e.IsIntermediate}");
    }

    public static void Log(string area, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{area}] {message}{Environment.NewLine}";

        lock (SyncRoot)
        {
            File.AppendAllText(LogPathValue, line);
        }
    }

    public static string DescribeObject(object? value)
        => value?.GetType().FullName ?? "null";

    public static string DescribeFocus(XamlRoot? xamlRoot)
    {
        if (xamlRoot is null)
        {
            return "no-xaml-root";
        }

        return DescribeObject(FocusManager.GetFocusedElement(xamlRoot));
    }

    private static string FormatPoint(double x, double y)
        => $"({x:F1},{y:F1})";
}

