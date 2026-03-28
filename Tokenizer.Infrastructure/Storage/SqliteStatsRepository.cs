using Microsoft.Data.Sqlite;
using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Models;

namespace Tokenizer.Infrastructure.Storage;

public sealed class SqliteStatsRepository(SqliteDatabase database) : IStatsRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await SqliteSchemaInitializer.EnsureCreatedAsync(connection, cancellationToken);
    }

    public async Task<int> UpsertAppProfileAsync(ForegroundAppInfo appInfo, CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await SqliteSchemaInitializer.EnsureCreatedAsync(connection, cancellationToken);

        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AppProfile (ProcessName, DisplayName, FirstSeenAtUtc, LastSeenAtUtc)
            VALUES ($processName, $displayName, $firstSeen, $lastSeen)
            ON CONFLICT(ProcessName) DO UPDATE SET
                DisplayName = excluded.DisplayName,
                LastSeenAtUtc = excluded.LastSeenAtUtc;
            """;
        command.Parameters.AddWithValue("$processName", appInfo.ProcessName);
        command.Parameters.AddWithValue("$displayName", appInfo.DisplayName);
        command.Parameters.AddWithValue("$firstSeen", nowUnix);
        command.Parameters.AddWithValue("$lastSeen", nowUnix);
        await command.ExecuteNonQueryAsync(cancellationToken);

        await using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT Id FROM AppProfile WHERE ProcessName = $processName;";
        idCommand.Parameters.AddWithValue("$processName", appInfo.ProcessName);
        var id = await idCommand.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(id);
    }

    public async Task UpsertMinuteStatAsync(MinuteStatRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await SqliteSchemaInitializer.EnsureCreatedAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO MinuteStat (BucketStartUtc, LocalDate, AppId, CharCount, AvgCps, PeakCps, ActiveSeconds, UpdatedAtUtc)
            VALUES ($bucketStartUtc, $localDate, $appId, $charCount, $avgCps, $peakCps, $activeSeconds, $updatedAtUtc)
            ON CONFLICT(BucketStartUtc, AppId) DO UPDATE SET
                LocalDate = excluded.LocalDate,
                CharCount = excluded.CharCount,
                AvgCps = excluded.AvgCps,
                PeakCps = excluded.PeakCps,
                ActiveSeconds = excluded.ActiveSeconds,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$bucketStartUtc", record.BucketStartUtc.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$localDate", record.LocalDate);
        command.Parameters.AddWithValue("$appId", record.AppId);
        command.Parameters.AddWithValue("$charCount", record.CharCount);
        command.Parameters.AddWithValue("$avgCps", record.AvgCps);
        command.Parameters.AddWithValue("$peakCps", record.PeakCps);
        command.Parameters.AddWithValue("$activeSeconds", record.ActiveSeconds);
        command.Parameters.AddWithValue("$updatedAtUtc", record.UpdatedAtUtc.ToUnixTimeSeconds());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpsertDailySummaryAsync(DailySummaryRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await SqliteSchemaInitializer.EnsureCreatedAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO DailySummary (StatDate, TotalChars, PeakCps, TopAppId, TopAppName, UpdatedAtUtc)
            VALUES ($statDate, $totalChars, $peakCps, $topAppId, $topAppName, $updatedAtUtc)
            ON CONFLICT(StatDate) DO UPDATE SET
                TotalChars = excluded.TotalChars,
                PeakCps = excluded.PeakCps,
                TopAppId = excluded.TopAppId,
                TopAppName = excluded.TopAppName,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;
        command.Parameters.AddWithValue("$statDate", record.StatDate);
        command.Parameters.AddWithValue("$totalChars", record.TotalChars);
        command.Parameters.AddWithValue("$peakCps", record.PeakCps);
        command.Parameters.AddWithValue("$topAppId", (object?)record.TopAppId ?? DBNull.Value);
        command.Parameters.AddWithValue("$topAppName", (object?)record.TopAppName ?? DBNull.Value);
        command.Parameters.AddWithValue("$updatedAtUtc", record.UpdatedAtUtc.ToUnixTimeSeconds());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<DailySummaryRecord?> GetDailySummaryAsync(string localDate, CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await SqliteSchemaInitializer.EnsureCreatedAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT StatDate, TotalChars, PeakCps, TopAppId, TopAppName, UpdatedAtUtc
            FROM DailySummary
            WHERE StatDate = $statDate;
            """;
        command.Parameters.AddWithValue("$statDate", localDate);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new DailySummaryRecord(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5)));
    }

    public async Task<IReadOnlyList<MinuteChartPoint>> GetMinuteChartAsync(string localDate, CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await SqliteSchemaInitializer.EnsureCreatedAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT BucketStartUtc, SUM(CharCount) AS CharCount, SUM(CharCount) / 60.0 AS AvgCps, MAX(PeakCps) AS PeakCps
            FROM MinuteStat
            WHERE LocalDate = $localDate
            GROUP BY BucketStartUtc
            ORDER BY BucketStartUtc;
            """;
        command.Parameters.AddWithValue("$localDate", localDate);

        var items = new List<MinuteChartPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new MinuteChartPoint(
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(0)),
                reader.GetInt32(1),
                reader.GetDouble(2),
                reader.GetInt32(3)));
        }

        return items;
    }

    public async Task<IReadOnlyList<AppRankingItem>> GetAppRankingAsync(string localDate, CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await SqliteSchemaInitializer.EnsureCreatedAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.AppId, p.DisplayName, p.ProcessName, SUM(s.CharCount) AS CharCount, MAX(s.PeakCps) AS PeakCps, SUM(s.ActiveSeconds) AS ActiveSeconds
            FROM MinuteStat s
            INNER JOIN AppProfile p ON p.Id = s.AppId
            WHERE s.LocalDate = $localDate
            GROUP BY s.AppId, p.DisplayName, p.ProcessName
            ORDER BY CharCount DESC, PeakCps DESC;
            """;
        command.Parameters.AddWithValue("$localDate", localDate);

        var items = new List<AppRankingItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new AppRankingItem(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5)));
        }

        return items;
    }

    public async Task<IReadOnlyList<DailyTrendPoint>> GetDailyTrendAsync(int days, CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await SqliteSchemaInitializer.EnsureCreatedAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT StatDate, TotalChars, PeakCps
            FROM DailySummary
            ORDER BY StatDate DESC
            LIMIT $days;
            """;
        command.Parameters.AddWithValue("$days", days);

        var items = new List<DailyTrendPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new DailyTrendPoint(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2)));
        }

        items.Reverse();
        return items;
    }
}

