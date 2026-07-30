using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Models;
using System.Text.RegularExpressions;

namespace DayLoop.Api.Services;

public static class ObsidianSyncService
{
    private static string? GetVaultPath()
    {
        var env = Environment.GetEnvironmentVariable("OBSIDIAN_VAULT_PATH");
        if (!string.IsNullOrEmpty(env)) return env;

        try
        {
            using var conn = Database.CreateConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT value FROM app_settings WHERE key = 'obsidian_vault_path'";
            var result = cmd.ExecuteScalar();
            return result as string;
        }
        catch
        {
            return null;
        }
    }

    private static string Slugify(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var slug = string.Join("", text.Where(c => !invalid.Contains(c)));
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');
        return slug.Length > 100 ? slug[..100] : slug;
    }

    private static string PadNum(int n, int total)
    {
        var digits = total.ToString().Length;
        return n.ToString().PadLeft(digits, '0');
    }

    private static string? ExtractBookName(string title)
    {
        var m = Regex.Match(title, @"《([^》]+)》");
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    public static int SyncNotes()
    {
        var vaultPath = GetVaultPath();
        if (string.IsNullOrEmpty(vaultPath)) return 0;

        var dir = Path.Combine(vaultPath, "DayLoop", "备忘录");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM notes ORDER BY created_at ASC, id ASC";
        var allNotes = new List<Note>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                allNotes.Add(new Note
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    Title = reader.IsDBNull(reader.GetOrdinal("title")) ? "" : reader.GetString(reader.GetOrdinal("title")),
                    Content = reader.IsDBNull(reader.GetOrdinal("content")) ? "" : reader.GetString(reader.GetOrdinal("content")),
                    Category = reader.IsDBNull(reader.GetOrdinal("category")) ? "" : reader.GetString(reader.GetOrdinal("category")),
                    Tags = reader.IsDBNull(reader.GetOrdinal("tags")) ? "" : reader.GetString(reader.GetOrdinal("tags")),
                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at")) ? "" : reader.GetString(reader.GetOrdinal("created_at")),
                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? "" : reader.GetString(reader.GetOrdinal("updated_at")),
                });
            }
        }

        if (allNotes.Count == 0) return 0;
        var total = allNotes.Count;

        var bookGroups = new Dictionary<string, List<Note>>();
        var standaloneNotes = new List<Note>();

        foreach (var note in allNotes)
        {
            var book = ExtractBookName(note.Title);
            if (book != null)
            {
                if (!bookGroups.ContainsKey(book)) bookGroups[book] = new List<Note>();
                bookGroups[book].Add(note);
            }
            else
            {
                standaloneNotes.Add(note);
            }
        }

        int idx = 0;

        // Standalone notes
        foreach (var note in standaloneNotes)
        {
            idx++;
            var slug = Slugify(note.Title ?? $"note-{note.Id}");
            var filePath = Path.Combine(dir, $"{PadNum(idx, total)}-{slug}.md");
            var tags = (note.Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var frontmatter = new List<string> { "---" };
            frontmatter.Add($"created: {note.CreatedAt}");
            frontmatter.Add($"updated: {note.UpdatedAt}");
            if (!string.IsNullOrEmpty(note.Category)) frontmatter.Add($"category: {note.Category}");
            if (tags.Length > 0) frontmatter.Add($"tags: [{string.Join(", ", tags)}]");
            frontmatter.Add("source: DayLoop");
            frontmatter.Add("---");
            frontmatter.Add("");

            WriteFile(filePath, string.Join("\n", frontmatter) + (note.Content ?? ""), vaultPath);
        }

        // Book group notes
        foreach (var kv in bookGroups)
        {
            idx++;
            var book = kv.Key;
            var notes = kv.Value;
            var slug = Slugify($"读书笔记-{book}");
            var filePath = Path.Combine(dir, $"{PadNum(idx, total)}-{slug}.md");

            var entries = notes.Select(n =>
            {
                var date = (n.CreatedAt ?? "").Length >= 10 ? n.CreatedAt[..10] : "";
                return $"## {date}\n\n{n.Content ?? ""}";
            });

            var allTags = notes
                .SelectMany(n => (n.Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct()
                .ToList();

            var frontmatter = new List<string> { "---" };
            frontmatter.Add($"book: 《{book}》");
            if (allTags.Count > 0) frontmatter.Add($"tags: [{string.Join(", ", allTags)}]");
            frontmatter.Add("source: DayLoop");
            frontmatter.Add("type: book-notes");
            frontmatter.Add("---");
            frontmatter.Add("");

            var body = $"# 《{book}》读书笔记\n\n{string.Join("\n\n---\n\n", entries)}";
            WriteFile(filePath, string.Join("\n", frontmatter) + body, vaultPath);
        }

        return total;
    }

    public static int SyncReviews()
    {
        var vaultPath = GetVaultPath();
        if (string.IsNullOrEmpty(vaultPath)) return 0;

        var dir = Path.Combine(vaultPath, "DayLoop", "每日复盘");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM daily_reviews ORDER BY date ASC";
        var count = 0;
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                var review = new DailyReview
                {
                    Date = reader.GetString(reader.GetOrdinal("date")),
                    Content = reader.IsDBNull(reader.GetOrdinal("content")) ? "" : reader.GetString(reader.GetOrdinal("content")),
                    CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at")) ? "" : reader.GetString(reader.GetOrdinal("created_at")),
                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? "" : reader.GetString(reader.GetOrdinal("updated_at")),
                };

                var filePath = Path.Combine(dir, $"{review.Date}-每日复盘.md");
                var frontmatter = new List<string> { "---" };
                frontmatter.Add($"date: {review.Date}");
                frontmatter.Add($"created: {review.CreatedAt}");
                frontmatter.Add($"updated: {review.UpdatedAt}");
                frontmatter.Add("type: daily-review");
                frontmatter.Add("source: DayLoop");
                frontmatter.Add("---");
                frontmatter.Add("");

                WriteFile(filePath, string.Join("\n", frontmatter) + (review.Content ?? ""), vaultPath);
                count++;
            }
        }
        return count;
    }

    public static int SyncAchievements()
    {
        var vaultPath = GetVaultPath();
        if (string.IsNullOrEmpty(vaultPath)) return 0;

        var dir = Path.Combine(vaultPath, "DayLoop", "每日成果");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);

        using var conn = Database.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM tasks WHERE achievement != '' AND achievement IS NOT NULL AND title != '今日复盘' AND sync_enabled != 0 ORDER BY date ASC, id ASC";
        var allTasks = new List<TaskItem>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
            {
                allTasks.Add(new TaskItem
                {
                    Id = reader.GetInt64(reader.GetOrdinal("id")),
                    Date = reader.IsDBNull(reader.GetOrdinal("date")) ? "" : reader.GetString(reader.GetOrdinal("date")),
                    Title = reader.IsDBNull(reader.GetOrdinal("title")) ? "" : reader.GetString(reader.GetOrdinal("title")),
                    StartTime = reader.IsDBNull(reader.GetOrdinal("start_time")) ? "" : reader.GetString(reader.GetOrdinal("start_time")),
                    EndTime = reader.IsDBNull(reader.GetOrdinal("end_time")) ? "" : reader.GetString(reader.GetOrdinal("end_time")),
                    PlannedDuration = reader.IsDBNull(reader.GetOrdinal("planned_duration")) ? 0 : reader.GetInt32(reader.GetOrdinal("planned_duration")),
                    Category = reader.IsDBNull(reader.GetOrdinal("category")) ? "" : reader.GetString(reader.GetOrdinal("category")),
                    Priority = reader.IsDBNull(reader.GetOrdinal("priority")) ? 2 : reader.GetInt32(reader.GetOrdinal("priority")),
                    Note = reader.IsDBNull(reader.GetOrdinal("note")) ? "" : reader.GetString(reader.GetOrdinal("note")),
                    Achievement = reader.IsDBNull(reader.GetOrdinal("achievement")) ? "" : reader.GetString(reader.GetOrdinal("achievement")),
                    Status = reader.IsDBNull(reader.GetOrdinal("status")) ? "planned" : reader.GetString(reader.GetOrdinal("status")),
                    Tags = reader.IsDBNull(reader.GetOrdinal("tags")) ? "" : reader.GetString(reader.GetOrdinal("tags")),
                });
            }
        }

        if (allTasks.Count == 0) return 0;

        var bookGroups = new Dictionary<string, List<TaskItem>>();
        var standalone = new List<TaskItem>();

        foreach (var task in allTasks)
        {
            var book = ExtractBookName(task.Title);
            if (book != null)
            {
                if (!bookGroups.ContainsKey(book)) bookGroups[book] = new List<TaskItem>();
                bookGroups[book].Add(task);
            }
            else
            {
                standalone.Add(task);
            }
        }

        // Group remaining tasks by title so same-named tasks get merged
        var titleGroups = new Dictionary<string, List<TaskItem>>();
        foreach (var task in standalone)
        {
            var key = task.Title ?? "";
            if (!titleGroups.ContainsKey(key)) titleGroups[key] = new List<TaskItem>();
            titleGroups[key].Add(task);
        }

        // Process title groups
        foreach (var kv in titleGroups)
        {
            var title = kv.Key;
            var tasks = kv.Value;
            var slug = Slugify(title);

            if (tasks.Count == 1)
            {
                // Single task — standalone file with date prefix
                var task = tasks[0];
                var filePath = Path.Combine(dir, $"{task.Date}-{slug}.md");
                var tags = (task.Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                var frontmatter = new List<string> { "---" };
                frontmatter.Add($"date: {task.Date}");
                frontmatter.Add($"category: {task.Category ?? ""}");
                frontmatter.Add($"priority: {task.Priority}");
                frontmatter.Add($"status: {task.Status}");
                if (tags.Length > 0) frontmatter.Add($"tags: [{string.Join(", ", tags)}]");
                frontmatter.Add("source: DayLoop");
                frontmatter.Add("type: achievement");
                frontmatter.Add("---");
                frontmatter.Add("");

                var body = new List<string>
                {
                    $"# {title}",
                    "",
                    $"> {task.Achievement}",
                    "",
                };
                if (!string.IsNullOrEmpty(task.Note))
                    body.Add($"**备注**: {task.Note}");
                if (!string.IsNullOrEmpty(task.StartTime) || !string.IsNullOrEmpty(task.EndTime))
                    body.Add($"**时间**: {task.StartTime ?? ""}{(string.IsNullOrEmpty(task.EndTime) ? "" : " - " + task.EndTime)}");
                if (task.PlannedDuration > 0)
                    body.Add($"**计划时长**: {task.PlannedDuration}分钟");

                WriteFile(filePath, string.Join("\n", frontmatter) + string.Join("\n", body), vaultPath);
            }
            else
            {
                // Multiple tasks with same title → merged file without date prefix
                var filePath = Path.Combine(dir, $"{slug}.md");

                var entries = tasks.Select(t =>
                {
                    var date = (t.Date ?? "").Length >= 10 ? t.Date[..10] : "";
                    var parts = new List<string>
                    {
                        $"## {date}",
                        "",
                        $"> {t.Achievement}",
                        "",
                    };
                    if (!string.IsNullOrEmpty(t.Note))
                        parts.Add($"**备注**: {t.Note}");
                    if (!string.IsNullOrEmpty(t.StartTime) || !string.IsNullOrEmpty(t.EndTime))
                        parts.Add($"**时间**: {t.StartTime ?? ""}{(string.IsNullOrEmpty(t.EndTime) ? "" : " - " + t.EndTime)}");
                    if (t.PlannedDuration > 0)
                        parts.Add($"**计划时长**: {t.PlannedDuration}分钟");
                    return string.Join("\n", parts);
                });

                var allTags = tasks
                    .SelectMany(t => (t.Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Distinct()
                    .ToList();

                var dates = tasks.Select(t => t.Date).Where(d => !string.IsNullOrEmpty(d)).ToList();
                var dateRange = dates.Count == 1 ? dates[0] : $"{dates[0]} ~ {dates[^1]}";

                var frontmatter = new List<string> { "---" };
                frontmatter.Add($"title: {title}");
                frontmatter.Add($"date: {dateRange}");
                if (allTags.Count > 0) frontmatter.Add($"tags: [{string.Join(", ", allTags)}]");
                frontmatter.Add("source: DayLoop");
                frontmatter.Add("type: achievement");
                frontmatter.Add("---");
                frontmatter.Add("");

                WriteFile(filePath, string.Join("\n", frontmatter) + $"# {title}\n\n{string.Join("\n\n---\n\n", entries)}", vaultPath);
            }
        }

        // Book-grouped achievements → merged file
        foreach (var kv in bookGroups)
        {
            var book = kv.Key;
            var tasks = kv.Value;
            var slug = Slugify($"读书笔记-{book}");
            var filePath = Path.Combine(dir, $"{tasks[0].Date}-{slug}.md");

            var entries = tasks.Select(t =>
            {
                var date = (t.Date ?? "").Length >= 10 ? t.Date[..10] : "";
                var parts = new List<string>
                {
                    $"## {date}：{t.Title}",
                    "",
                    $"> {t.Achievement}",
                    "",
                };
                if (!string.IsNullOrEmpty(t.Note))
                    parts.Add($"**备注**: {t.Note}");
                return string.Join("\n", parts);
            });

            var allTags = tasks
                .SelectMany(t => (t.Tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct()
                .ToList();

            var frontmatter = new List<string> { "---" };
            frontmatter.Add($"book: 《{book}》");
            if (allTags.Count > 0) frontmatter.Add($"tags: [{string.Join(", ", allTags)}]");
            frontmatter.Add("source: DayLoop");
            frontmatter.Add("type: book-notes");
            frontmatter.Add("---");
            frontmatter.Add("");

            WriteFile(filePath, string.Join("\n", frontmatter) + $"# 《{book}》读书笔记\n\n{string.Join("\n\n---\n\n", entries)}", vaultPath);
        }

        return allTasks.Count;
    }

    public static (int Notes, int Reviews, int Achievements) SyncAll()
    {
        var vaultPath = GetVaultPath();
        if (string.IsNullOrEmpty(vaultPath)) return (0, 0, 0);

        // Clean entire DayLoop folder
        var baseDir = Path.Combine(vaultPath, "DayLoop");
        if (Directory.Exists(baseDir)) Directory.Delete(baseDir, true);

        var notes = SyncNotes();
        var reviews = SyncReviews();
        var achievements = SyncAchievements();
        return (notes, reviews, achievements);
    }

    private static readonly string UploadsDir = Path.Combine(
        Directory.GetCurrentDirectory(), "..", "backend", "data", "uploads"
    );

    private static string ProcessImageRefs(string content, string vaultImagesDir)
    {
        var regex = new Regex(@"!\[.*?\]\(/uploads/([^)]+)\)");
        var result = content;
        foreach (Match m in regex.Matches(content))
        {
            var filename = m.Groups[1].Value;
            var srcPath = Path.Combine(UploadsDir, filename);
            if (File.Exists(srcPath))
            {
                if (!Directory.Exists(vaultImagesDir))
                    Directory.CreateDirectory(vaultImagesDir);
                var destPath = Path.Combine(vaultImagesDir, filename);
                try { File.Copy(srcPath, destPath, overwrite: true); }
                catch (Exception ex) { Console.Error.WriteLine($"[ObsidianSync] Failed to copy image {filename}: {ex.Message}"); }
            }
            result = result.Replace(m.Value, $"![{filename}](../图片/{filename})");
        }
        return result;
    }

    private static void WriteFile(string filePath, string content, string? vaultPath = null)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            if (vaultPath != null)
            {
                var vaultImagesDir = Path.Combine(vaultPath, "DayLoop", "图片");
                content = ProcessImageRefs(content, vaultImagesDir);
            }
            File.WriteAllText(filePath, content);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ObsidianSync] Failed to write {filePath}: {ex.Message}");
        }
    }
}
