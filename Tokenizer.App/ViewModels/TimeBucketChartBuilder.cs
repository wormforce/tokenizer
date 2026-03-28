using Tokenizer.Core.Models;

namespace Tokenizer.App.ViewModels;

internal sealed record BucketedChartData(
    IReadOnlyList<double> Values,
    IReadOnlyList<string> Labels,
    string SeriesName,
    string AxisName);

internal static class TimeBucketChartBuilder
{
    public static BucketedChartData BuildForDate(
        IReadOnlyList<MinuteChartPoint> rawPoints,
        DateTime dateLocal,
        TimeBucketOption bucket,
        TimeRangeOption range,
        DateTime nowLocal)
    {
        var dayStart = dateLocal.Date;
        var fullRangeEndExclusive = dayStart.AddDays(1);
        var fullBucketCount = Math.Max(1, (int)Math.Ceiling((fullRangeEndExclusive - dayStart).TotalMinutes / bucket.Minutes));
        var fullValues = new double[fullBucketCount];

        foreach (var point in rawPoints)
        {
            var localTime = point.BucketStartUtc.ToLocalTime().DateTime;
            if (localTime < dayStart || localTime >= fullRangeEndExclusive)
            {
                continue;
            }

            var bucketIndex = (int)((localTime - dayStart).TotalMinutes / bucket.Minutes);
            if (bucketIndex >= 0 && bucketIndex < fullValues.Length)
            {
                fullValues[bucketIndex] += point.CharCount;
            }
        }

        var (startIndex, endIndex) = ResolveRange(fullValues, range);
        var bucketCount = (endIndex - startIndex) + 1;
        var values = new double[bucketCount];
        Array.Copy(fullValues, startIndex, values, 0, bucketCount);

        var labels = BuildLabels(dayStart.AddMinutes(startIndex * bucket.Minutes), bucket.Minutes, bucketCount);
        return new BucketedChartData(
            values,
            labels,
            $"Chars / {bucket.Label}",
            "chars");
    }

    private static string[] BuildLabels(DateTime startLocal, int bucketMinutes, int bucketCount)
    {
        var labelEveryBuckets = Math.Max(1, 60 / bucketMinutes);
        if (bucketMinutes >= 60)
        {
            labelEveryBuckets = 1;
        }

        var labels = new string[bucketCount];
        for (var index = 0; index < bucketCount; index++)
        {
            var bucketTime = startLocal.AddMinutes(index * bucketMinutes);
            labels[index] = index % labelEveryBuckets == 0
                ? bucketTime.ToString("HH:mm")
                : string.Empty;
        }

        if (bucketCount > 0)
        {
            labels[^1] = startLocal.AddMinutes((bucketCount - 1) * bucketMinutes).ToString("HH:mm");
        }

        return labels;
    }

    private static (int StartIndex, int EndIndex) ResolveRange(double[] values, TimeRangeOption range)
    {
        if (range.FullDay || values.Length == 0)
        {
            return (0, Math.Max(0, values.Length - 1));
        }

        var firstNonZero = Array.FindIndex(values, static value => value > 0);
        var lastNonZero = Array.FindLastIndex(values, static value => value > 0);
        if (firstNonZero < 0 || lastNonZero < 0)
        {
            return (0, Math.Max(0, values.Length - 1));
        }

        var startIndex = Math.Max(0, firstNonZero - 1);
        return (startIndex, lastNonZero);
    }
}

