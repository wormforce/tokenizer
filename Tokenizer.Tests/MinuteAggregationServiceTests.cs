using Tokenizer.Core.Statistics;

namespace Tokenizer.Tests;

public sealed class MinuteAggregationServiceTests
{
    [Fact]
    public void Flush_ReturnsCombinedBucketSnapshot()
    {
        var service = new MinuteAggregationService();
        var baseTime = new DateTimeOffset(2026, 03, 27, 12, 00, 00, TimeSpan.Zero);

        service.Register(baseTime, "2026-03-27", 1, 1);
        service.Register(baseTime.AddSeconds(1), "2026-03-27", 1, 2);
        service.Register(baseTime.AddSeconds(20), "2026-03-27", 1, 4);

        var records = service.Flush(baseTime.AddSeconds(25));
        var record = Assert.Single(records);

        Assert.Equal(3, record.CharCount);
        Assert.Equal(4, record.PeakCps);
        Assert.Equal(3, record.ActiveSeconds);
        Assert.Equal("2026-03-27", record.LocalDate);
    }

    [Fact]
    public void Register_OnMinuteBoundaryFlushesPreviousBucket()
    {
        var service = new MinuteAggregationService();
        var beforeBoundary = new DateTimeOffset(2026, 03, 27, 12, 00, 59, TimeSpan.Zero);
        var afterBoundary = beforeBoundary.AddSeconds(2);

        service.Register(beforeBoundary, "2026-03-27", 1, 1);
        var result = service.Register(afterBoundary, "2026-03-27", 1, 1);

        Assert.Contains(result.PersistRecords, static record => record.BucketStartUtc == new DateTimeOffset(2026, 03, 27, 12, 00, 00, TimeSpan.Zero));
    }
}

