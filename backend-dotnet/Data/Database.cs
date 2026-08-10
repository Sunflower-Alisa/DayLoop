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
        AddColumnIfMissing(conn, "tasks", "planned_days", "INTEGER DEFAULT 1");
        AddColumnIfMissing(conn, "recurring_templates", "planned_days", "INTEGER DEFAULT 1");
        AddColumnIfMissing(conn, "tasks", "overall_status", "TEXT DEFAULT 'pending'");

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

        // summaries table
        using var cmd4 = conn.CreateCommand();
        cmd4.CommandText = """
            CREATE TABLE IF NOT EXISTS summaries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                type TEXT NOT NULL CHECK(type IN ('weekly','monthly','quarterly','yearly')),
                period_key TEXT NOT NULL,
                content TEXT DEFAULT '',
                auto_summary TEXT DEFAULT '',
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime')),
                updated_at TEXT DEFAULT (datetime('now','localtime'))
            )
        """;
        cmd4.ExecuteNonQuery();

        using var cmd5 = conn.CreateCommand();
        cmd5.CommandText = """
            CREATE TABLE IF NOT EXISTS task_summaries (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                content TEXT DEFAULT '',
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime')),
                updated_at TEXT DEFAULT (datetime('now','localtime'))
            )
        """;
        cmd5.ExecuteNonQuery();

        AddColumnIfMissing(conn, "daily_reviews", "tags", "TEXT DEFAULT ''");

        // ===== English Learning tables =====
        using var eng = conn.CreateCommand();
        eng.CommandText = """
            CREATE TABLE IF NOT EXISTS word_books (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                level TEXT DEFAULT 'intermediate',
                description TEXT DEFAULT '',
                cover_color TEXT DEFAULT '#4f46e5',
                is_default INTEGER DEFAULT 0,
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS words (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                word TEXT NOT NULL,
                phonetic TEXT DEFAULT '',
                pos TEXT DEFAULT '',
                meaning TEXT DEFAULT '',
                example_en TEXT DEFAULT '',
                example_cn TEXT DEFAULT '',
                image_url TEXT DEFAULT '',
                audio_url TEXT DEFAULT '',
                book_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS word_progress (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                word_id INTEGER NOT NULL,
                status TEXT DEFAULT 'new',
                stage INTEGER DEFAULT 0,
                correct_streak INTEGER DEFAULT 0,
                wrong_count INTEGER DEFAULT 0,
                last_review_at TEXT DEFAULT '',
                next_review_at TEXT DEFAULT '',
                UNIQUE(user_id, word_id)
            );

            CREATE TABLE IF NOT EXISTS learning_logs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                date TEXT DEFAULT '',
                type TEXT DEFAULT 'new',
                word_id INTEGER,
                topic_id INTEGER,
                result TEXT DEFAULT '',
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS wrong_words (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                word_id INTEGER NOT NULL,
                created_at TEXT DEFAULT (datetime('now','localtime')),
                UNIQUE(user_id, word_id)
            );

            CREATE TABLE IF NOT EXISTS study_sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                date TEXT DEFAULT '',
                module TEXT DEFAULT '',
                start_time TEXT DEFAULT '',
                end_time TEXT DEFAULT '',
                duration_seconds INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS scenarios (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                category TEXT DEFAULT '',
                level INTEGER DEFAULT 1,
                icon TEXT DEFAULT '',
                description TEXT DEFAULT '',
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS scenario_lines (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                scenario_id INTEGER NOT NULL,
                ord INTEGER DEFAULT 0,
                speaker TEXT DEFAULT '',
                en_text TEXT DEFAULT '',
                cn_text TEXT DEFAULT '',
                audio_url TEXT DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS scenario_phrases (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                scenario_id INTEGER NOT NULL,
                phrase TEXT DEFAULT '',
                meaning TEXT DEFAULT '',
                example_en TEXT DEFAULT '',
                example_cn TEXT DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS scenario_quizzes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                scenario_id INTEGER NOT NULL,
                question_en TEXT DEFAULT '',
                question_cn TEXT DEFAULT '',
                options TEXT DEFAULT '',
                answer_index INTEGER DEFAULT 0,
                explanation TEXT DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS scenario_progress (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                scenario_id INTEGER NOT NULL,
                mastered INTEGER DEFAULT 0,
                updated_at TEXT DEFAULT (datetime('now','localtime')),
                UNIQUE(user_id, scenario_id)
            );

            CREATE TABLE IF NOT EXISTS speaking_topics (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                category TEXT DEFAULT 'daily',
                level TEXT DEFAULT 'beginner',
                lines TEXT DEFAULT '',
                source_type TEXT DEFAULT 'topic',
                source_id INTEGER DEFAULT 0,
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS speaking_records (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id INTEGER NOT NULL,
                topic_id INTEGER NOT NULL,
                line_index INTEGER DEFAULT 0,
                audio_url TEXT DEFAULT '',
                accuracy INTEGER DEFAULT 0,
                fluency INTEGER DEFAULT 0,
                completeness INTEGER DEFAULT 0,
                overall INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS video_clips (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                source TEXT DEFAULT '',
                cover_url TEXT DEFAULT '',
                path TEXT DEFAULT '',
                duration INTEGER DEFAULT 0,
                level TEXT DEFAULT 'medium',
                tags TEXT DEFAULT '',
                description TEXT DEFAULT '',
                user_id INTEGER DEFAULT 0,
                created_at TEXT DEFAULT (datetime('now','localtime'))
            );

            CREATE TABLE IF NOT EXISTS clip_lines (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                clip_id INTEGER NOT NULL,
                ord INTEGER DEFAULT 0,
                speaker TEXT DEFAULT '',
                en_text TEXT DEFAULT '',
                cn_text TEXT DEFAULT '',
                start_time REAL DEFAULT 0,
                end_time REAL DEFAULT 0
            );
        """;
        eng.ExecuteNonQuery();
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
