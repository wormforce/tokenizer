namespace Tokenizer.Core.Models;

public sealed record DailyTrendPoint(
    string StatDate,
    int TotalChars,
    int PeakCps);

