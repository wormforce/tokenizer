using Tokenizer.Core.Models;

namespace Tokenizer.Core.Statistics;

public sealed class DailySummaryAccumulator
{
    private string? _currentLocalDate;
    private readonly Dictionary<int, int> _appTotals = [];
    private readonly Dictionary<int, string> _appNames = [];

    public int TodayCharCount { get; private set; }

    public int TodayPeakCps { get; private set; }

    public string? TopAppDisplayName { get; private set; }

    public int? TopAppId { get; private set; }

    public void Seed(
        string localDate,
        int totalChars,
        int peakCps,
        IEnumerable<AppRankingItem> rankings,
        string? topAppDisplayName)
    {
        Reset(localDate);

        TodayCharCount = totalChars;
        TodayPeakCps = peakCps;
        TopAppDisplayName = topAppDisplayName;

        foreach (var ranking in rankings)
        {
            _appNames[ranking.AppId] = ranking.DisplayName;
            _appTotals[ranking.AppId] = ranking.CharCount;
        }

        if (_appTotals.Count > 0)
        {
            var top = _appTotals.MaxBy(static item => item.Value);
            TopAppId = top.Key;
            TopAppDisplayName = _appNames[top.Key];
        }
    }

    public void Register(string localDate, int appId, string appDisplayName, int realtimeCps)
    {
        if (_currentLocalDate is not null && !string.Equals(_currentLocalDate, localDate, StringComparison.Ordinal))
        {
            Reset(localDate);
        }

        _currentLocalDate ??= localDate;

        TodayCharCount += 1;
        TodayPeakCps = Math.Max(TodayPeakCps, realtimeCps);
        _appNames[appId] = appDisplayName;
        _appTotals[appId] = _appTotals.GetValueOrDefault(appId) + 1;

        var top = _appTotals.MaxBy(static item => item.Value);
        TopAppId = top.Key;
        TopAppDisplayName = _appNames[top.Key];
    }

    public DailySummaryRecord Build(DateTimeOffset updatedAtUtc)
    {
        return new DailySummaryRecord(
            _currentLocalDate ?? updatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd"),
            TodayCharCount,
            TodayPeakCps,
            TopAppId,
            TopAppDisplayName,
            updatedAtUtc);
    }

    public void Reset(string? localDate = null)
    {
        _currentLocalDate = localDate;
        _appTotals.Clear();
        _appNames.Clear();
        TodayCharCount = 0;
        TodayPeakCps = 0;
        TopAppId = null;
        TopAppDisplayName = null;
    }
}

