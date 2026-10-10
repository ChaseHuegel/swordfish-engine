using Microsoft.Data.Sqlite;
using Swordfish.Library.IO;

namespace WaywardBeyond.Data.Sqlite;

/// <summary>A utility for managing sqlite databases with common parameters.</summary>
internal static class SqliteDatabase
{
    private const int BUSY_TIMEOUT_MS = 5000;

    /// <summary>Opens a sqlite database connection at the provided path.</summary>
    public static SqliteConnection Open(PathInfo path)
    {
        path.CreateDirectory();
        
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false,
        };

        var connection = new SqliteConnection(connectionString.ToString());
        connection.Open();

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = $"PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout={BUSY_TIMEOUT_MS};";
        command.ExecuteNonQuery();

        return connection;
    }

    /// <summary>Encodes a value for use as a <c>TEXT PRIMARY KEY</c>.</summary>
    public static string EncodeKey(ulong value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
