using Microsoft.Data.Sqlite;

namespace Tokenizer.Infrastructure.Storage;

internal static class SqliteSchemaInitializer
{
    public static async Task EnsureCreatedAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var sql = """
            CREATE TABLE IF NOT EXISTS AppProfile (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProcessName TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                FirstSeenAtUtc INTEGER NOT NULL,
                LastSeenAtUtc INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS MinuteStat (
                BucketStartUtc INTEGER NOT NULL,
                LocalDate TEXT NOT NULL,
                AppId INTEGER NOT NULL,
                CharCount INTEGER NOT NULL,
                AvgCps REAL NOT NULL,
                PeakCps INTEGER NOT NULL,
                ActiveSeconds INTEGER NOT NULL,
                UpdatedAtUtc INTEGER NOT NULL,
                PRIMARY KEY (BucketStartUtc, AppId),
                FOREIGN KEY (AppId) REFERENCES AppProfile(Id)
            );

            CREATE INDEX IF NOT EXISTS IX_MinuteStat_LocalDate ON MinuteStat(LocalDate);

            CREATE TABLE IF NOT EXISTS DailySummary (
                StatDate TEXT PRIMARY KEY,
                TotalChars INTEGER NOT NULL,
                PeakCps INTEGER NOT NULL,
                TopAppId INTEGER NULL,
                TopAppName TEXT NULL,
                UpdatedAtUtc INTEGER NOT NULL,
                FOREIGN KEY (TopAppId) REFERENCES AppProfile(Id)
            );

            CREATE TABLE IF NOT EXISTS AppSettings (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                AutostartEnabled INTEGER NOT NULL,
                FloatingBallEnabled INTEGER NOT NULL,
                FloatingEdge INTEGER NOT NULL,
                LaunchMinimized INTEGER NOT NULL,
                Paused INTEGER NOT NULL,
                FloatingOffsetX REAL NOT NULL,
                FloatingOffsetY REAL NOT NULL,
                FloatingBallOpacityPercent INTEGER NOT NULL,
                FloatingBallSize INTEGER NOT NULL
            );
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await EnsureSettingsColumnAsync(connection, "FloatingOffsetX", "REAL NOT NULL DEFAULT 180", cancellationToken);
        await EnsureSettingsColumnAsync(connection, "FloatingOffsetY", "REAL NOT NULL DEFAULT 180", cancellationToken);
        await EnsureSettingsColumnAsync(connection, "FloatingBallOpacityPercent", "INTEGER NOT NULL DEFAULT 84", cancellationToken);
        await EnsureSettingsColumnAsync(connection, "FloatingBallSize", "INTEGER NOT NULL DEFAULT 98", cancellationToken);

        await using var seed = connection.CreateCommand();
        seed.CommandText = """
            INSERT INTO AppSettings (Id, AutostartEnabled, FloatingBallEnabled, FloatingEdge, LaunchMinimized, Paused, FloatingOffsetX, FloatingOffsetY, FloatingBallOpacityPercent, FloatingBallSize)
            SELECT 1, 0, 1, 1, 0, 0, 180, 180, 84, 98
            WHERE NOT EXISTS (SELECT 1 FROM AppSettings WHERE Id = 1);
            """;
        await seed.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSettingsColumnAsync(
        SqliteConnection connection,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA table_info(AppSettings);";

        await using var reader = await pragma.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        await reader.CloseAsync();

        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE AppSettings ADD COLUMN {columnName} {columnDefinition};";
        await alter.ExecuteNonQueryAsync(cancellationToken);
    }
}

