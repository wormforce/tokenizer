using Microsoft.Data.Sqlite;
using Tokenizer.Core.Interfaces;
using Tokenizer.Core.Models;

namespace Tokenizer.Infrastructure.Storage;

public sealed class SqliteSettingsRepository(SqliteDatabase database) : ISettingsRepository
{
    public async Task<AppSettingsModel> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await SqliteSchemaInitializer.EnsureCreatedAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT AutostartEnabled, FloatingBallEnabled, FloatingEdge, LaunchMinimized, Paused, FloatingOffsetX, FloatingOffsetY, FloatingBallOpacityPercent, FloatingBallSize
            FROM AppSettings
            WHERE Id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new AppSettingsModel();
        }

        return new AppSettingsModel
        {
            AutostartEnabled = reader.GetInt32(0) == 1,
            FloatingBallEnabled = reader.GetInt32(1) == 1,
            FloatingEdge = (FloatingEdge)reader.GetInt32(2),
            LaunchMinimized = reader.GetInt32(3) == 1,
            Paused = reader.GetInt32(4) == 1,
            FloatingOffsetX = reader.GetDouble(5),
            FloatingOffsetY = reader.GetDouble(6),
            FloatingBallOpacityPercent = reader.GetInt32(7),
            FloatingBallSize = reader.GetInt32(8)
        };
    }

    public async Task SaveAsync(AppSettingsModel settings, CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await SqliteSchemaInitializer.EnsureCreatedAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE AppSettings
            SET AutostartEnabled = $autostart,
                FloatingBallEnabled = $floatingEnabled,
                FloatingEdge = $floatingEdge,
                LaunchMinimized = $launchMinimized,
                Paused = $paused,
                FloatingOffsetX = $floatingOffsetX,
                FloatingOffsetY = $floatingOffsetY,
                FloatingBallOpacityPercent = $floatingBallOpacityPercent,
                FloatingBallSize = $floatingBallSize
            WHERE Id = 1;
            """;
        command.Parameters.AddWithValue("$autostart", settings.AutostartEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$floatingEnabled", settings.FloatingBallEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$floatingEdge", (int)settings.FloatingEdge);
        command.Parameters.AddWithValue("$launchMinimized", settings.LaunchMinimized ? 1 : 0);
        command.Parameters.AddWithValue("$paused", settings.Paused ? 1 : 0);
        command.Parameters.AddWithValue("$floatingOffsetX", settings.FloatingOffsetX);
        command.Parameters.AddWithValue("$floatingOffsetY", settings.FloatingOffsetY);
        command.Parameters.AddWithValue("$floatingBallOpacityPercent", settings.FloatingBallOpacityPercent);
        command.Parameters.AddWithValue("$floatingBallSize", settings.FloatingBallSize);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

