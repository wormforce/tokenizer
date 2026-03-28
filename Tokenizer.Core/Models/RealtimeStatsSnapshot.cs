namespace Tokenizer.Core.Models;

public sealed record RealtimeStatsSnapshot(
    DateTimeOffset CapturedAtUtc,
    int CurrentCps,
    int WindowCharCount,
    double WindowDurationSeconds,
    int TodayCharCount,
    int TodayPeakCps,
    string? TopAppDisplayName,
    bool IsPaused);

