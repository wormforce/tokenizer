namespace Tokenizer.Core.Interfaces;

public interface ITrayService : IAsyncDisposable
{
    event EventHandler? OpenRequested;

    event EventHandler? TogglePauseRequested;

    event EventHandler? ExitRequested;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task UpdateStateAsync(bool paused, int currentCps, CancellationToken cancellationToken = default);
}

