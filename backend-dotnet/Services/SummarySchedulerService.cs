using Microsoft.Data.Sqlite;
using DayLoop.Api.Data;
using DayLoop.Api.Controllers;

namespace DayLoop.Api.Services;

public class SummarySchedulerService : BackgroundService
{
    private readonly ILogger<SummarySchedulerService> _logger;

    public SummarySchedulerService(ILogger<SummarySchedulerService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[SummaryScheduler] Registered: auto-generate summaries at 22:00");

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var next10Pm = now.Date.AddHours(22);
            if (now >= next10Pm)
                next10Pm = next10Pm.AddDays(1);
            var delay = next10Pm - now;

            await Task.Delay(delay, stoppingToken);

            AutoGenerateSummaries();
        }
    }

    private void AutoGenerateSummaries()
    {
        var today = DateTime.Now;
        var year = today.Year;
        var month = today.Month;
        var day = today.Day;
        var dow = (int)today.DayOfWeek;
        var lastDayOfMonth = DateTime.DaysInMonth(year, month);
        var quarter = (int)Math.Ceiling(month / 3.0);
        var lastMonthOfQuarter = quarter * 3;
        var lastDayOfQuarter = DateTime.DaysInMonth(year, lastMonthOfQuarter);

        var periods = new List<(string type, string period)>();

        if (dow == 0)
        {
            var jan1 = new DateTime(year, 1, 1);
            var days = (int)(today - jan1).TotalDays;
            var weekNum = (int)Math.Ceiling((days + (int)jan1.DayOfWeek + 1) / 7.0);
            periods.Add(("weekly", $"{year}-W{weekNum:D2}"));
        }

        if (day == lastDayOfMonth)
            periods.Add(("monthly", $"{year}-{month:D2}"));

        if (day == lastDayOfQuarter && month == lastMonthOfQuarter)
            periods.Add(("quarterly", $"{year}-Q{quarter}"));

        if (month == 12 && day == 31)
            periods.Add(("yearly", $"{year}"));

        if (periods.Count == 0) return;

        using var conn = Database.CreateConnection();
        var users = new List<long>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id FROM users";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                users.Add(reader.GetInt64(0));
        }

        foreach (var userId in users)
        {
            foreach (var (type, period) in periods)
            {
                try
                {
                    using var check = conn.CreateCommand();
                    check.CommandText = "SELECT id FROM summaries WHERE user_id = @uid AND type = @type AND period_key = @period";
                    check.Parameters.AddWithValue("@uid", userId);
                    check.Parameters.AddWithValue("@type", type);
                    check.Parameters.AddWithValue("@period", period);
                    var existing = check.ExecuteScalar();
                    if (existing != null) continue;

                    var autoSummary = SummariesController.GenerateAutoSummary(userId, type, period);
                    using var insert = conn.CreateCommand();
                    insert.CommandText = "INSERT INTO summaries (type, period_key, auto_summary, user_id) VALUES (@type, @period, @auto, @uid)";
                    insert.Parameters.AddWithValue("@type", type);
                    insert.Parameters.AddWithValue("@period", period);
                    insert.Parameters.AddWithValue("@auto", autoSummary);
                    insert.Parameters.AddWithValue("@uid", userId);
                    insert.ExecuteNonQuery();

                    _logger.LogInformation("[SummaryScheduler] Auto-generated {type} summary for user {uid} period {period}", type, userId, period);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SummaryScheduler] Error for user {uid} {type} {period}", userId, type, period);
                }
            }
        }
    }
}
