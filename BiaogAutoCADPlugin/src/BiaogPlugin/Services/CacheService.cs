using Microsoft.Data.Sqlite;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace BiaogPlugin.Services;

/// <summary>
/// 翻译缓存服务（SQLite）
/// </summary>
public class CacheService
{
    private readonly string _dbPath;

    public CacheService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".biaoge"
        );
        Directory.CreateDirectory(appDataPath);

        _dbPath = Path.Combine(appDataPath, "cache.db");

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS translation_cache (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_text TEXT NOT NULL,
                target_language TEXT NOT NULL,
                translated_text TEXT NOT NULL,
                created_at INTEGER NOT NULL,
                UNIQUE(source_text, target_language)
            )
        ";
        command.ExecuteNonQuery();

        Log.Information("缓存数据库初始化完成: {DbPath}", _dbPath);
    }

    /// <summary>
    /// 获取翻译缓存
    /// </summary>
    public async Task<string?> GetTranslationAsync(string sourceText, string targetLanguage)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT translated_text
            FROM translation_cache
            WHERE source_text = $source_text AND target_language = $target_language
        ";
        command.Parameters.AddWithValue("$source_text", sourceText);
        command.Parameters.AddWithValue("$target_language", targetLanguage);

        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }

    /// <summary>
    /// 设置翻译缓存
    /// </summary>
    public async Task SetTranslationAsync(string sourceText, string targetLanguage, string translatedText)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
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
    /// 清理缓存
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM translation_cache";
        await command.ExecuteNonQueryAsync();

        Log.Information("缓存已清空");
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    public async Task<CacheStatistics> GetStatisticsAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
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
