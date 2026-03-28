using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Tokenizer.App.Diagnostics;

namespace Tokenizer.App.Views;

internal static class PageScrollWheelRouter
{
    private const double DefaultScrollStep = 48;
    private const int WheelDeltaPerStep = 120;

    public static void Attach(string pageName, UIElement eventSource, ScrollViewer scrollViewer)
    {
        WheelDiagnostics.AttachPage(pageName, eventSource, scrollViewer);
        eventSource.AddHandler(
            UIElement.PointerWheelChangedEvent,
            new PointerEventHandler((_, e) =>
            {
                var delta = e.GetCurrentPoint(eventSource).Properties.MouseWheelDelta;
                var handled = TryScroll(scrollViewer, delta, out var outcome);
                WheelDiagnostics.Log(
                    pageName,
                    $"pointer-wheel src={WheelDiagnostics.DescribeObject(e.OriginalSource)} delta={delta} handledBefore={e.Handled} offset={scrollViewer.VerticalOffset:F1}/{scrollViewer.ScrollableHeight:F1} handledAfter={handled} outcome={outcome} focus={WheelDiagnostics.DescribeFocus(eventSource.XamlRoot)}");
                if (handled)
                {
                    e.Handled = true;
                }
            }),
            true);
    }

    public static bool TryScroll(ScrollViewer scrollViewer, int delta, out string outcome)
    {
        if (scrollViewer.ScrollableHeight <= 0)
        {
            outcome = "no-scrollable-height";
            return false;
        }

        if (delta == 0)
        {
            outcome = "delta-zero";
            return false;
        }

        var offsetDelta = delta * (DefaultScrollStep / WheelDeltaPerStep);
        var nextOffset = Math.Clamp(
            scrollViewer.VerticalOffset - offsetDelta,
            0,
            scrollViewer.ScrollableHeight);

        if (Math.Abs(nextOffset - scrollViewer.VerticalOffset) < 0.5)
        {
            outcome = $"offset-unchanged next={nextOffset:F1}";
            return false;
        }

        scrollViewer.ChangeView(null, nextOffset, null, true);
        outcome = $"changed next={nextOffset:F1}";
        return true;
    }
}

