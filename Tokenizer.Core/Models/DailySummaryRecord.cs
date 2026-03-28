namespace Tokenizer.Core.Models;

public sealed record DailySummaryRecord(
    string StatDate,
    int TotalChars,
    int PeakCps,
    int? TopAppId,
    string? TopAppName,
    DateTimeOffset UpdatedAtUtc);

