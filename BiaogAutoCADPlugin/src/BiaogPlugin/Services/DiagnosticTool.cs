using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace BiaogPlugin.Services;

/// <summary>
/// 诊断工具 - 自动检测常见问题并提供修复建议
/// </summary>
public class DiagnosticTool
{
    private readonly ConfigManager _configManager;
    private readonly BailianApiClient _bailianClient;
    private readonly CacheService _cacheService;

    public DiagnosticTool(
        ConfigManager configManager,
        BailianApiClient bailianClient,
        CacheService cacheService)
    {
        _configManager = configManager;
        _bailianClient = bailianClient;
        _cacheService = cacheService;
    }

    /// <summary>
    /// 运行完整诊断
    /// </summary>
    public async Task<DiagnosticReport> RunFullDiagnosticAsync()
    {
        Log.Information("开始运行诊断...");

        var report = new DiagnosticReport();
        var checks = new List<Task<DiagnosticCheck>>
        {
            CheckConfigurationAsync(),
            CheckApiConnectionAsync(),
            CheckCacheHealthAsync(),
            CheckFileSystemPermissionsAsync(),
            CheckDiskSpaceAsync(),
            CheckNetworkConnectivityAsync()
        };

        var results = await Task.WhenAll(checks);
        report.Checks.AddRange(results);

        report.OverallStatus = report.Checks.All(c => c.Status == CheckStatus.Pass)
            ? DiagnosticStatus.Healthy
            : report.Checks.Any(c => c.Status == CheckStatus.Fail)
                ? DiagnosticStatus.Critical
                : DiagnosticStatus.Warning;

        Log.Information($"诊断完成，状态: {report.OverallStatus}");

        return report;
    }

    /// <summary>
    /// 检查配置
    /// </summary>
    private async Task<DiagnosticCheck> CheckConfigurationAsync()
    {
        var check = new DiagnosticCheck
        {
            Name = "配置文件检查",
            Category = "Configuration"
        };

        try
        {
            // 检查配置文件是否存在
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".biaoge",
                "config.json"
            );

            if (!File.Exists(configPath))
            {
                check.Status = CheckStatus.Warning;
                check.Message = "配置文件不存在";
                check.Recommendation = "首次使用需要配置API密钥，请运行 BIAOGE_SETTINGS 命令";
                return check;
            }

            // 检查API密钥是否配置
            var apiKey = _configManager.GetString("Bailian:ApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                check.Status = CheckStatus.Warning;
                check.Message = "未配置API密钥";
                check.Recommendation = "请在设置中配置阿里云百炼API密钥";
                return check;
            }

            // 检查翻译模型配置
            var model = _configManager.GetString("Bailian:TextTranslationModel");
            if (string.IsNullOrEmpty(model))
            {
                check.Status = CheckStatus.Warning;
                check.Message = "未配置翻译模型";
                check.Recommendation = "建议在设置中选择翻译模型（默认: qwen-mt-plus）";
                return check;
            }

            check.Status = CheckStatus.Pass;
            check.Message = "配置正常";
            check.Details = $"API密钥已配置，翻译模型: {model}";
        }
        catch (System.Exception ex)
        {
            check.Status = CheckStatus.Fail;
            check.Message = "配置检查失败";
            check.Details = ex.Message;
            Log.Error(ex, "配置检查失败");
        }

        return await Task.FromResult(check);
    }

    /// <summary>
    /// 检查API连接
    /// </summary>
    private async Task<DiagnosticCheck> CheckApiConnectionAsync()
    {
        var check = new DiagnosticCheck
        {
            Name = "API连接检查",
            Category = "Network"
        };

        try
        {
            if (!_bailianClient.HasApiKey)
            {
                check.Status = CheckStatus.Warning;
                check.Message = "未配置API密钥，跳过连接测试";
                return check;
            }

            var success = await _bailianClient.TestConnectionAsync();

            if (success)
            {
                check.Status = CheckStatus.Pass;
                check.Message = "API连接正常";
            }
            else
            {
                check.Status = CheckStatus.Fail;
                check.Message = "API连接失败";
                check.Recommendation = "检查API密钥是否正确，确认网络连接正常";
            }
        }
        catch (System.Exception ex)
        {
            check.Status = CheckStatus.Fail;
            check.Message = "API连接测试异常";
            check.Details = ex.Message;
            check.Recommendation = "检查网络连接，确认防火墙设置允许访问 dashscope.aliyuncs.com";
            Log.Error(ex, "API连接测试失败");
        }

        return check;
    }

    /// <summary>
    /// 检查缓存健康状态
    /// </summary>
    private async Task<DiagnosticCheck> CheckCacheHealthAsync()
    {
        var check = new DiagnosticCheck
        {
            Name = "缓存系统检查",
            Category = "Storage"
        };

        try
        {
            var stats = await _cacheService.GetStatisticsAsync();

            check.Status = CheckStatus.Pass;
            check.Message = $"缓存正常 ({stats.TotalCount} 条记录)";
            check.Details = $"总记录数: {stats.TotalCount}, 语言数: {stats.LanguageCount}";

            // 检查缓存是否过大（超过10万条）
            if (stats.TotalCount > 100000)
            {
                check.Status = CheckStatus.Warning;
                check.Message = "缓存记录数较多";
                check.Recommendation = "建议清理旧缓存以释放空间，运行 BIAOGE_CLEARCACHE 命令";
            }
        }
        catch (System.Exception ex)
        {
            check.Status = CheckStatus.Fail;
            check.Message = "缓存系统异常";
            check.Details = ex.Message;
            check.Recommendation = "尝试删除缓存数据库文件: %USERPROFILE%\\.biaoge\\cache.db";
            Log.Error(ex, "缓存健康检查失败");
        }

        return check;
    }

    /// <summary>
    /// 检查文件系统权限
    /// </summary>
    private async Task<DiagnosticCheck> CheckFileSystemPermissionsAsync()
    {
        var check = new DiagnosticCheck
        {
            Name = "文件系统权限检查",
            Category = "Security"
        };

        try
        {
            var testPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".biaoge"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Biaoge")
            };

            var issues = new List<string>();

            foreach (var path in testPaths)
            {
                Directory.CreateDirectory(path);

                // 测试写入权限
                var testFile = Path.Combine(path, $"test_{Guid.NewGuid()}.tmp");
                try
                {
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }
                catch
                {
                    issues.Add($"无写入权限: {path}");
                }
            }

            if (issues.Any())
            {
                check.Status = CheckStatus.Fail;
                check.Message = "文件系统权限不足";
                check.Details = string.Join("; ", issues);
                check.Recommendation = "请联系系统管理员授予相应目录的读写权限";
            }
            else
            {
                check.Status = CheckStatus.Pass;
                check.Message = "文件系统权限正常";
            }
        }
        catch (System.Exception ex)
        {
            check.Status = CheckStatus.Fail;
            check.Message = "权限检查失败";
            check.Details = ex.Message;
            Log.Error(ex, "文件系统权限检查失败");
        }

        return await Task.FromResult(check);
    }

    /// <summary>
    /// 检查磁盘空间
    /// </summary>
    private async Task<DiagnosticCheck> CheckDiskSpaceAsync()
    {
        var check = new DiagnosticCheck
        {
            Name = "磁盘空间检查",
            Category = "Storage"
        };

        try
        {
            var dataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".biaoge"
            );

            var drive = new DriveInfo(Path.GetPathRoot(dataPath)!);
            var freeSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);

            if (freeSpaceGB < 0.1) // 小于100MB
            {
                check.Status = CheckStatus.Fail;
                check.Message = $"磁盘空间不足: {freeSpaceGB:F2}GB";
                check.Recommendation = "清理磁盘空间以确保插件正常运行";
            }
            else if (freeSpaceGB < 1.0) // 小于1GB
            {
                check.Status = CheckStatus.Warning;
                check.Message = $"磁盘空间较少: {freeSpaceGB:F2}GB";
                check.Recommendation = "建议清理磁盘以预留更多空间";
            }
            else
            {
                check.Status = CheckStatus.Pass;
                check.Message = $"磁盘空间充足: {freeSpaceGB:F2}GB";
            }
        }
        catch (System.Exception ex)
        {
            check.Status = CheckStatus.Warning;
            check.Message = "磁盘空间检查异常";
            check.Details = ex.Message;
            Log.Warning(ex, "磁盘空间检查失败");
        }

        return await Task.FromResult(check);
    }

    /// <summary>
    /// 检查网络连接
    /// </summary>
    private async Task<DiagnosticCheck> CheckNetworkConnectivityAsync()
    {
        var check = new DiagnosticCheck
        {
            Name = "网络连接检查",
            Category = "Network"
        };

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await httpClient.GetAsync("https://www.baidu.com");

            if (response.IsSuccessStatusCode)
            {
                check.Status = CheckStatus.Pass;
                check.Message = "网络连接正常";
            }
            else
            {
                check.Status = CheckStatus.Warning;
                check.Message = $"网络响应异常: {response.StatusCode}";
            }
        }
        catch (System.Exception ex)
        {
            check.Status = CheckStatus.Fail;
            check.Message = "网络连接失败";
            check.Details = ex.Message;
            check.Recommendation = "检查网络连接，确认是否可以访问互联网";
            Log.Error(ex, "网络连接检查失败");
        }

        return check;
    }
}

/// <summary>
/// 诊断报告
/// </summary>
public class DiagnosticReport
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public DiagnosticStatus OverallStatus { get; set; }
    public List<DiagnosticCheck> Checks { get; set; } = new();

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine("╔═══════════════════════════════════════════════════════╗");
        sb.AppendLine("║            标哥插件诊断报告                          ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════╝");
        sb.AppendLine();
        sb.AppendLine($"报告时间: {Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"总体状态: {OverallStatus}");
        sb.AppendLine();

        foreach (var check in Checks)
        {
            sb.AppendLine(check.ToString());
            sb.AppendLine();
        }

        var passCount = Checks.Count(c => c.Status == CheckStatus.Pass);
        var warnCount = Checks.Count(c => c.Status == CheckStatus.Warning);
        var failCount = Checks.Count(c => c.Status == CheckStatus.Fail);

        sb.AppendLine($"检查项统计: 通过 {passCount} | 警告 {warnCount} | 失败 {failCount}");

        return sb.ToString();
    }
}

/// <summary>
/// 诊断检查项
/// </summary>
public class DiagnosticCheck
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public CheckStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? Recommendation { get; set; }

    public override string ToString()
    {
        var statusIcon = Status switch
        {
            CheckStatus.Pass => "✓",
            CheckStatus.Warning => "⚠",
            CheckStatus.Fail => "✗",
            _ => "•"
        };

        var sb = new StringBuilder();
        sb.AppendLine($"[{statusIcon}] {Name} ({Category})");
        sb.AppendLine($"    状态: {Status}");
        sb.AppendLine($"    消息: {Message}");

        if (!string.IsNullOrEmpty(Details))
        {
            sb.AppendLine($"    详情: {Details}");
        }

        if (!string.IsNullOrEmpty(Recommendation))
        {
            sb.AppendLine($"    建议: {Recommendation}");
        }

        return sb.ToString();
    }
}

/// <summary>
/// 检查状态
/// </summary>
public enum CheckStatus
{
    Pass,
    Warning,
    Fail
}

/// <summary>
/// 诊断状态
/// </summary>
public enum DiagnosticStatus
{
    Healthy,
    Warning,
    Critical
}
