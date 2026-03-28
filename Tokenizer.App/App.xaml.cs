using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Tokenizer.App.Diagnostics;
using Tokenizer.App.Services;
using Tokenizer.App.ViewModels;
using Tokenizer.App.Views;
using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Statistics;
using Tokenizer.Infrastructure.Autostart;
using Tokenizer.Infrastructure.InputHook;
using Tokenizer.Infrastructure.Storage;
using Tokenizer.Infrastructure.Tray;
using Tokenizer.Infrastructure.Windows;

namespace Tokenizer.App;

public partial class App : Application
{
    private bool _isLaunched;
    private Task? _launchTask;
    private MainWindow? _mainWindow;

    public App()
    {
        InitializeComponent();
        WheelDiagnostics.StartSession("app-start");
        Host = BuildHost();
    }

    public IHost Host { get; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (_isLaunched)
        {
            _ = Host.Services.GetRequiredService<AppWindowService>().ShowMainWindowAsync();
            return;
        }

        var windowService = Host.Services.GetRequiredService<AppWindowService>();
        _mainWindow = Host.Services.GetRequiredService<MainWindow>();
        windowService.RegisterMainWindow(_mainWindow);
        _mainWindow.ShowWindow();

        _isLaunched = true;
        _launchTask ??= LaunchAsync();
    }

    private async Task LaunchAsync()
    {
        await Host.StartAsync();

        var windowService = Host.Services.GetRequiredService<AppWindowService>();
        WireCommands();

        var settings = await Host.Services.GetRequiredService<ISettingsRepository>().GetAsync();
        if (settings.LaunchMinimized)
        {
            await windowService.HideMainWindowAsync();
        }
        else
        {
            await windowService.ShowMainWindowAsync();
        }
    }

    private void WireCommands()
    {
        var trayService = Host.Services.GetRequiredService<ITrayService>();
        trayService.OpenRequested += OnOpenRequested;
        trayService.ExitRequested += OnExitRequested;

        var floatingBallService = Host.Services.GetRequiredService<FloatingBallService>();
        floatingBallService.OpenRequested += OnOpenRequested;
        floatingBallService.TogglePauseRequested += OnTogglePauseRequested;
        floatingBallService.ExitRequested += OnExitRequested;
    }

    private async void OnOpenRequested(object? sender, EventArgs e)
    {
        await Host.Services.GetRequiredService<AppWindowService>().ShowMainWindowAsync();
    }

    private async void OnTogglePauseRequested(object? sender, EventArgs e)
    {
        var typingMonitor = Host.Services.GetRequiredService<ITypingMonitorService>();
        await typingMonitor.SetPausedAsync(!typingMonitor.CurrentSnapshot.IsPaused);
    }

    private async void OnExitRequested(object? sender, EventArgs e)
    {
        await Host.Services.GetRequiredService<FloatingBallService>().ShutdownAsync();
        await Host.StopAsync();
        await Host.Services.GetRequiredService<AppWindowService>().RequestExitAsync();
        Exit();
    }

    private IHost BuildHost()
    {
        return Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                var dispatcher = DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("DispatcherQueue is not available.");
                services.AddSingleton<IAppDispatcher>(new AppDispatcher(dispatcher));

                services.AddSingleton<SqliteDatabase>();
                services.AddSingleton<IStatsRepository, SqliteStatsRepository>();
                services.AddSingleton<ISettingsRepository, SqliteSettingsRepository>();
                services.AddSingleton<IStatsQueryService, SqliteStatsQueryService>();

                services.AddSingleton<IKeyboardHookService, LowLevelKeyboardHookService>();
                services.AddSingleton<IForegroundAppService, ForegroundAppService>();
                services.AddSingleton<IAutostartService, StartupTaskAutostartService>();
                services.AddSingleton<ITrayService, NotifyIconTrayService>();
                services.AddSingleton<IRealtimeSpeedCalculator, RollingCpsCalculator>();
                services.AddSingleton<IMinuteAggregationService, MinuteAggregationService>();

                services.AddSingleton<AppWindowService>();
                services.AddSingleton<FloatingBallViewModel>();
                services.AddSingleton<FloatingBallWindow>();
                services.AddSingleton<FloatingBallService>();
                services.AddSingleton<IFloatingBallService>(sp => sp.GetRequiredService<FloatingBallService>());

                services.AddSingleton<TypingMonitorService>();
                services.AddSingleton<ITypingMonitorService>(sp => sp.GetRequiredService<TypingMonitorService>());
                services.AddHostedService(sp => new TypingMonitorHostedService(sp.GetRequiredService<TypingMonitorService>()));

                services.AddSingleton<ShellViewModel>();
                services.AddSingleton<TodayViewModel>();
                services.AddSingleton<HistoryViewModel>();
                services.AddSingleton<SettingsViewModel>();

                services.AddTransient<TodayPage>();
                services.AddTransient<HistoryPage>();
                services.AddTransient<SettingsPage>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }
}

