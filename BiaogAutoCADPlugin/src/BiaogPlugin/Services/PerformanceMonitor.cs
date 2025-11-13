using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Serilog;

namespace BiaogPlugin.Services;

/// <summary>
/// 性能监控服务 - 监控插件性能指标
/// </summary>
public class PerformanceMonitor
{
    private readonly Dictionary<string, PerformanceMetric> _metrics = new();
    private readonly object _lock = new();

    public PerformanceMonitor()
    {
        Log.Information("性能监控器已初始化");
    }

    /// <summary>
    /// 开始性能计时
    /// </summary>
    public IDisposable Measure(string operationName)
    {
        return new PerformanceMeasurement(this, operationName);
    }

    /// <summary>
    /// 记录操作耗时
    /// </summary>
    internal void RecordOperation(string operationName, long elapsedMilliseconds, bool success = true)
    {
        lock (_lock)
        {
            if (!_metrics.TryGetValue(operationName, out var metric))
            {
                metric = new PerformanceMetric(operationName);
                _metrics[operationName] = metric;
            }

            metric.RecordExecution(elapsedMilliseconds, success);
        }
    }

    /// <summary>
    /// 获取指定操作的性能指标
    /// </summary>
    public PerformanceMetric? GetMetric(string operationName)
    {
        lock (_lock)
        {
            return _metrics.TryGetValue(operationName, out var metric) ? metric : null;
        }
    }

    /// <summary>
    /// 获取所有性能指标
    /// </summary>
    public List<PerformanceMetric> GetAllMetrics()
    {
        lock (_lock)
        {
            return _metrics.Values.ToList();
        }
    }

    /// <summary>
    /// 生成性能报告
    /// </summary>
    public string GenerateReport()
    {
        lock (_lock)
        {
            if (_metrics.Count == 0)
            {
                return "暂无性能数据";
            }

            var report = "=== 性能监控报告 ===\n\n";

            foreach (var metric in _metrics.Values.OrderByDescending(m => m.TotalExecutionTimeMs))
            {
                report += metric.ToString() + "\n\n";
            }

            report += $"报告生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";

            return report;
        }
    }

    /// <summary>
    /// 清除所有性能数据
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _metrics.Clear();
            Log.Information("性能监控数据已清除");
        }
    }

    /// <summary>
    /// 检查是否有性能问题
    /// </summary>
    public List<PerformanceWarning> CheckForIssues()
    {
        var warnings = new List<PerformanceWarning>();

        lock (_lock)
        {
            foreach (var metric in _metrics.Values)
            {
                // 检查平均耗时过长（超过1秒）
                if (metric.AverageExecutionTimeMs > 1000)
                {
                    warnings.Add(new PerformanceWarning
                    {
                        Severity = WarningSeverity.Warning,
                        OperationName = metric.OperationName,
                        Message = $"平均耗时过长: {metric.AverageExecutionTimeMs:F2}ms",
                        Suggestion = "考虑优化算法或使用缓存"
                    });
                }

                // 检查最大耗时过长（超过5秒）
                if (metric.MaxExecutionTimeMs > 5000)
                {
                    warnings.Add(new PerformanceWarning
                    {
                        Severity = WarningSeverity.Error,
                        OperationName = metric.OperationName,
                        Message = $"最大耗时过长: {metric.MaxExecutionTimeMs}ms",
                        Suggestion = "可能存在性能瓶颈，需要深度优化"
                    });
                }

                // 检查失败率过高（超过5%）
                if (metric.FailureRate > 0.05)
                {
                    warnings.Add(new PerformanceWarning
                    {
                        Severity = WarningSeverity.Error,
                        OperationName = metric.OperationName,
                        Message = $"失败率过高: {metric.FailureRate:P}",
                        Suggestion = "检查错误日志，修复潜在问题"
                    });
                }
            }
        }

        return warnings;
    }

    /// <summary>
    /// 性能测量辅助类
    /// </summary>
    private class PerformanceMeasurement : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        private bool _success = true;

        public PerformanceMeasurement(PerformanceMonitor monitor, string operationName)
        {
            _monitor = monitor;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void MarkAsFailed()
        {
            _success = false;
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _monitor.RecordOperation(_operationName, _stopwatch.ElapsedMilliseconds, _success);

            // 如果操作耗时超过3秒，记录警告
            if (_stopwatch.ElapsedMilliseconds > 3000)
            {
                Log.Warning(
                    "操作 {OperationName} 耗时过长: {ElapsedMs}ms",
                    _operationName,
                    _stopwatch.ElapsedMilliseconds
                );
            }
        }
    }
}

/// <summary>
/// 性能指标
/// </summary>
public class PerformanceMetric
{
    private readonly List<long> _executionTimes = new();
    private int _failureCount = 0;

    public string OperationName { get; }
    public int ExecutionCount => _executionTimes.Count;
    public long TotalExecutionTimeMs => _executionTimes.Sum();
    public double AverageExecutionTimeMs => ExecutionCount > 0 ? _executionTimes.Average() : 0;
    public long MinExecutionTimeMs => ExecutionCount > 0 ? _executionTimes.Min() : 0;
    public long MaxExecutionTimeMs => ExecutionCount > 0 ? _executionTimes.Max() : 0;
    public int FailureCount => _failureCount;
    public double FailureRate => ExecutionCount > 0 ? (double)_failureCount / ExecutionCount : 0;
    public DateTime FirstExecutionTime { get; private set; }
    public DateTime LastExecutionTime { get; private set; }

    public PerformanceMetric(string operationName)
    {
        OperationName = operationName;
        FirstExecutionTime = DateTime.Now;
        LastExecutionTime = DateTime.Now;
    }

    internal void RecordExecution(long elapsedMilliseconds, bool success)
    {
        _executionTimes.Add(elapsedMilliseconds);
        if (!success)
        {
            _failureCount++;
        }
        LastExecutionTime = DateTime.Now;
    }

    public override string ToString()
    {
        return $"操作: {OperationName}\n" +
               $"  执行次数: {ExecutionCount}\n" +
               $"  总耗时: {TotalExecutionTimeMs}ms\n" +
               $"  平均耗时: {AverageExecutionTimeMs:F2}ms\n" +
               $"  最小/最大耗时: {MinExecutionTimeMs}ms / {MaxExecutionTimeMs}ms\n" +
               $"  失败次数: {FailureCount} ({FailureRate:P})\n" +
               $"  首次执行: {FirstExecutionTime:yyyy-MM-dd HH:mm:ss}\n" +
               $"  最后执行: {LastExecutionTime:yyyy-MM-dd HH:mm:ss}";
    }
}

/// <summary>
/// 性能警告
/// </summary>
public class PerformanceWarning
{
    public WarningSeverity Severity { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;

    public override string ToString()
    {
        var severityIcon = Severity switch
        {
            WarningSeverity.Info => "ℹ️",
            WarningSeverity.Warning => "⚠️",
            WarningSeverity.Error => "❌",
            _ => "•"
        };

        return $"{severityIcon} [{Severity}] {OperationName}: {Message}\n" +
               $"   建议: {Suggestion}";
    }
}

/// <summary>
/// 警告严重程度
/// </summary>
public enum WarningSeverity
{
    Info,
    Warning,
    Error
}
