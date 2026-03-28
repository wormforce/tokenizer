using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tokenizer.App.ViewModels;

namespace Tokenizer.App.Views;

public sealed partial class TodayPage : Page
{
    private const double DetailsWideThreshold = 980;
    private const double MetricWideThreshold = 1240;
    private const double MetricMediumThreshold = 820;
    private const double LayoutHysteresis = 48;

    private bool? _isDetailsWide;
    private int? _metricColumns;

    public TodayPage(TodayViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
        Loaded += OnLoaded;
        SizeChanged += OnViewportSizeChanged;
    }

    public TodayViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ApplyViewportLayout(ActualWidth);
        await ViewModel.RefreshAsync();
        ScheduleViewportRefresh();
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
        => ApplyViewportLayout(e.NewSize.Width);

    private void ApplyViewportLayout(double width)
    {
        if (width <= 0)
        {
            return;
        }

        LayoutRoot.MinWidth = width;
        ApplyResponsiveLayout(width);
    }

    private void ApplyResponsiveLayout(double width)
    {
        _metricColumns = ResolveMetricColumns(_metricColumns, width);
        _isDetailsWide = ResolveWideState(_isDetailsWide, width, DetailsWideThreshold);

        ApplyMetricLayout(_metricColumns.Value);
        ApplyDetailsLayout(_isDetailsWide.Value);
    }

    private void ApplyMetricLayout(int columns)
    {
        var star = new GridLength(1, GridUnitType.Star);
        var zero = new GridLength(0);

        MetricColumn1.Width = star;
        MetricColumn2.Width = columns >= 2 ? star : zero;
        MetricColumn3.Width = columns == 4 ? star : zero;
        MetricColumn4.Width = columns == 4 ? star : zero;

        MetricRow1.Height = GridLength.Auto;
        MetricRow2.Height = columns == 4 ? zero : GridLength.Auto;
        MetricRow3.Height = columns == 1 ? GridLength.Auto : zero;
        MetricRow4.Height = columns == 1 ? GridLength.Auto : zero;

        if (columns == 4)
        {
            SetMetricCardPosition(TodayCharsCard, 0, 0);
            SetMetricCardPosition(ThisMinuteCard, 0, 1);
            SetMetricCardPosition(PeakMinuteCard, 0, 2);
            SetMetricCardPosition(TopAppCard, 0, 3);
            return;
        }

        if (columns == 2)
        {
            SetMetricCardPosition(TodayCharsCard, 0, 0);
            SetMetricCardPosition(ThisMinuteCard, 0, 1);
            SetMetricCardPosition(PeakMinuteCard, 1, 0);
            SetMetricCardPosition(TopAppCard, 1, 1);
            return;
        }

        SetMetricCardPosition(TodayCharsCard, 0, 0);
        SetMetricCardPosition(ThisMinuteCard, 1, 0);
        SetMetricCardPosition(PeakMinuteCard, 2, 0);
        SetMetricCardPosition(TopAppCard, 3, 0);
    }

    private void ApplyDetailsLayout(bool isWide)
    {
        var zero = new GridLength(0);

        DetailsPrimaryColumn.Width = isWide
            ? new GridLength(2, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Star);
        DetailsSecondaryColumn.Width = isWide
            ? new GridLength(1, GridUnitType.Star)
            : zero;
        DetailsRow1.Height = GridLength.Auto;
        DetailsRow2.Height = isWide ? zero : GridLength.Auto;

        Grid.SetRow(MinuteChartCard, 0);
        Grid.SetColumn(MinuteChartCard, 0);
        Grid.SetRow(AppRankingCard, isWide ? 0 : 1);
        Grid.SetColumn(AppRankingCard, isWide ? 1 : 0);
    }

    private static void SetMetricCardPosition(FrameworkElement card, int row, int column)
    {
        Grid.SetRow(card, row);
        Grid.SetColumn(card, column);
    }

    private void ScheduleViewportRefresh()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ApplyViewportLayout(ActualWidth);
            LayoutRoot.UpdateLayout();
            UpdateLayout();
        });
    }

    private static bool ResolveWideState(bool? current, double width, double threshold)
    {
        if (current is null)
        {
            return width >= threshold;
        }

        return current.Value
            ? width >= threshold - LayoutHysteresis
            : width >= threshold + LayoutHysteresis;
    }

    private static int ResolveMetricColumns(int? current, double width)
    {
        if (current is null)
        {
            return width >= MetricWideThreshold
                ? 4
                : width >= MetricMediumThreshold
                    ? 2
                    : 1;
        }

        return current.Value switch
        {
            4 when width < MetricWideThreshold - LayoutHysteresis => width >= MetricMediumThreshold ? 2 : 1,
            4 => 4,
            2 when width >= MetricWideThreshold + LayoutHysteresis => 4,
            2 when width < MetricMediumThreshold - LayoutHysteresis => 1,
            2 => 2,
            _ when width >= MetricWideThreshold + LayoutHysteresis => 4,
            _ when width >= MetricMediumThreshold + LayoutHysteresis => 2,
            _ => 1
        };
    }
}

