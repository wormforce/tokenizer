using Tokenizer.Core.Models;

namespace Tokenizer.Core.Interfaces;

public interface ISettingsRepository
{
    Task<AppSettingsModel> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettingsModel settings, CancellationToken cancellationToken = default);
}

