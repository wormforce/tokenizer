using Tokenizer.Core.Models;

namespace Tokenizer.Core.Interfaces;

public interface IMinuteAggregationService
{
    AggregationResult Register(DateTimeOffset capturedAtUtc, string localDate, int appId, int realtimeCps);

    IReadOnlyCollection<MinuteStatRecord> Flush(DateTimeOffset flushedAtUtc);

    void Reset();
}

