namespace Tokenizer.Core.Interfaces;

public interface IAutostartService
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    Task<bool> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}

