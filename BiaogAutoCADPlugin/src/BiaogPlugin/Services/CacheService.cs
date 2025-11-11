using Microsoft.Data.Sqlite;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BiaogPlugin.Services;

/// <summary>
/// 翻译缓存服务（SQLite）
/// ✅ 优化：使用连接池化 + 异步延迟初始化
/// </summary>
public class CacheService
{
    private readonly string _dbPath;
    private readonly string _connectionString;
    private bool _initialized = false;
    private readonly System.Threading.SemaphoreSlim _initLock = new(1, 1);

    public CacheService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".biaoge"
        );
        Directory.CreateDirectory(appDataPath);

        _dbPath = Path.Combine(appDataPath, "cache.db");

        // ✅ 优化：使用连接字符串池化，提高性能
        // Mode=ReadWriteCreate: 如果不存在则创建
        // Cache=Shared: 多个连接共享缓存
        // Pooling=True: 启用连接池
        _connectionString = $"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared;Pooling=True";

        Log.Debug("CacheService已构造，延迟初始化数据库");
    }

    /// <summary>
    /// ✅ 优化：异步延迟初始化，避免阻塞AutoCAD启动
    /// </summary>
    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS translation_cache (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    source_text TEXT NOT NULL,
                    target_language TEXT NOT NULL,
                    translated_text TEXT NOT NULL,
                    created_at INTEGER NOT NULL,
                    UNIQUE(source_text, target_language)
                );

                CREATE INDEX IF NOT EXISTS idx_cache_lookup
                ON translation_cache(source_text, target_language);

                CREATE INDEX IF NOT EXISTS idx_cache_created
                ON translation_cache(created_at);
            ";
            await command.ExecuteNonQueryAsync();

            _initialized = true;
            Log.Information("缓存数据库初始化完成: {DbPath}", _dbPath);
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// 获取翻译缓存
    /// ✅ 优化：添加TTL检查，默认30天过期
    /// </summary>
    public async Task<string?> GetTranslationAsync(string sourceText, string targetLanguage, int expirationDays = 30)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT translated_text, created_at
            FROM translation_cache
            WHERE source_text = $source_text AND target_language = $target_language
        ";
        command.Parameters.AddWithValue("$source_text", sourceText);
        command.Parameters.AddWithValue("$target_language", targetLanguage);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var translatedText = reader.GetString(0);
            var createdAt = reader.GetInt64(1);

            // ✅ 检查是否过期
            var expirationTimestamp = DateTimeOffset.UtcNow.AddDays(-expirationDays).ToUnixTimeSeconds();
            if (createdAt < expirationTimestamp)
            {
                Log.Debug("缓存已过期: {Text}, 创建时间: {CreatedAt}", sourceText, DateTimeOffset.FromUnixTimeSeconds(createdAt));
                return null; // 返回null，触发重新翻译
            }

            return translatedText;
        }

        return null;
    }

    /// <summary>
    /// 设置翻译缓存
    /// </summary>
    public async Task SetTranslationAsync(string sourceText, string targetLanguage, string translatedText)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO translation_cache (source_text, target_language, translated_text, created_at)
            VALUES ($source_text, $target_language, $translated_text, $created_at)
        ";
        command.Parameters.AddWithValue("$source_text", sourceText);
        command.Parameters.AddWithValue("$target_language", targetLanguage);
        command.Parameters.AddWithValue("$translated_text", translatedText);
        command.Parameters.AddWithValue("$created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 清理所有缓存
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM translation_cache";
        await command.ExecuteNonQueryAsync();

        Log.Information("缓存已清空");
    }

    /// <summary>
    /// ✅ 优化：清理过期缓存
    /// </summary>
    public async Task<int> CleanExpiredCacheAsync(int expirationDays = 30)
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var expirationTimestamp = DateTimeOffset.UtcNow.AddDays(-expirationDays).ToUnixTimeSeconds();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM translation_cache WHERE created_at < $expiration";
        command.Parameters.AddWithValue("$expiration", expirationTimestamp);

        var deletedCount = await command.ExecuteNonQueryAsync();
        Log.Information($"清理过期缓存: 删除 {deletedCount} 条记录（超过 {expirationDays} 天）");

        return deletedCount;
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public async Task<CacheStatistics> GetStatisticsAsync()
    {
        await EnsureInitializedAsync();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT
                COUNT(*) as TotalCount,
                COUNT(DISTINCT target_language) as LanguageCount
            FROM translation_cache
        ";

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new CacheStatistics
            {
                TotalCount = reader.GetInt32(0),
                LanguageCount = reader.GetInt32(1)
            };
        }

        return new CacheStatistics();
    }
}

/// <summary>
/// 缓存统计信息
/// </summary>
public class CacheStatistics
{
    public int TotalCount { get; set; }
    public int LanguageCount { get; set; }
}
