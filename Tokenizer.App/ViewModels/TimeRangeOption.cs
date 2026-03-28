namespace Tokenizer.App.ViewModels;

public sealed record TimeRangeOption(string Label, bool FullDay)
{
    public static TimeRangeOption DataWindow { get; } = new("Data only", false);

    public static TimeRangeOption FullDayRange { get; } = new("0-24 h", true);

    public static IReadOnlyList<TimeRangeOption> All { get; } =
    [
        DataWindow,
        FullDayRange
    ];
}

