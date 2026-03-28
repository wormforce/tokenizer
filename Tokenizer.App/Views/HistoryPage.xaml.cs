using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tokenizer.App.ViewModels;

namespace Tokenizer.App.Views;

public sealed partial class HistoryPage : Page
{
    private const double HeaderWideThreshold = 760;
    private const double DetailsWideThreshold = 980;
    private const double LayoutHysteresis = 48;

    private bool? _isHeaderWide;
    private bool? _isDetailsWide;

    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
        Loaded += OnLoaded;
        SizeChanged += OnViewportSizeChanged;
    }

    public HistoryViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ApplyViewportLayout(ActualWidth);
        await ViewModel.LoadAsync();
        ScheduleViewportRefresh();
    }

    private async void DatePicker_DateChanged(object sender, DatePickerValueChangedEventArgs args)
    {
        if (args.NewDate is { } newDate)
        {
            await ViewModel.LoadAsync(newDate);
        }
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
        _isHeaderWide = ResolveWideState(_isHeaderWide, width, HeaderWideThreshold);
        _isDetailsWide = ResolveWideState(_isDetailsWide, width, DetailsWideThreshold);

        ApplyHeaderLayout(_isHeaderWide.Value);
        ApplyDetailsLayout(_isDetailsWide.Value);
    }

    private void ApplyHeaderLayout(bool isWide)
    {
        var zero = new GridLength(0);

        HistoryHeaderSecondaryColumn.Width = isWide ? GridLength.Auto : zero;
        HistoryHeaderSecondaryRow.Height = isWide ? zero : GridLength.Auto;

        Grid.SetRow(HistoryDatePicker, isWide ? 0 : 1);
        Grid.SetColumn(HistoryDatePicker, isWide ? 1 : 0);
        Grid.SetColumnSpan(HistoryDatePicker, isWide ? 1 : 2);
    }

    private void ApplyDetailsLayout(bool isWide)
    {
        var zero = new GridLength(0);

        HistoryDetailsPrimaryColumn.Width = isWide
            ? new GridLength(2, GridUnitType.Star)
            : new GridLength(1, GridUnitType.Star);
        HistoryDetailsSecondaryColumn.Width = isWide
            ? new GridLength(1, GridUnitType.Star)
            : zero;
        HistoryDetailsRow1.Height = GridLength.Auto;
        HistoryDetailsRow2.Height = isWide ? zero : GridLength.Auto;

        Grid.SetRow(HistoryDayChartCard, 0);
        Grid.SetColumn(HistoryDayChartCard, 0);
        Grid.SetRow(HistoryRankingCard, isWide ? 0 : 1);
        Grid.SetColumn(HistoryRankingCard, isWide ? 1 : 0);
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
}

