using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Swordfish.Library.IO;

namespace WaywardBeyond.Data.Sqlite;

/// <summary>A wrapper type for creating and interacting with sqlite databases.</summary>
internal class SqliteDatabase(PathInfo path, string? createCommand = null)
{
    private const string DEFAULT_CREATE_COMMAND = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;";
    
    private readonly string _createCommand = createCommand ?? DEFAULT_CREATE_COMMAND;
    
    /// <summary>Encodes a value for use as a <c>TEXT PRIMARY KEY</c>.</summary>
    public static string EncodeKey(ulong value)
    {
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Opens a sqlite database connection at the provided path.</summary>
    /// <exception cref="SqliteException">A SQLite error occurs while opening the connection.</exception>
    public SqliteConnection Open()
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
        command.CommandText = _createCommand;
        command.ExecuteNonQuery();
        return connection;
    }

    /// <inheritdoc cref="SqliteConnection.BeginTransaction(bool)"/>
    public Transaction BeginTransaction(bool deferred)
    {
        SqliteConnection connection = Open();
        SqliteTransaction transaction = connection.BeginTransaction(deferred);
        return new Transaction(connection, transaction);
    }

    public readonly record struct Transaction(SqliteConnection Connection, SqliteTransaction Value)
        : IDisposable, IAsyncDisposable
    {
        public void Dispose()
        {
            Value.Dispose();
            Connection.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            await Value.DisposeAsync();
            await Connection.DisposeAsync();
        }
    };
}
