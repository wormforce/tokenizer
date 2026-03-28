using CommunityToolkit.Mvvm.Input;
using Tokenizer.App.Services;
using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Models;

namespace Tokenizer.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsRepository _settingsRepository;
    private readonly IAutostartService _autostartService;
    private readonly ITypingMonitorService _typingMonitorService;
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
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public IAsyncRelayCommand SaveCommand { get; }

    public bool AutostartEnabled
    {
        get => _autostartEnabled;
        set => SetProperty(ref _autostartEnabled, value);
    }

    public bool FloatingBallEnabled
    {
        get => _floatingBallEnabled;
        set => SetProperty(ref _floatingBallEnabled, value);
    }

    public double FloatingBallOpacity
    {
        get => _floatingBallOpacity;
        set
        {
            if (SetProperty(ref _floatingBallOpacity, value))
            {
                OnPropertyChanged(nameof(FloatingBallOpacityLabel));
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
            }
        }
    }

    public string FloatingBallSizeLabel => $"{Math.Round(FloatingBallSize)} px";

    public bool LaunchMinimized
    {
        get => _launchMinimized;
        set => SetProperty(ref _launchMinimized, value);
    }

    public bool Paused
    {
        get => _paused;
        set => SetProperty(ref _paused, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public async Task LoadAsync()
    {
        var settings = await _settingsRepository.GetAsync();
        var autostartEnabled = await _autostartService.IsEnabledAsync();

        await Dispatcher.EnqueueAsync(() =>
        {
            AutostartEnabled = autostartEnabled;
            FloatingBallEnabled = settings.FloatingBallEnabled;
            FloatingBallOpacity = ClampToInt(settings.FloatingBallOpacityPercent, MinimumFloatingBallOpacityPercent, MaximumFloatingBallOpacityPercent) / 100d;
            FloatingBallSize = ClampToInt(settings.FloatingBallSize, MinimumFloatingBallSize, MaximumFloatingBallSize);
            LaunchMinimized = settings.LaunchMinimized;
            Paused = settings.Paused;
            StatusMessage = "Settings loaded";
        });
    }

    public async Task SaveAsync()
    {
        var current = await _settingsRepository.GetAsync();
        var autostartResult = await _autostartService.SetEnabledAsync(AutostartEnabled);
        var settings = new AppSettingsModel
        {
            AutostartEnabled = autostartResult,
            FloatingBallEnabled = FloatingBallEnabled,
            FloatingEdge = current.FloatingEdge,
            LaunchMinimized = LaunchMinimized,
            Paused = Paused,
            FloatingOffsetX = current.FloatingOffsetX,
            FloatingOffsetY = current.FloatingOffsetY,
            FloatingBallOpacityPercent = ClampToInt(FloatingBallOpacity * 100d, MinimumFloatingBallOpacityPercent, MaximumFloatingBallOpacityPercent),
            FloatingBallSize = ClampToInt(FloatingBallSize, MinimumFloatingBallSize, MaximumFloatingBallSize)
        };

        await _typingMonitorService.ApplySettingsAsync(settings);

        await Dispatcher.EnqueueAsync(() =>
        {
            AutostartEnabled = autostartResult;
            StatusMessage = autostartResult || !AutostartEnabled
                ? "Saved and applied"
                : "Autostart request was rejected by the system";
        });
    }

    private static int ClampToInt(double value, int minimum, int maximum)
        => (int)Math.Clamp(Math.Round(value), minimum, maximum);
}

