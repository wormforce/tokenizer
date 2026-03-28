using Tokenizer.Core.Models;
using Tokenizer.Infrastructure.Storage;

namespace Tokenizer.Tests;

public sealed class SqliteRepositoriesTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "TokenizerTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StatsRepository_CanRoundTripDailyStatsQueries()
    {
        var database = new SqliteDatabase(Path.Combine(_tempDirectory, "stats.db"));
        var repository = new SqliteStatsRepository(database);

        await repository.InitializeAsync();

        var appId = await repository.UpsertAppProfileAsync(new ForegroundAppInfo(12u, "notepad", "Notepad"));
        await repository.UpsertMinuteStatAsync(new MinuteStatRecord(
            new DateTimeOffset(2026, 03, 27, 12, 00, 00, TimeSpan.Zero),
            "2026-03-27",
            appId,
            120,
            2,
            6,
            23,
            DateTimeOffset.UtcNow));
        await repository.UpsertDailySummaryAsync(new DailySummaryRecord(
            "2026-03-27",
            120,
            6,
            appId,
            "Notepad",
            DateTimeOffset.UtcNow));

        var summary = await repository.GetDailySummaryAsync("2026-03-27");
        var chart = await repository.GetMinuteChartAsync("2026-03-27");
        var ranking = await repository.GetAppRankingAsync("2026-03-27");
        var trend = await repository.GetDailyTrendAsync(7);

        Assert.NotNull(summary);
        Assert.Equal(120, summary!.TotalChars);
        Assert.Single(chart);
        Assert.Single(ranking);
        Assert.Single(trend);
    }

    [Fact]
    public async Task SettingsRepository_CanPersistSettings()
    {
        var database = new SqliteDatabase(Path.Combine(_tempDirectory, "settings.db"));
        var repository = new SqliteSettingsRepository(database);

        var settings = await repository.GetAsync();
        settings.AutostartEnabled = true;
        settings.FloatingBallEnabled = false;
        settings.LaunchMinimized = true;
        settings.Paused = true;
        settings.FloatingEdge = FloatingEdge.Left;
        settings.FloatingOffsetY = 240;
        settings.FloatingBallOpacityPercent = 72;
        settings.FloatingBallSize = 96;

        await repository.SaveAsync(settings);
        var reloaded = await repository.GetAsync();

        Assert.True(reloaded.AutostartEnabled);
        Assert.False(reloaded.FloatingBallEnabled);
        Assert.True(reloaded.LaunchMinimized);
        Assert.True(reloaded.Paused);
        Assert.Equal(FloatingEdge.Left, reloaded.FloatingEdge);
        Assert.Equal(240, reloaded.FloatingOffsetY);
        Assert.Equal(72, reloaded.FloatingBallOpacityPercent);
        Assert.Equal(96, reloaded.FloatingBallSize);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}

