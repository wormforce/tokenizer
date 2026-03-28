using Microsoft.Data.Sqlite;

namespace Tokenizer.Infrastructure.Storage;

public sealed class SqliteDatabase
{
    private readonly string _databasePath;

    public SqliteDatabase(string? databasePath = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            var baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Tokenizer");

            Directory.CreateDirectory(baseDirectory);
            _databasePath = Path.Combine(baseDirectory, "Tokenizer.db");
        }
        else
        {
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _databasePath = databasePath;
        }
    }

    public string DatabasePath => _databasePath;

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_databasePath};Cache=Shared;Mode=ReadWriteCreate;Pooling=False");
        connection.Open();
        return connection;
    }
}
