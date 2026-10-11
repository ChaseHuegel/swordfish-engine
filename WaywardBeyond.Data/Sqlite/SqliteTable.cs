using System.Threading;
using Microsoft.Data.Sqlite;

namespace WaywardBeyond.Data.Sqlite;

/// <summary>
/// A wrapper for creating and interacting with a sqlite database table.
/// The table is guaranteed to be created lazily and thread-safe if it doesn't exist.
/// </summary>
internal sealed class SqliteTable(string createCommandText)
{
    private readonly Lock _createLock = new();
    private bool _created;

    /// <inheritdoc cref="SqliteConnection.CreateCommand"/>
    public SqliteCommand CreateCommand(SqliteConnection connection)
    {
        if (!_created)
        {
            EnsureCreated(connection);
        }
        
        return connection.CreateCommand();
    }
    
    private void EnsureCreated(SqliteConnection connection)
    {
        using Lock.Scope _ = _createLock.EnterScope();
        if (_created)
        {
            return;
        }

        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = createCommandText;
        command.ExecuteNonQuery();

        _created = true;
    }
}