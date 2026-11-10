using LibreHardwareMonitor.Hardware;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace SystemLiveDashboard.Services;

public class HardwareMonitorService : BackgroundService
{
    private readonly Computer _computer;
    private readonly IHubContext<MonitorHub> _hubContext;
    private readonly ILogger<HardwareMonitorService> _logger;
    private readonly DatabaseService _dbService; // این خط اضافه شود

    // برای محاسبه سرعت شبکه
    private long _lastBytesReceived = 0;
    private long _lastBytesSent = 0;
    private DateTime _lastNetworkCheck = DateTime.Now;

     public HardwareMonitorService(IHubContext<MonitorHub> hubContext, ILogger<HardwareMonitorService> logger, DatabaseService dbService) // dbService به پارامترها اضافه شود
    {
        _hubContext = hubContext;
        _logger = logger;
        _dbService = dbService;
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsMemoryEnabled = true,
            IsStorageEnabled = true
        };
        _computer.Open();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _computer.Accept(new UpdateVisitor());

                // === CPU ===
                string cpuName = "Unknown";
                int coreCount = 0;
                int threadCount = 0;
                float cpuLoad = 0;
                var coreLoads = new List<object>();

                foreach (var hw in _computer.Hardware)
                {
                    if (hw.HardwareType == HardwareType.Cpu)
                    {
                        cpuName = hw.Name;
                        foreach (var sensor in hw.Sensors)
                        {
                            if (sensor.SensorType == SensorType.Load && sensor.Value.HasValue)
                            {
                                if (sensor.Name.Contains("Total"))
                                    cpuLoad = sensor.Value.Value;
                                else if (sensor.Name.Contains("Core"))
                                    coreLoads.Add(new { Name = sensor.Name, Value = Math.Round(sensor.Value.Value, 1) });
                            }
                        }
                    }
                }

                // تعداد هسته/ترد از ویندوز
                var cpuInfo = GetCpuInfo();
                coreCount = cpuInfo.cores;
                threadCount = cpuInfo.threads;

                // === RAM ===
                double totalRamGB = 0, usedRamGB = 0;
                float memUsedPercent = 0;
                foreach (var hw in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Memory))
                {
                    foreach (var sensor in hw.Sensors)
                    {
                        if (sensor.SensorType == SensorType.Data && sensor.Value.HasValue)
                        {
                            if (sensor.Name.Contains("Used")) usedRamGB = sensor.Value.Value;
                            if (sensor.Name.Contains("Available")) totalRamGB = usedRamGB + sensor.Value.Value;
                        }
                        if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Memory") && sensor.Value.HasValue)
                            memUsedPercent = sensor.Value.Value;
                    }
                }
                if (totalRamGB == 0)
                {
                    var memInfo = GetRamInfo();
                    totalRamGB = memInfo.total;
                    usedRamGB = memInfo.used;
                    memUsedPercent = (float)(usedRamGB / totalRamGB * 100);
                }

                // === دیسک‌ها ===
                var disks = new List<object>();
                foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    disks.Add(new
                    {
                        Name = drive.Name.Replace("\\", ""),
                        Label = string.IsNullOrEmpty(drive.VolumeLabel) ? "Local Disk" : drive.VolumeLabel,
                        TotalGB = Math.Round(drive.TotalSize / 1073741824.0, 1),
                        UsedGB = Math.Round((drive.TotalSize - drive.AvailableFreeSpace) / 1073741824.0, 1),
                        Percent = Math.Round((drive.TotalSize - drive.AvailableFreeSpace) / (double)drive.TotalSize * 100, 1)
                    });
                }

                // === Uptime ===
                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                string uptimeStr = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";

                // === شبکه ===
                long bytesRecv = 0, bytesSent = 0;
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    {
                        var stats = ni.GetIPv4Statistics();
                        bytesRecv += stats.BytesReceived;
                        bytesSent += stats.BytesSent;
                    }
                }
                var now = DateTime.Now;
                var elapsed = (now - _lastNetworkCheck).TotalSeconds;
                double downSpeed = elapsed > 0 ? (bytesRecv - _lastBytesReceived) / elapsed / 1024 : 0;
                double upSpeed = elapsed > 0 ? (bytesSent - _lastBytesSent) / elapsed / 1024 : 0;
                _lastBytesReceived = bytesRecv;
                _lastBytesSent = bytesSent;
                _lastNetworkCheck = now;

                // === تاپ ۵ پروسه ===
                var topProcs = new List<object>();
                foreach (var p in Process.GetProcesses().OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0; } }).Take(5))
                {
                    try
                    {
                        topProcs.Add(new
                        {
                            Name = p.ProcessName,
                            RamMB = Math.Round(p.WorkingSet64 / 1048576.0, 1)
                        });
                    }
                    catch { }
                }

                                // ذخیره در دیتابیس
                await _dbService.SaveMetricAsync((float)Math.Round(cpuLoad, 1), (float)Math.Round(memUsedPercent, 1), Math.Round(usedRamGB, 1));

                await _hubContext.Clients.All.SendAsync("ReceiveStats", new
                {
                    Timestamp = now.ToString("HH:mm:ss"),
                    CpuName = cpuName,
                    CoreCount = coreCount,
                    ThreadCount = threadCount,
                    CpuLoad = Math.Round(cpuLoad, 1),
                    CoreLoads = coreLoads,
                    MemUsedPercent = Math.Round(memUsedPercent, 1),
                    MemUsedGB = Math.Round(usedRamGB, 1),
                    MemTotalGB = Math.Round(totalRamGB, 1),
                    Disks = disks,
                    Uptime = uptimeStr,
                    DownSpeedKB = Math.Round(downSpeed, 1),
                    UpSpeedKB = Math.Round(upSpeed, 1),
                    TopProcesses = topProcs
                }, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در خواندن داده‌ها");
            }

            await Task.Delay(2000, stoppingToken);
        }
    }

    private static (int cores, int threads) GetCpuInfo()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher("SELECT NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                int cores = Convert.ToInt32(obj["NumberOfCores"]);
                int threads = Convert.ToInt32(obj["NumberOfLogicalProcessors"]);
                return (cores, threads);
            }
        }
        catch { }
        return (Environment.ProcessorCount / 2, Environment.ProcessorCount);
    }

    private static (double total, double used) GetRamInfo()
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT TotalPhysicalMemory, FreePhysicalMemory FROM Win32_ComputerSystem, Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                double total = Convert.ToDouble(obj["TotalPhysicalMemory"]) / 1073741824.0;
                double free = Convert.ToDouble(obj["FreePhysicalMemory"]) * 1024 / 1073741824.0;
                return (total, total - free);
            }
        }
        catch { }
        return (16, 8);
    }

    public override void Dispose()
    {
        _computer.Close();
        base.Dispose();
    }
}

public class UpdateVisitor : IVisitor
{
    public void VisitComputer(IComputer computer) => computer.Traverse(this);
    public void VisitHardware(IHardware hardware)
    {
        hardware.Update();
        foreach (var sub in hardware.SubHardware) sub.Accept(this);
    }
    public void VisitSensor(ISensor sensor) { }
    public void VisitParameter(IParameter parameter) { }
}