namespace Tokenizer.Core.Models;

public sealed record AggregationResult(
    IReadOnlyCollection<MinuteStatRecord> PersistRecords,
    bool BucketRolledOver);

