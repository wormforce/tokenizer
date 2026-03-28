using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Models;

namespace Tokenizer.Core.Statistics;

public sealed class MinuteAggregationService : IMinuteAggregationService
{
    private readonly Dictionary<(DateTimeOffset BucketStartUtc, string LocalDate, int AppId), MutableBucket> _buckets = new();
    private DateTimeOffset? _currentBucketStartUtc;
    private DateTimeOffset _lastFlushUtc = DateTimeOffset.MinValue;

    public AggregationResult Register(DateTimeOffset capturedAtUtc, string localDate, int appId, int realtimeCps)
    {
        var bucketStartUtc = new DateTimeOffset(
            capturedAtUtc.Year,
            capturedAtUtc.Month,
            capturedAtUtc.Day,
            capturedAtUtc.Hour,
            capturedAtUtc.Minute,
            0,
            TimeSpan.Zero);

        var rolledOver = _currentBucketStartUtc.HasValue && bucketStartUtc != _currentBucketStartUtc.Value;
        var records = new List<MinuteStatRecord>();

        if (rolledOver)
        {
            records.AddRange(BuildSnapshots(capturedAtUtc));
            _buckets.Clear();
        }

        _currentBucketStartUtc = bucketStartUtc;

        var key = (bucketStartUtc, localDate, appId);
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            bucket = new MutableBucket(bucketStartUtc, localDate, appId);
            _buckets[key] = bucket;
        }

        bucket.Register(capturedAtUtc, realtimeCps);

        if (_lastFlushUtc == DateTimeOffset.MinValue || capturedAtUtc - _lastFlushUtc >= TimeSpan.FromSeconds(5))
        {
            _lastFlushUtc = capturedAtUtc;
            records.AddRange(BuildSnapshots(capturedAtUtc));
        }

        return new AggregationResult(records, rolledOver);
    }

    public IReadOnlyCollection<MinuteStatRecord> Flush(DateTimeOffset flushedAtUtc)
    {
        var records = BuildSnapshots(flushedAtUtc);
        _buckets.Clear();
        _currentBucketStartUtc = null;
        _lastFlushUtc = flushedAtUtc;
        return records;
    }

    public void Reset()
    {
        _buckets.Clear();
        _currentBucketStartUtc = null;
        _lastFlushUtc = DateTimeOffset.MinValue;
    }

    private IReadOnlyCollection<MinuteStatRecord> BuildSnapshots(DateTimeOffset timestamp)
    {
        return _buckets.Values
            .Select(bucket => bucket.ToRecord(timestamp))
            .ToArray();
    }

    private sealed class MutableBucket(DateTimeOffset bucketStartUtc, string localDate, int appId)
    {
        private readonly HashSet<int> _activeSecondOffsets = [];

        public DateTimeOffset BucketStartUtc { get; } = bucketStartUtc;

        public string LocalDate { get; } = localDate;

        public int AppId { get; } = appId;

        public int CharCount { get; private set; }

        public int PeakCps { get; private set; }

        public void Register(DateTimeOffset capturedAtUtc, int realtimeCps)
        {
            CharCount += 1;
            PeakCps = Math.Max(PeakCps, realtimeCps);
            var offset = (int)(capturedAtUtc - BucketStartUtc).TotalSeconds;
            if (offset >= 0)
            {
                _activeSecondOffsets.Add(offset);
            }
        }

        public MinuteStatRecord ToRecord(DateTimeOffset updatedAtUtc)
        {
            return new MinuteStatRecord(
                BucketStartUtc,
                LocalDate,
                AppId,
                CharCount,
                CharCount / 60d,
                PeakCps,
                _activeSecondOffsets.Count,
                updatedAtUtc);
        }
    }
}

