using Tokenizer.Core.Models;

namespace Tokenizer.Core.Interfaces;

public interface IStatsRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<int> UpsertAppProfileAsync(ForegroundAppInfo appInfo, CancellationToken cancellationToken = default);

    Task UpsertMinuteStatAsync(MinuteStatRecord record, CancellationToken cancellationToken = default);

    Task UpsertDailySummaryAsync(DailySummaryRecord record, CancellationToken cancellationToken = default);

    Task<DailySummaryRecord?> GetDailySummaryAsync(string localDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MinuteChartPoint>> GetMinuteChartAsync(string localDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppRankingItem>> GetAppRankingAsync(string localDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyTrendPoint>> GetDailyTrendAsync(int days, CancellationToken cancellationToken = default);
}

