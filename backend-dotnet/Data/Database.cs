using Microsoft.Data.Sqlite;

namespace DayLoop.Api.Data;

public static class Database
{
    public static string DbPath =>
    Environment.GetEnvironmentVariable("DAYLOOP_DB_PATH")
    ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "backend", "data", "dayloop.db");

    public static SqliteConnection CreateConnection()
    {
        var dir = Path.GetDirectoryName(DbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL";
        cmd.ExecuteNonQuery();

        cmd.CommandText = "PRAGMA foreign_keys=ON";
        cmd.ExecuteNonQuery();

        return conn;
    }

    public static void Initialize()
    {
        using var conn = CreateConnection();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS tasks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                date TEXT NOT NULL,
                title TEXT NOT NULL,
                start_time TEXT DEFAULT '',
                end_time TEXT DEFAULT '',
                planned_duration INTEGER DEFAULT 0,
                actual_duration INTEGER,
                actual_start TEXT,
                actual_end TEXT,
                status TEXT DEFAULT 'planned' CHECK(status IN ('planned','in_progress','completed','cancelled')),
                category TEXT DEFAULT '',
                priority INTEGER DEFAULT 2 CHECK(priority BETWEEN 1 AND 3),
                note TEXT DEFAULT '',
                is_recurring INTEGER DEFAULT 0,
                is_planned INTEGER DEFAULT 1,
                recurring_template_id INTEGER,
                achievement TEXT DEFAULT '',
                note_id INTEGER,
                tags TEXT DEFAULT '',
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime')),
                updated_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS daily_reviews (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                date TEXT NOT NULL UNIQUE,
                content TEXT DEFAULT '',
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime')),
                updated_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS recurring_templates (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                start_time TEXT DEFAULT '',
                end_time TEXT DEFAULT '',
                planned_duration INTEGER DEFAULT 0,
                category TEXT DEFAULT '',
                priority INTEGER DEFAULT 2,
                note TEXT DEFAULT '',
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS notes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                content TEXT DEFAULT '',
                category TEXT DEFAULT '',
                tags TEXT DEFAULT '',
                task_id INTEGER,
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime')),
                updated_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS note_categories (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS questions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                content TEXT DEFAULT '',
                answer TEXT DEFAULT '',
                answer_source TEXT DEFAULT 'self' CHECK(answer_source IN ('self','ai','web')),
                category TEXT DEFAULT '',
                tags TEXT DEFAULT '',
                task_id INTEGER,
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime')),
                updated_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS question_categories (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL UNIQUE,
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );
        """;
        cmd.ExecuteNonQuery();

        using var cmdQ = conn.CreateCommand();
        cmdQ.CommandText = """
            CREATE TABLE IF NOT EXISTS question_task_links (
                question_id INTEGER NOT NULL,
                task_id INTEGER NOT NULL,
                PRIMARY KEY (question_id, task_id)
            )
        """;
        cmdQ.ExecuteNonQuery();

        // Add user_id columns to existing tables (safe if already exist)
        AddColumnIfMissing(conn, "tasks", "user_id", "INTEGER DEFAULT 0");
        AddColumnIfMissing(conn, "daily_reviews", "user_id", "INTEGER DEFAULT 0");
        AddColumnIfMissing(conn, "recurring_templates", "user_id", "INTEGER DEFAULT 0");
        AddColumnIfMissing(conn, "recurring_templates", "recurrence_type", "TEXT DEFAULT 'daily'");
        AddColumnIfMissing(conn, "recurring_templates", "recurrence_days", "TEXT DEFAULT ''");
        AddColumnIfMissing(conn, "recurring_templates", "recurring_enabled", "INTEGER DEFAULT 1");
        AddColumnIfMissing(conn, "notes", "user_id", "INTEGER DEFAULT 0");
        AddColumnIfMissing(conn, "note_categories", "user_id", "INTEGER DEFAULT 0");
        AddColumnIfMissing(conn, "tasks", "sync_enabled", "INTEGER DEFAULT 1");
AddColumnIfMissing(conn, "recurring_templates", "sync_enabled", "INTEGER DEFAULT 1");

        // Junction table for note <-> task many-to-many
        using var cmd2 = conn.CreateCommand();
        cmd2.CommandText = """
            CREATE TABLE IF NOT EXISTS note_task_links (
                note_id INTEGER NOT NULL,
                task_id INTEGER NOT NULL,
                PRIMARY KEY (note_id, task_id)
            )
        """;
        cmd2.ExecuteNonQuery();

        using var cmd3 = conn.CreateCommand();
        cmd3.CommandText = """
            CREATE TABLE IF NOT EXISTS app_settings (
                key TEXT PRIMARY KEY,
                value TEXT DEFAULT ''
            )
        """;
        cmd3.ExecuteNonQuery();
    }

    private static void AddColumnIfMissing(SqliteConnection conn, string table, string column, string definition)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        bool found = false;
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                if (reader.GetString(1) == column)
                {
                    found = true;
                    break;
                }
            }
        }
        if (!found)
        {
            cmd.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            cmd.ExecuteNonQuery();
        }
    }
}
