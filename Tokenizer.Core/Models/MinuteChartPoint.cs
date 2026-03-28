namespace Tokenizer.Core.Models;

public sealed record MinuteChartPoint(
    DateTimeOffset BucketStartUtc,
    int CharCount,
    double AvgCps,
    int PeakCps);

