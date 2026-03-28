using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Models;
using Tokenizer.Core.Statistics;

namespace Tokenizer.Infrastructure.Windows;

public sealed class TypingMonitorService : ITypingMonitorService, IAsyncDisposable
{
    private static readonly TimeSpan IdleRefreshInterval = TimeSpan.FromMilliseconds(250);
    private readonly IKeyboardHookService _keyboardHookService;
    private readonly IForegroundAppService _foregroundAppService;
    private readonly IStatsRepository _statsRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ITrayService _trayService;
    private readonly IFloatingBallService _floatingBallService;
    private readonly IAutostartService _autostartService;
    private readonly IRealtimeSpeedCalculator _realtimeSpeedCalculator;
    private readonly IMinuteAggregationService _minuteAggregationService;
    private readonly ILogger<TypingMonitorService> _logger;
    private readonly DailySummaryAccumulator _dailySummaryAccumulator = new();
    private readonly Channel<KeyStrokeSample> _channel = Channel.CreateUnbounded<KeyStrokeSample>();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

    private Task? _consumerTask;
    private Task? _idleMonitorTask;
    private CancellationTokenSource? _cts;
    private AppSettingsModel _settings = new();
    private bool _initialized;

    public TypingMonitorService(
        IKeyboardHookService keyboardHookService,
        IForegroundAppService foregroundAppService,
        IStatsRepository statsRepository,
        ISettingsRepository settingsRepository,
        ITrayService trayService,
        IFloatingBallService floatingBallService,
        IAutostartService autostartService,
        IRealtimeSpeedCalculator realtimeSpeedCalculator,
        IMinuteAggregationService minuteAggregationService,
        ILogger<TypingMonitorService> logger)
    {
        _keyboardHookService = keyboardHookService;
        _foregroundAppService = foregroundAppService;
        _statsRepository = statsRepository;
        _settingsRepository = settingsRepository;
        _trayService = trayService;
        _floatingBallService = floatingBallService;
        _autostartService = autostartService;
        _realtimeSpeedCalculator = realtimeSpeedCalculator;
        _minuteAggregationService = minuteAggregationService;
        _logger = logger;
        CurrentSnapshot = new RealtimeStatsSnapshot(DateTimeOffset.UtcNow, 0, 0, 2, 0, 0, null, false);
    }

    public event EventHandler<RealtimeStatsSnapshot>? SnapshotUpdated;

    public RealtimeStatsSnapshot CurrentSnapshot { get; private set; }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await _statsRepository.InitializeAsync(cancellationToken);
            _settings = await _settingsRepository.GetAsync(cancellationToken);

            var autostartEnabled = await _autostartService.IsEnabledAsync(cancellationToken);
            if (_settings.AutostartEnabled != autostartEnabled)
            {
                _settings.AutostartEnabled = autostartEnabled;
                await _settingsRepository.SaveAsync(_settings, cancellationToken);
            }

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            var todaySummary = await _statsRepository.GetDailySummaryAsync(today, cancellationToken);
            var todayRanking = await _statsRepository.GetAppRankingAsync(today, cancellationToken);
            if (todaySummary is not null)
            {
                _dailySummaryAccumulator.Seed(
                    todaySummary.StatDate,
                    todaySummary.TotalChars,
                    todaySummary.PeakCps,
                    todayRanking,
                    todaySummary.TopAppName);
            }

            CurrentSnapshot = BuildSnapshot(0);

            _trayService.TogglePauseRequested += OnTogglePauseRequested;
            await _trayService.InitializeAsync(cancellationToken);
            await _trayService.UpdateStateAsync(_settings.Paused, 0, cancellationToken);

            await _floatingBallService.InitializeAsync(cancellationToken);
            await _floatingBallService.UpdateSettingsAsync(_settings, cancellationToken);
            if (_settings.FloatingBallEnabled)
            {
                await _floatingBallService.ShowAsync(cancellationToken);
            }
            else
            {
                await _floatingBallService.HideAsync(cancellationToken);
            }

            _keyboardHookService.KeyCaptured += OnKeyCaptured;
            _consumerTask = Task.Run(() => ConsumeAsync(_cts.Token), _cts.Token);
            _idleMonitorTask = Task.Run(() => MonitorIdleSpeedAsync(_cts.Token), _cts.Token);
            await _keyboardHookService.StartAsync(cancellationToken);

            await PublishSnapshotAsync(CurrentSnapshot, cancellationToken);
            _initialized = true;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task SetPausedAsync(bool paused, CancellationToken cancellationToken = default)
    {
        _settings.Paused = paused;
        await _settingsRepository.SaveAsync(_settings, cancellationToken);

        if (paused)
        {
            _realtimeSpeedCalculator.Reset();
        }

        var snapshot = BuildSnapshot(paused ? 0 : _realtimeSpeedCalculator.CurrentCps);
        await PublishSnapshotAsync(snapshot, cancellationToken);
    }

    public async Task ApplySettingsAsync(AppSettingsModel settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        await _settingsRepository.SaveAsync(_settings, cancellationToken);
        await _floatingBallService.UpdateSettingsAsync(_settings, cancellationToken);

        if (_settings.FloatingBallEnabled)
        {
            await _floatingBallService.ShowAsync(cancellationToken);
        }
        else
        {
            await _floatingBallService.HideAsync(cancellationToken);
        }

        if (_settings.Paused != CurrentSnapshot.IsPaused)
        {
            await SetPausedAsync(_settings.Paused, cancellationToken);
            return;
        }

        await PublishSnapshotAsync(BuildSnapshot(_realtimeSpeedCalculator.CurrentCps), cancellationToken);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var records = _minuteAggregationService.Flush(now);
        foreach (var record in records)
        {
            await _statsRepository.UpsertMinuteStatAsync(record, cancellationToken);
        }

        await _statsRepository.UpsertDailySummaryAsync(_dailySummaryAccumulator.Build(now), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts is not null)
        {
            try
            {
                _cts.Cancel();
            }
            catch
            {
                // Ignore shutdown races.
            }
        }

        _keyboardHookService.KeyCaptured -= OnKeyCaptured;
        _trayService.TogglePauseRequested -= OnTogglePauseRequested;

        if (_consumerTask is not null)
        {
            try
            {
                await _consumerTask;
            }
            catch
            {
                // Ignore during shutdown.
            }
        }

        if (_idleMonitorTask is not null)
        {
            try
            {
                await _idleMonitorTask;
            }
            catch
            {
                // Ignore during shutdown.
            }
        }

        try
        {
            await _keyboardHookService.StopAsync();
        }
        catch
        {
            // Ignore during shutdown.
        }

        await _keyboardHookService.DisposeAsync();
        await _trayService.DisposeAsync();

        _cts?.Dispose();
        _lifecycleGate.Dispose();
    }

    private void OnKeyCaptured(object? sender, KeyStrokeSample sample)
    {
        if (_settings.Paused || sample.IsInjected || !VisibleKeyClassifier.IsCountable(sample.VirtualKey))
        {
            return;
        }

        _channel.Writer.TryWrite(sample);
    }

    private async void OnTogglePauseRequested(object? sender, EventArgs e)
    {
        try
        {
            await SetPausedAsync(!_settings.Paused);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle pause state from tray command.");
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var sample in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                var appInfo = await _foregroundAppService.ResolveAsync(sample, cancellationToken);
                if (appInfo is null)
                {
                    continue;
                }

                var appId = await _statsRepository.UpsertAppProfileAsync(appInfo, cancellationToken);
                var currentCps = _realtimeSpeedCalculator.Register(sample.CapturedAtUtc);
                var localDate = sample.CapturedAtUtc.ToLocalTime().ToString("yyyy-MM-dd");
                var aggregationResult = _minuteAggregationService.Register(sample.CapturedAtUtc, localDate, appId, currentCps);

                foreach (var record in aggregationResult.PersistRecords)
                {
                    await _statsRepository.UpsertMinuteStatAsync(record, cancellationToken);
                }

                _dailySummaryAccumulator.Register(localDate, appId, appInfo.DisplayName, currentCps);
                var summary = _dailySummaryAccumulator.Build(sample.CapturedAtUtc);
                await _statsRepository.UpsertDailySummaryAsync(summary, cancellationToken);

                var snapshot = BuildSnapshot(currentCps);
                await PublishSnapshotAsync(snapshot, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Typing monitor consumer loop crashed.");
        }
    }

    private async Task MonitorIdleSpeedAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(IdleRefreshInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (_settings.Paused || CurrentSnapshot.CurrentCps == 0)
                {
                    continue;
                }

                var currentCps = _realtimeSpeedCalculator.Refresh(DateTimeOffset.UtcNow);
                if (currentCps == CurrentSnapshot.CurrentCps)
                {
                    continue;
                }

                await PublishSnapshotAsync(BuildSnapshot(currentCps), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private RealtimeStatsSnapshot BuildSnapshot(int currentCps)
    {
        return new RealtimeStatsSnapshot(
            DateTimeOffset.UtcNow,
            currentCps,
            currentCps * 2,
            2,
            _dailySummaryAccumulator.TodayCharCount,
            _dailySummaryAccumulator.TodayPeakCps,
            _dailySummaryAccumulator.TopAppDisplayName,
            _settings.Paused);
    }

    private async Task PublishSnapshotAsync(RealtimeStatsSnapshot snapshot, CancellationToken cancellationToken)
    {
        CurrentSnapshot = snapshot;
        SnapshotUpdated?.Invoke(this, snapshot);
        await _trayService.UpdateStateAsync(snapshot.IsPaused, snapshot.CurrentCps, cancellationToken);
        await _floatingBallService.UpdateSnapshotAsync(snapshot, cancellationToken);
    }
}

