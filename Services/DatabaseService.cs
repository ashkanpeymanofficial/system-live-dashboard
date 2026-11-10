using SystemLiveDashboard.Data;
using Microsoft.EntityFrameworkCore;

namespace SystemLiveDashboard.Services;

public class DatabaseService
{
    private readonly ILogger<DatabaseService> _logger;
    public DatabaseService(ILogger<DatabaseService> logger)
    {
        _logger = logger;
        // ساخت خودکار دیتابیس در اولین اجرا
        using var db = new SystemDbContext();
        db.Database.EnsureCreated();
    }

    public async Task SaveMetricAsync(float cpu, float memPercent, double memGB)
    {
        try
        {
            using var db = new SystemDbContext();
            db.Metrics.Add(new SystemMetric
            {
                Timestamp = DateTime.Now,
                CpuLoad = cpu,
                MemUsedPercent = memPercent,
                MemUsedGB = memGB
            });
            await db.SaveChangesAsync();

            // هر 100 رکورد، رکوردهای قدیمی‌تر از 24 ساعت را پاک کن تا دیتابیس سنگین نشود
            var count = await db.Metrics.CountAsync();
            if (count % 100 == 0)
            {
                var cutoff = DateTime.Now.AddHours(-24);
                var oldRecords = await db.Metrics.Where(m => m.Timestamp < cutoff).ToListAsync();
                if (oldRecords.Any())
                {
                    db.Metrics.RemoveRange(oldRecords);
                    await db.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving metric to DB");
        }
    }

    public async Task<List<SystemMetric>> GetHistoryAsync(int hours = 1)
    {
        using var db = new SystemDbContext();
        var cutoff = DateTime.Now.AddHours(-hours);
        return await db.Metrics
            .Where(m => m.Timestamp >= cutoff)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }
}