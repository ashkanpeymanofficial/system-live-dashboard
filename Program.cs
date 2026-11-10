using SystemLiveDashboard;
using SystemLiveDashboard.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddHostedService<HardwareMonitorService>();
builder.Services.AddSingleton<DatabaseService>(); // این خط اضافه شد

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<MonitorHub>("/monitorHub");

// این Endpoint جدید برای گرفتن تاریخچه است
app.MapGet("/api/history", async (DatabaseService dbService, int hours = 1) =>
{
    var history = await dbService.GetHistoryAsync(hours);
    // خروجی را به فرمت مناسب برای نمودار تبدیل می‌کنیم
    var result = history.Select(h => new
    {
        Time = h.Timestamp.ToString("HH:mm"),
        Cpu = h.CpuLoad,
        Mem = h.MemUsedPercent
    });
    return Results.Ok(result);
});

app.MapGet("/api/history/export", async (DatabaseService dbService) =>
{
    var history = await dbService.GetHistoryAsync(24);
    var csv = new StringBuilder();
    csv.AppendLine("Timestamp,CPU Load (%),RAM Used (%),RAM Used (GB)");
    foreach (var h in history)
    {
        csv.AppendLine($"{h.Timestamp:yyyy-MM-dd HH:mm:ss},{h.CpuLoad},{h.MemUsedPercent},{h.MemUsedGB}");
    }
    
    var bytes = Encoding.UTF8.GetBytes(csv.ToString());
    return Results.File(bytes, "text/csv", "system_metrics.csv");
});



app.Run();