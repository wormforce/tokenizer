using Tokenizer.App.Services;
using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Models;

namespace Tokenizer.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IAutostartService _autostartService;
    private readonly ITypingMonitorService _typingMonitorService;
    private CancellationTokenSource? _applyDebounceCts;
    private AppSettingsModel _loadedSettings = new();
    private bool _isInitializing;

    private const int MinimumFloatingBallOpacityPercent = 20;
    private const int MaximumFloatingBallOpacityPercent = 100;
    private const int MinimumFloatingBallSize = 60;
    private const int MaximumFloatingBallSize = 120;

    private bool _autostartEnabled;
    private bool _floatingBallEnabled;
    private double _floatingBallOpacity = AppSettingsModel.DefaultFloatingBallOpacityPercent / 100d;
    private double _floatingBallSize = AppSettingsModel.DefaultFloatingBallSize;
    private bool _launchMinimized;
    private bool _paused;
    private string _statusMessage = "Ready";

    public SettingsViewModel(
        ISettingsRepository settingsRepository,
        IAutostartService autostartService,
        ITypingMonitorService typingMonitorService,
        IAppDispatcher dispatcher) : base(dispatcher)
    {
        _settingsRepository = settingsRepository;
        _autostartService = autostartService;
        _typingMonitorService = typingMonitorService;
    }

    public bool AutostartEnabled
    {
        get => _autostartEnabled;
        set
        {
            if (SetProperty(ref _autostartEnabled, value))
            {
                ScheduleApply();
            }
        }
    }

    public bool FloatingBallEnabled
    {
        get => _floatingBallEnabled;
        set
        {
            if (SetProperty(ref _floatingBallEnabled, value))
            {
                ScheduleApply();
            }
        }
    }

    public double FloatingBallOpacity
    {
        get => _floatingBallOpacity;
        set
        {
            if (SetProperty(ref _floatingBallOpacity, value))
            {
                OnPropertyChanged(nameof(FloatingBallOpacityLabel));
                ScheduleApply();
            }
        }
    }

    public string FloatingBallOpacityLabel => $"{Math.Round(FloatingBallOpacity * 100d)}%";

    public double FloatingBallSize
    {
        get => _floatingBallSize;
        set
        {
            if (SetProperty(ref _floatingBallSize, value))
            {
                OnPropertyChanged(nameof(FloatingBallSizeLabel));
                ScheduleApply();
            }
        }
    }

    public string FloatingBallSizeLabel => $"{Math.Round(FloatingBallSize)} px";

    public bool LaunchMinimized
    {
        get => _launchMinimized;
        set
        {
            if (SetProperty(ref _launchMinimized, value))
            {
                ScheduleApply();
            }
        }
    }

    public bool Paused
    {
        get => _paused;
        set
        {
            if (SetProperty(ref _paused, value))
            {
                ScheduleApply();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public async Task LoadAsync()
    {
        _isInitializing = true;

        var settings = await _settingsRepository.GetAsync();
        var autostartEnabled = await _autostartService.IsEnabledAsync();
        _loadedSettings = settings;

        await Dispatcher.EnqueueAsync(() =>
        {
            AutostartEnabled = autostartEnabled;
            FloatingBallEnabled = settings.FloatingBallEnabled;
            FloatingBallOpacity = ClampToInt(settings.FloatingBallOpacityPercent, MinimumFloatingBallOpacityPercent, MaximumFloatingBallOpacityPercent) / 100d;
            FloatingBallSize = ClampToInt(settings.FloatingBallSize, MinimumFloatingBallSize, MaximumFloatingBallSize);
            LaunchMinimized = settings.LaunchMinimized;
            Paused = settings.Paused;
            StatusMessage = "Changes apply automatically";
        });

        _isInitializing = false;
    }

    private void ScheduleApply()
    {
        if (_isInitializing)
        {
            return;
        }

        _applyDebounceCts?.Cancel();
        _applyDebounceCts?.Dispose();
        _applyDebounceCts = new CancellationTokenSource();
        var token = _applyDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(250, token);
                await ApplyCurrentSettingsAsync(token);
            }
            catch (OperationCanceledException)
            {
                // Ignore superseded changes.
            }
        }, token);
    }

    private async Task ApplyCurrentSettingsAsync(CancellationToken cancellationToken)
    {
        await Dispatcher.EnqueueAsync(() => StatusMessage = "Applying changes...");

        var autostartResult = await _autostartService.SetEnabledAsync(AutostartEnabled);
        cancellationToken.ThrowIfCancellationRequested();

        var settings = new AppSettingsModel
        {
            AutostartEnabled = autostartResult,
            FloatingBallEnabled = FloatingBallEnabled,
            FloatingEdge = _loadedSettings.FloatingEdge,
            LaunchMinimized = LaunchMinimized,
            Paused = Paused,
            FloatingOffsetX = _loadedSettings.FloatingOffsetX,
            FloatingOffsetY = _loadedSettings.FloatingOffsetY,
            FloatingBallOpacityPercent = ClampToInt(FloatingBallOpacity * 100d, MinimumFloatingBallOpacityPercent, MaximumFloatingBallOpacityPercent),
            FloatingBallSize = ClampToInt(FloatingBallSize, MinimumFloatingBallSize, MaximumFloatingBallSize)
        };

        await _typingMonitorService.ApplySettingsAsync(settings, cancellationToken);
        _loadedSettings = settings;

        await Dispatcher.EnqueueAsync(() =>
        {
            AutostartEnabled = autostartResult;
            StatusMessage = autostartResult || !AutostartEnabled
                ? "Applied automatically"
                : "Autostart request was rejected by the system";
        });
    }

    private static int ClampToInt(double value, int minimum, int maximum)
        => (int)Math.Clamp(Math.Round(value), minimum, maximum);
}
