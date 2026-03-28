using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Tokenizer.App.ViewModels;

namespace Tokenizer.App.Views;

public sealed partial class SettingsPage : Page
{
    private const double HeaderWideThreshold = 900;
    private const double ContentWideThreshold = 1080;
    private const double LayoutHysteresis = 48;

    private bool? _isHeaderWide;
    private bool? _isContentWide;

    public SettingsPage(SettingsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = ViewModel;
        Loaded += OnLoaded;
        SizeChanged += OnViewportSizeChanged;
    }

    public SettingsViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        ApplyViewportLayout(ActualWidth);
        await ViewModel.LoadAsync();
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
        _isHeaderWide = ResolveWideState(_isHeaderWide, width, HeaderWideThreshold);
        _isContentWide = ResolveWideState(_isContentWide, width, ContentWideThreshold);

        ApplyHeaderLayout(_isHeaderWide.Value);
        ApplyContentLayout(_isContentWide.Value);
    }

    private void ApplyHeaderLayout(bool isWide)
    {
        var zero = new GridLength(0);

        SettingsHeaderSecondaryColumn.Width = isWide ? GridLength.Auto : zero;
        SettingsHeaderSecondaryRow.Height = isWide ? zero : GridLength.Auto;

        Grid.SetRow(SettingsStatusCard, isWide ? 0 : 1);
        Grid.SetColumn(SettingsStatusCard, isWide ? 1 : 0);
        Grid.SetColumnSpan(SettingsStatusCard, isWide ? 1 : 2);
    }

    private void ApplyContentLayout(bool isWide)
    {
        var zero = new GridLength(0);

        SettingsPrimaryColumn.Width = new GridLength(1, GridUnitType.Star);
        SettingsSecondaryColumn.Width = isWide
            ? new GridLength(1, GridUnitType.Star)
            : zero;
        SettingsContentRow1.Height = GridLength.Auto;
        SettingsContentRow2.Height = isWide ? zero : GridLength.Auto;

        Grid.SetRow(SettingsBehaviorCard, 0);
        Grid.SetColumn(SettingsBehaviorCard, 0);
        Grid.SetRow(SettingsFloatingCard, isWide ? 0 : 1);
        Grid.SetColumn(SettingsFloatingCard, isWide ? 1 : 0);
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

