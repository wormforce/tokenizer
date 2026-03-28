using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Models;
using Tokenizer.App.Views;

namespace Tokenizer.App.Services;

public sealed class FloatingBallService(
    FloatingBallWindow window,
    ISettingsRepository settingsRepository) : IFloatingBallService
{
    private AppSettingsModel _settings = new();

    public event EventHandler? OpenRequested;

    public event EventHandler? TogglePauseRequested;

    public event EventHandler? ExitRequested;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await window.InitializeAsync(cancellationToken);
        window.OpenRequested += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        window.TogglePauseRequested += (_, _) => TogglePauseRequested?.Invoke(this, EventArgs.Empty);
        window.ExitRequested += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        window.PositionCommitted += OnPositionCommitted;
    }

    public Task ShowAsync(CancellationToken cancellationToken = default)
    {
        window.ShowWindow();
        return Task.CompletedTask;
    }

    public Task HideAsync(CancellationToken cancellationToken = default)
    {
        window.HideWindow();
        return Task.CompletedTask;
    }

    public Task UpdateSnapshotAsync(RealtimeStatsSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        window.ApplySnapshot(snapshot);
        return Task.CompletedTask;
    }

    public Task UpdateSettingsAsync(AppSettingsModel settings, CancellationToken cancellationToken = default)
    {
        _settings = settings;
        window.ApplySettings(settings);
        return Task.CompletedTask;
    }

    public Task ShutdownAsync()
    {
        window.CloseWindow();
        return Task.CompletedTask;
    }

    private async void OnPositionCommitted(object? sender, FloatingBallPositionChangedEventArgs e)
    {
        _settings.FloatingEdge = e.Edge;
        _settings.FloatingOffsetX = e.OffsetX;
        _settings.FloatingOffsetY = e.OffsetY;
        await settingsRepository.SaveAsync(_settings);
    }
}

