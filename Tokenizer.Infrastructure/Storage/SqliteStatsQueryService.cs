using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Models;

namespace Tokenizer.Infrastructure.Storage;

public sealed class SqliteStatsQueryService(IStatsRepository repository) : IStatsQueryService
{
    public Task<DailySummaryRecord?> GetTodaySummaryAsync(CancellationToken cancellationToken = default)
        => repository.GetDailySummaryAsync(DateTime.Now.ToString("yyyy-MM-dd"), cancellationToken);

    public Task<IReadOnlyList<MinuteChartPoint>> GetMinuteChartAsync(string localDate, CancellationToken cancellationToken = default)
        => repository.GetMinuteChartAsync(localDate, cancellationToken);

    public Task<IReadOnlyList<AppRankingItem>> GetAppRankingAsync(string localDate, CancellationToken cancellationToken = default)
        => repository.GetAppRankingAsync(localDate, cancellationToken);

    public Task<IReadOnlyList<DailyTrendPoint>> GetDailyTrendAsync(int days, CancellationToken cancellationToken = default)
        => repository.GetDailyTrendAsync(days, cancellationToken);
}

