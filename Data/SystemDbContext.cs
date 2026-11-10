using Microsoft.EntityFrameworkCore;

namespace SystemLiveDashboard.Data;

public class SystemDbContext : DbContext
{
    public DbSet<SystemMetric> Metrics { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // دیتابیس در پوشه برنامه ذخیره می‌شود
        optionsBuilder.UseSqlite("Data Source=system_metrics.db");
    }
}

public class SystemMetric
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public float CpuLoad { get; set; }
    public float MemUsedPercent { get; set; }
    public double MemUsedGB { get; set; }
}