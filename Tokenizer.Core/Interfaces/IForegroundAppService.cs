using Tokenizer.Core.Models;

namespace Tokenizer.Core.Interfaces;

public interface IForegroundAppService
{
    Task<ForegroundAppInfo?> ResolveAsync(KeyStrokeSample sample, CancellationToken cancellationToken = default);
}

