using Tokenizer.Core.Models;

namespace Tokenizer.Core.Interfaces;

public interface IKeyboardHookService : IAsyncDisposable
{
    event EventHandler<KeyStrokeSample>? KeyCaptured;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

