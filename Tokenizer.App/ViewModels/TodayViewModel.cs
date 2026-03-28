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

public sealed class TodayViewModel : ViewModelBase
{
    private readonly IStatsQueryService _statsQueryService;
    private readonly ITypingMonitorService _typingMonitorService;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private DateTimeOffset _lastRefreshUtc = DateTimeOffset.MinValue;
    private IReadOnlyList<MinuteChartPoint> _rawMinuteChart = Array.Empty<MinuteChartPoint>();

    private int _todayTotalChars;
    private int _currentMinuteChars;
    private int _peakMinuteChars;
    private string _topAppDisplayName = "No app yet";
    private ISeries[] _minuteSeries;
    private Axis[] _xAxes;
    private Axis[] _yAxes;
    private TimeBucketOption _selectedBucketOption = TimeBucketOption.OneMinute;
    private TimeRangeOption _selectedRangeOption = TimeRangeOption.DataWindow;

    public TodayViewModel(
        IStatsQueryService statsQueryService,
        ITypingMonitorService typingMonitorService,
        IAppDispatcher dispatcher) : base(dispatcher)
    {
        _statsQueryService = statsQueryService;
        _typingMonitorService = typingMonitorService;
        _typingMonitorService.SnapshotUpdated += OnSnapshotUpdated;

        _minuteSeries = [CreateSeries(Array.Empty<double>(), "Chars / min")];
        _xAxes = [CreateXAxis(Array.Empty<string>())];
        _yAxes = [CreateYAxis("chars")];
        AppRanking = [];
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
    }

    public IReadOnlyList<TimeBucketOption> BucketOptions { get; } = TimeBucketOption.All;

    public IReadOnlyList<TimeRangeOption> RangeOptions { get; } = TimeRangeOption.All;

    public ObservableCollection<AppRankingItem> AppRanking { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    public int TodayTotalChars
    {
        get => _todayTotalChars;
        private set => SetProperty(ref _todayTotalChars, value);
    }

    public int CurrentMinuteChars
    {
        get => _currentMinuteChars;
        private set => SetProperty(ref _currentMinuteChars, value);
    }

    public int PeakMinuteChars
    {
        get => _peakMinuteChars;
        private set => SetProperty(ref _peakMinuteChars, value);
    }

    public string TopAppDisplayName
    {
        get => _topAppDisplayName;
        private set => SetProperty(ref _topAppDisplayName, value);
    }

    public ISeries[] MinuteSeries
    {
        get => _minuteSeries;
        private set => SetProperty(ref _minuteSeries, value);
    }

    public Axis[] XAxes
    {
        get => _xAxes;
        private set => SetProperty(ref _xAxes, value);
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
                UpdateChart(_rawMinuteChart);
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
                UpdateChart(_rawMinuteChart);
            }
        }
    }

    public async Task RefreshAsync()
    {
        await _refreshGate.WaitAsync();
        try
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var summary = await _statsQueryService.GetTodaySummaryAsync();
            var chart = await _statsQueryService.GetMinuteChartAsync(today);
            var ranking = await _statsQueryService.GetAppRankingAsync(today);

            await Dispatcher.EnqueueAsync(() =>
            {
                TodayTotalChars = summary?.TotalChars ?? _typingMonitorService.CurrentSnapshot.TodayCharCount;
                TopAppDisplayName = summary?.TopAppName ?? _typingMonitorService.CurrentSnapshot.TopAppDisplayName ?? "No app yet";
                _rawMinuteChart = chart;
                UpdateChart(chart);
                ReplaceRanking(ranking);
            });

            _lastRefreshUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async void OnSnapshotUpdated(object? sender, RealtimeStatsSnapshot snapshot)
    {
        await Dispatcher.EnqueueAsync(() =>
        {
            TodayTotalChars = snapshot.TodayCharCount;
            TopAppDisplayName = snapshot.TopAppDisplayName ?? "No app yet";
        });

        if (DateTimeOffset.UtcNow - _lastRefreshUtc >= TimeSpan.FromSeconds(5))
        {
            await RefreshAsync();
        }
    }

    private void UpdateChart(IReadOnlyList<MinuteChartPoint> points)
    {
        var bucketed = TimeBucketChartBuilder.BuildForDate(
            points,
            DateTime.Now.Date,
            SelectedBucketOption,
            SelectedRangeOption,
            DateTime.Now);

        MinuteSeries = [CreateSeries(bucketed.Values, bucketed.SeriesName)];
        XAxes = [CreateXAxis(bucketed.Labels)];
        YAxes = [CreateYAxis(bucketed.AxisName)];

        var currentBucket = points.LastOrDefault();
        CurrentMinuteChars = currentBucket?.CharCount ?? 0;
        PeakMinuteChars = points.Count == 0 ? 0 : points.Max(static point => point.CharCount);
    }

    private void ReplaceRanking(IReadOnlyList<AppRankingItem> items)
    {
        AppRanking.Clear();
        foreach (var item in items)
        {
            AppRanking.Add(item);
        }
    }

    private static LineSeries<double> CreateSeries(IEnumerable<double> values, string tooltipLabel)
    {
        return new LineSeries<double>
        {
            Name = string.Empty,
            Values = values.ToArray(),
            GeometrySize = 0,
            LineSmoothness = 0.8,
            Fill = null,
            Stroke = new SolidColorPaint(new SKColor(22, 138, 173), 3),
            GeometryStroke = new SolidColorPaint(new SKColor(242, 165, 65), 2),
            GeometryFill = new SolidColorPaint(new SKColor(242, 165, 65)),
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
            Name = axisName,
            MinLimit = 0,
            LabelsPaint = new SolidColorPaint(new SKColor(235, 239, 244)),
            NamePaint = new SolidColorPaint(new SKColor(235, 239, 244)),
            SeparatorsPaint = new SolidColorPaint(new SKColor(70, 78, 88))
        };
    }
}

