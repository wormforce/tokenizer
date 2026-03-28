using Tokenizer.Core.Models;

namespace Tokenizer.Core.Interfaces;

public interface IFloatingBallService
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task ShowAsync(CancellationToken cancellationToken = default);

    Task HideAsync(CancellationToken cancellationToken = default);

    Task UpdateSnapshotAsync(RealtimeStatsSnapshot snapshot, CancellationToken cancellationToken = default);

    Task UpdateSettingsAsync(AppSettingsModel settings, CancellationToken cancellationToken = default);
}

