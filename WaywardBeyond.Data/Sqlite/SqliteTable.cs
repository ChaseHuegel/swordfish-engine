using System.Threading;
using Microsoft.Data.Sqlite;
using Swordfish.Library.IO;

namespace WaywardBeyond.Data.Sqlite;

/// <summary>
/// A thread-safe wrapper for creating and interacting with a sqlite database table.
/// The table is guaranteed to be created lazily if it doesn't exist.
/// </summary>
internal sealed class SqliteTable(in PathInfo dbPath, in string createCommandText)
{
    private readonly PathInfo _dbPath = dbPath;
    private readonly string _createCommandText = createCommandText;
    private readonly Lock _createLock = new();
    private bool _created;

    /// <summary>Creates the table immediately, if it doesn't already exist.</summary>
    /// <remarks>This is useful if you want to create the table deterministically instead of lazily.</remarks>
    public void Create()
    {
        if (_created)
        {
            return;
        }
        
        using SqliteConnection connection = SqliteDatabase.Open(_dbPath);
        EnsureCreated(connection);
    }

    /// <inheritdoc cref="SqliteConnection.CreateCommand"/>
    public SqliteCommand CreateCommand()
    {
        using SqliteConnection connection = SqliteDatabase.Open(_dbPath);
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
        command.CommandText = _createCommandText;
        command.ExecuteNonQuery();

        _created = true;
    }
}