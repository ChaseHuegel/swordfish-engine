using System.IO;
using Microsoft.Data.Sqlite;

namespace WaywardBeyond.Data;

/// <summary>
/// Opens SQLite databases for the save stores. Connections are not pooled so a disposed store releases
/// its file handle and the level directory can be removed on every platform.
/// </summary>
internal static class SqliteDatabase
{
    private const int BUSY_TIMEOUT_MS = 5000;

    public static SqliteConnection Open(string databasePath)
    {
        string? directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout={BUSY_TIMEOUT_MS};";
        command.ExecuteNonQuery();

        return connection;
    }
}
