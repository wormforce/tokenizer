using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Tokenizer.App.Services;
using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Models;

namespace Tokenizer.App.ViewModels;

public sealed class HistoryViewModel : ViewModelBase
{
    private readonly IStatsQueryService _statsQueryService;
    private DateTimeOffset _selectedDate = DateTimeOffset.Now.Date;
    private IReadOnlyList<MinuteChartPoint> _rawDayChart = Array.Empty<MinuteChartPoint>();
    private ISeries[] _trendSeries;
    private ISeries[] _daySeries;
    private Axis[] _trendXAxes;
    private Axis[] _dayXAxes;
    private Axis[] _yAxes;
    private TimeBucketOption _selectedBucketOption = TimeBucketOption.OneMinute;
    private TimeRangeOption _selectedRangeOption = TimeRangeOption.DataWindow;

    public HistoryViewModel(IStatsQueryService statsQueryService, IAppDispatcher dispatcher) : base(dispatcher)
    {
        _statsQueryService = statsQueryService;
        TrendRanking = [];
        _trendSeries = [CreateSeries(Array.Empty<double>(), "Chars")];
        _daySeries = [CreateSeries(Array.Empty<double>(), "Chars / min")];
        _trendXAxes = [CreateXAxis(Array.Empty<string>())];
        _dayXAxes = [CreateXAxis(Array.Empty<string>())];
        _yAxes = [CreateYAxis("chars")];
        ReloadCommand = new AsyncRelayCommand(() => LoadAsync(SelectedDate));
    }

    public IReadOnlyList<TimeBucketOption> BucketOptions { get; } = TimeBucketOption.All;

    public IReadOnlyList<TimeRangeOption> RangeOptions { get; } = TimeRangeOption.All;

    public ObservableCollection<AppRankingItem> TrendRanking { get; }

    public IAsyncRelayCommand ReloadCommand { get; }

    public DateTimeOffset SelectedDate
    {
        get => _selectedDate;
        set => SetProperty(ref _selectedDate, value);
    }

    public ISeries[] TrendSeries
    {
        get => _trendSeries;
        private set => SetProperty(ref _trendSeries, value);
    }

    public ISeries[] DaySeries
    {
        get => _daySeries;
        private set => SetProperty(ref _daySeries, value);
    }

    public Axis[] TrendXAxes
    {
        get => _trendXAxes;
        private set => SetProperty(ref _trendXAxes, value);
    }

    public Axis[] DayXAxes
    {
        get => _dayXAxes;
        private set => SetProperty(ref _dayXAxes, value);
    }

    public Axis[] YAxes
    {
        get => _yAxes;
        private set => SetProperty(ref _yAxes, value);
    }

    public TimeBucketOption SelectedBucketOption
    {
        get => _selectedBucketOption;
        set
        {
            if (SetProperty(ref _selectedBucketOption, value))
            {
                UpdateDayChart(_rawDayChart);
            }
        }
    }

    public TimeRangeOption SelectedRangeOption
    {
        get => _selectedRangeOption;
        set
        {
            if (SetProperty(ref _selectedRangeOption, value))
            {
                UpdateDayChart(_rawDayChart);
            }
        }
    }

    public async Task LoadAsync(DateTimeOffset? selectedDate = null)
    {
        if (selectedDate.HasValue)
        {
            SelectedDate = selectedDate.Value;
        }

        var localDate = SelectedDate.ToString("yyyy-MM-dd");
        var trend = await _statsQueryService.GetDailyTrendAsync(14);
        var dayChart = await _statsQueryService.GetMinuteChartAsync(localDate);
        var ranking = await _statsQueryService.GetAppRankingAsync(localDate);

        await Dispatcher.EnqueueAsync(() =>
        {
            TrendSeries = [CreateSeries(trend.Select(static point => (double)point.TotalChars), "Chars")];
            TrendXAxes = [CreateXAxis(trend.Select(static point => point.StatDate[5..]))];
            _rawDayChart = dayChart;
            UpdateDayChart(dayChart);

            TrendRanking.Clear();
            foreach (var item in ranking)
            {
                TrendRanking.Add(item);
            }
        });
    }

    private void UpdateDayChart(IReadOnlyList<MinuteChartPoint> dayChart)
    {
        var bucketed = TimeBucketChartBuilder.BuildForDate(
            dayChart,
            SelectedDate.Date,
            SelectedBucketOption,
            SelectedRangeOption,
            DateTime.Now);

        DaySeries = [CreateSeries(bucketed.Values, bucketed.SeriesName)];
        DayXAxes = [CreateXAxis(bucketed.Labels)];
        YAxes = [CreateYAxis(bucketed.AxisName)];
    }

    private static LineSeries<double> CreateSeries(IEnumerable<double> values, string tooltipLabel)
    {
        return new LineSeries<double>
        {
            Name = string.Empty,
            Values = values.ToArray(),
            GeometrySize = 0,
            Fill = null,
            Stroke = new SolidColorPaint(new SKColor(22, 138, 173), 3),
            GeometryFill = new SolidColorPaint(new SKColor(22, 138, 173)),
            YToolTipLabelFormatter = chartPoint => $"{chartPoint.Coordinate.PrimaryValue:0} {tooltipLabel.ToLowerInvariant()}"
        };
    }

    private static Axis CreateXAxis(IEnumerable<string> labels)
    {
        return new Axis
        {
            Labels = labels.ToArray(),
            LabelsRotation = 10,
            LabelsPaint = new SolidColorPaint(new SKColor(235, 239, 244)),
            SeparatorsPaint = new SolidColorPaint(new SKColor(70, 78, 88))
        };
    }

    private static Axis CreateYAxis(string axisName)
    {
        return new Axis
        {
            MinLimit = 0,
            Name = axisName,
            LabelsPaint = new SolidColorPaint(new SKColor(235, 239, 244)),
            NamePaint = new SolidColorPaint(new SKColor(235, 239, 244)),
            SeparatorsPaint = new SolidColorPaint(new SKColor(70, 78, 88))
        };
    }
}

