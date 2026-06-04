using Microsoft.Data.Sqlite;
using Rolan.Models;

namespace Rolan.Services;

public class DataService : IDataService
{
    private readonly string _connectionString;

    public DataService()
    {
        var dbDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Rolan");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "data.db");
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Groups (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                IconData BLOB
            );

            CREATE TABLE IF NOT EXISTS ShortcutItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GroupId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                TargetPath TEXT NOT NULL,
                Arguments TEXT,
                WorkingDirectory TEXT,
                IconData BLOB,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                Type INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (GroupId) REFERENCES Groups(Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_Items_GroupId ON ShortcutItems(GroupId);
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task<List<ShortcutGroup>> LoadAllAsync()
    {
        var groups = new List<ShortcutGroup>();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        // 加载分组
        using var groupCmd = conn.CreateCommand();
        groupCmd.CommandText = "SELECT Id, Name, SortOrder, IconData FROM Groups ORDER BY SortOrder";
        using var groupReader = await groupCmd.ExecuteReaderAsync();
        while (await groupReader.ReadAsync())
        {
            groups.Add(new ShortcutGroup
            {
                Id = groupReader.GetInt32(0),
                Name = groupReader.GetString(1),
                Order = groupReader.GetInt32(2),
                IconData = groupReader.IsDBNull(3) ? null : (byte[])groupReader[3]
            });
        }

        // 加载快捷方式
        foreach (var group in groups)
        {
            using var itemCmd = conn.CreateCommand();
            itemCmd.CommandText = """
                SELECT Id, GroupId, Name, TargetPath, Arguments, WorkingDirectory,
                       IconData, SortOrder, Type, CreatedAt
                FROM ShortcutItems WHERE GroupId = @gid ORDER BY SortOrder
                """;
            itemCmd.Parameters.AddWithValue("@gid", group.Id);
            using var itemReader = await itemCmd.ExecuteReaderAsync();
            while (await itemReader.ReadAsync())
            {
                group.Items.Add(new ShortcutItem
                {
                    Id = itemReader.GetInt32(0),
                    GroupId = itemReader.GetInt32(1),
                    Name = itemReader.GetString(2),
                    TargetPath = itemReader.GetString(3),
                    Arguments = itemReader.IsDBNull(4) ? null : itemReader.GetString(4),
                    WorkingDirectory = itemReader.IsDBNull(5) ? null : itemReader.GetString(5),
                    IconData = itemReader.IsDBNull(6) ? null : (byte[])itemReader[6],
                    Order = itemReader.GetInt32(7),
                    Type = (ShortcutType)itemReader.GetInt32(8),
                    CreatedAt = DateTime.Parse(itemReader.GetString(9))
                });
            }
        }

        return groups;
    }

    public async Task SaveGroupAsync(ShortcutGroup group)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        if (group.Id == 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Groups (Name, SortOrder, IconData) VALUES (@n, @o, @i); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@n", group.Name);
            cmd.Parameters.AddWithValue("@o", group.Order);
            cmd.Parameters.AddWithValue("@i", (object?)group.IconData ?? DBNull.Value);
            group.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        else
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Groups SET Name = @n, SortOrder = @o, IconData = @i WHERE Id = @id";
            cmd.Parameters.AddWithValue("@n", group.Name);
            cmd.Parameters.AddWithValue("@o", group.Order);
            cmd.Parameters.AddWithValue("@i", (object?)group.IconData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", group.Id);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteGroupAsync(int groupId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Groups WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", groupId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SaveItemAsync(ShortcutItem item)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        if (item.Id == 0)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO ShortcutItems (GroupId, Name, TargetPath, Arguments, WorkingDirectory,
                    IconData, SortOrder, Type, CreatedAt)
                VALUES (@gid, @n, @t, @a, @wd, @i, @o, @type, @ca);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("@gid", item.GroupId);
            cmd.Parameters.AddWithValue("@n", item.Name);
            cmd.Parameters.AddWithValue("@t", item.TargetPath);
            cmd.Parameters.AddWithValue("@a", (object?)item.Arguments ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@wd", (object?)item.WorkingDirectory ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@i", (object?)item.IconData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@o", item.Order);
            cmd.Parameters.AddWithValue("@type", (int)item.Type);
            cmd.Parameters.AddWithValue("@ca", item.CreatedAt.ToString("O"));
            item.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        else
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                UPDATE ShortcutItems SET GroupId = @gid, Name = @n, TargetPath = @t,
                    Arguments = @a, WorkingDirectory = @wd, IconData = @i,
                    SortOrder = @o, Type = @type
                WHERE Id = @id
                """;
            cmd.Parameters.AddWithValue("@gid", item.GroupId);
            cmd.Parameters.AddWithValue("@n", item.Name);
            cmd.Parameters.AddWithValue("@t", item.TargetPath);
            cmd.Parameters.AddWithValue("@a", (object?)item.Arguments ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@wd", (object?)item.WorkingDirectory ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@i", (object?)item.IconData ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@o", item.Order);
            cmd.Parameters.AddWithValue("@type", (int)item.Type);
            cmd.Parameters.AddWithValue("@id", item.Id);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteItemAsync(int itemId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM ShortcutItems WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", itemId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReorderGroupAsync(int groupId, int newOrder)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Groups SET SortOrder = @o WHERE Id = @id";
        cmd.Parameters.AddWithValue("@o", newOrder);
        cmd.Parameters.AddWithValue("@id", groupId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ReorderItemAsync(int itemId, int newOrder)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE ShortcutItems SET SortOrder = @o WHERE Id = @id";
        cmd.Parameters.AddWithValue("@o", newOrder);
        cmd.Parameters.AddWithValue("@id", itemId);
        await cmd.ExecuteNonQueryAsync();
    }
}
