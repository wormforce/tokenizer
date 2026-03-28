namespace Tokenizer.Core.Models;

public sealed record MinuteStatRecord(
    DateTimeOffset BucketStartUtc,
    string LocalDate,
    int AppId,
    int CharCount,
    double AvgCps,
    int PeakCps,
    int ActiveSeconds,
    DateTimeOffset UpdatedAtUtc);

