using Tokenizer.Core.Models;

namespace Tokenizer.Core.Interfaces;

public interface ITypingMonitorService
{
    event EventHandler<RealtimeStatsSnapshot>? SnapshotUpdated;

    RealtimeStatsSnapshot CurrentSnapshot { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SetPausedAsync(bool paused, CancellationToken cancellationToken = default);

    Task ApplySettingsAsync(AppSettingsModel settings, CancellationToken cancellationToken = default);

    Task FlushAsync(CancellationToken cancellationToken = default);
}

