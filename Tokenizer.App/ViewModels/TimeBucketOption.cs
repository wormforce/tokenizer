namespace Tokenizer.App.ViewModels;

public sealed record TimeBucketOption(string Label, int Minutes)
{
    public static TimeBucketOption OneMinute { get; } = new("1 min", 1);

    public static TimeBucketOption FifteenMinutes { get; } = new("15 min", 15);

    public static TimeBucketOption ThirtyMinutes { get; } = new("30 min", 30);

    public static TimeBucketOption OneHour { get; } = new("1 hour", 60);

    public static IReadOnlyList<TimeBucketOption> All { get; } =
    [
        OneMinute,
        FifteenMinutes,
        ThirtyMinutes,
        OneHour
    ];
}

