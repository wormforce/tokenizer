using Tokenizer.Core.Models;

namespace Tokenizer.Core.Interfaces;

public interface IStatsQueryService
{
    Task<DailySummaryRecord?> GetTodaySummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MinuteChartPoint>> GetMinuteChartAsync(string localDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppRankingItem>> GetAppRankingAsync(string localDate, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyTrendPoint>> GetDailyTrendAsync(int days, CancellationToken cancellationToken = default);
}

