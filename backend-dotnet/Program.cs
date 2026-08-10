using System.Net;
using System.Net.Sockets;
using DayLoop.Api.Data;
using DayLoop.Api.Services;

var builder = WebApplication.CreateBuilder(args);

var port = builder.Configuration["PORT"] ?? "5000";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});
builder.Services.AddHostedService<RecurringTaskService>();
builder.Services.AddHostedService<SummarySchedulerService>();

var app = builder.Build();

app.UseCors();
app.MapControllers();

// Initialize database
Database.Initialize();
SeedData.Seed();
Console.WriteLine("[Scheduler] Registered: auto-generate recurring tasks at midnight");

// Serve uploaded files
var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "backend", "data", "uploads");
if (!Directory.Exists(uploadsDir))
    Directory.CreateDirectory(uploadsDir);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsDir),
    RequestPath = "/uploads"
});

// Serve frontend SPA
var distDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "frontend-dotnet", "dist");
if (Directory.Exists(distDir))
{
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(distDir)
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(distDir)
    });
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(distDir)
    });
}

var VERSION = "2.0.0";
var lanIP = GetLANIP();
var publicUrl = "";

app.MapGet("/api/version", () => Results.Json(new
{
    version = VERSION,
    server = Dns.GetHostName(),
    lanIP,
    port = int.Parse(port),
    publicUrl = string.IsNullOrEmpty(publicUrl) ? null : publicUrl,
}));

Console.WriteLine("");
Console.WriteLine("╔═══════════════════════════════════════════╗");
Console.WriteLine("║           DayLoop v" + VERSION + " (.NET)          ║");
Console.WriteLine("╠═══════════════════════════════════════════╣");
Console.WriteLine("║                                           ║");
Console.WriteLine("║  Local:  http://localhost:" + port.PadRight(4) + "                   ║");
Console.WriteLine("║  LAN:    http://" + (lanIP + ":" + port).PadRight(30) + "      ║");
Console.WriteLine("║                                           ║");
Console.WriteLine("║  Phone: open browser, menu \"Add to Home\"  ║");
Console.WriteLine("╚═══════════════════════════════════════════╝");
Console.WriteLine("");

app.Run();

static string GetLANIP()
{
    try
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                return ip.ToString();
        }
    }
    catch { }
    return "localhost";
}

public partial class Program { }
