using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BiaogeCSharp.Services;

/// <summary>
/// 性能监控服务
/// </summary>
public class PerformanceMonitor : IDisposable
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly Process _currentProcess;
    private Timer? _monitorTimer;
    private bool _disposed;

    // 性能指标
    public double CpuUsage { get; private set; }
    public long MemoryUsageMB { get; private set; }
    public long WorkingSetMB { get; private set; }
    public int ThreadCount { get; private set; }
    public TimeSpan TotalProcessorTime { get; private set; }

    // 性能统计
    private DateTime _lastCpuCheck = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;

    // 事件：当性能指标更新时触发
    public event EventHandler<PerformanceMetrics>? MetricsUpdated;

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
    {
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();

        // 初始化CPU统计
        _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
        _lastCpuCheck = DateTime.UtcNow;

        _logger.LogInformation("性能监控器已初始化");
    }

    /// <summary>
    /// 开始监控
    /// </summary>
    public void Start(int intervalMilliseconds = 1000)
    {
        if (_monitorTimer != null)
        {
            _logger.LogWarning("性能监控已经在运行");
            return;
        }

        _monitorTimer = new Timer(
            _ => UpdateMetrics(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(intervalMilliseconds)
        );

        _logger.LogInformation("性能监控已启动，更新间隔: {Interval}ms", intervalMilliseconds);
    }

    /// <summary>
    /// 停止监控
    /// </summary>
    public void Stop()
    {
        _monitorTimer?.Dispose();
        _monitorTimer = null;
        _logger.LogInformation("性能监控已停止");
    }

    /// <summary>
    /// 更新性能指标
    /// </summary>
    private void UpdateMetrics()
    {
        try
        {
            _currentProcess.Refresh();

            // 内存使用
            MemoryUsageMB = _currentProcess.PrivateMemorySize64 / 1024 / 1024;
            WorkingSetMB = _currentProcess.WorkingSet64 / 1024 / 1024;

            // 线程数
            ThreadCount = _currentProcess.Threads.Count;

            // CPU使用率
            var currentTime = DateTime.UtcNow;
            var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;

            var cpuUsedMs = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
            var totalMsPassed = (currentTime - _lastCpuCheck).TotalMilliseconds;

            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);
            CpuUsage = Math.Round(cpuUsageTotal * 100, 2);

            TotalProcessorTime = currentTotalProcessorTime;

            _lastTotalProcessorTime = currentTotalProcessorTime;
            _lastCpuCheck = currentTime;

            // 触发事件
            MetricsUpdated?.Invoke(this, new PerformanceMetrics
            {
                CpuUsage = CpuUsage,
                MemoryUsageMB = MemoryUsageMB,
                WorkingSetMB = WorkingSetMB,
                ThreadCount = ThreadCount,
                Timestamp = DateTime.Now
            });

            _logger.LogDebug(
                "性能指标更新: CPU={CpuUsage}%, 内存={MemoryUsage}MB, 线程={ThreadCount}",
                CpuUsage,
                MemoryUsageMB,
                ThreadCount
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新性能指标失败");
        }
    }

    /// <summary>
    /// 获取当前性能快照
    /// </summary>
    public PerformanceMetrics GetSnapshot()
    {
        UpdateMetrics();

        return new PerformanceMetrics
        {
            CpuUsage = CpuUsage,
            MemoryUsageMB = MemoryUsageMB,
            WorkingSetMB = WorkingSetMB,
            ThreadCount = ThreadCount,
            Timestamp = DateTime.Now
        };
    }

    /// <summary>
    /// 记录性能报告
    /// </summary>
    public void LogPerformanceReport()
    {
        var metrics = GetSnapshot();

        _logger.LogInformation(
            "性能报告\n" +
            "  CPU使用率: {CpuUsage}%\n" +
            "  内存使用: {MemoryUsage} MB\n" +
            "  工作集: {WorkingSet} MB\n" +
            "  线程数: {ThreadCount}\n" +
            "  处理器时间: {ProcessorTime}",
            metrics.CpuUsage,
            metrics.MemoryUsageMB,
            metrics.WorkingSetMB,
            metrics.ThreadCount,
            TotalProcessorTime
        );
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _currentProcess?.Dispose();
        _disposed = true;

        _logger.LogInformation("性能监控器已释放");
    }
}

/// <summary>
/// 性能指标数据
/// </summary>
public class PerformanceMetrics
{
    public double CpuUsage { get; set; }
    public long MemoryUsageMB { get; set; }
    public long WorkingSetMB { get; set; }
    public int ThreadCount { get; set; }
    public DateTime Timestamp { get; set; }

    public override string ToString()
    {
        return $"CPU: {CpuUsage:F2}%, 内存: {MemoryUsageMB}MB, 线程: {ThreadCount}";
    }
}

/// <summary>
/// 性能计时器 - 用于测量代码块执行时间
/// </summary>
public class PerformanceTimer : IDisposable
{
    private readonly Stopwatch _stopwatch;
    private readonly string _operationName;
    private readonly ILogger _logger;

    public PerformanceTimer(string operationName, ILogger logger)
    {
        _operationName = operationName;
        _logger = logger;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        _logger.LogInformation(
            "性能: {Operation} 耗时 {ElapsedMs}ms",
            _operationName,
            _stopwatch.ElapsedMilliseconds
        );
    }

    public long ElapsedMilliseconds => _stopwatch.ElapsedMilliseconds;
}
