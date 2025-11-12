using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.AutoCAD.DatabaseServices;
using Microsoft.Data.Sqlite;
using Serilog;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 翻译历史记录
    /// 记录所有翻译操作，支持查询和撤销
    /// ✅ 优化：使用连接池化 + 异步延迟初始化 + 批量插入
    /// </summary>
    public class TranslationHistory
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly int _maxRecords;
        private bool _initialized = false;
        private readonly System.Threading.SemaphoreSlim _initLock = new(1, 1);

        /// <summary>
        /// 历史记录条目
        /// </summary>
        public class HistoryRecord
        {
            public long Id { get; set; }
            public DateTime Timestamp { get; set; }
            public string ObjectIdHandle { get; set; } = "";
            public string OriginalText { get; set; } = "";
            public string TranslatedText { get; set; } = "";
            public string SourceLanguage { get; set; } = "";
            public string TargetLanguage { get; set; } = "";
            public string EntityType { get; set; } = "";
            public string Layer { get; set; } = "";
            public string Operation { get; set; } = "translate"; // translate, undo, redo
        }

        public TranslationHistory(int maxRecords = 1000)
        {
            var appDataPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".biaoge"
            );
            System.IO.Directory.CreateDirectory(appDataPath);
            _dbPath = System.IO.Path.Combine(appDataPath, "history.db");
            _maxRecords = maxRecords;

            // ✅ 优化：使用连接字符串池化
            _connectionString = $"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared;Pooling=True";

            Log.Debug("TranslationHistory已构造，延迟初始化数据库");
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

                await using var createTableCmd = connection.CreateCommand();
                createTableCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS translation_history (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        timestamp TEXT NOT NULL,
                        object_id_handle TEXT NOT NULL,
                        original_text TEXT NOT NULL,
                        translated_text TEXT NOT NULL,
                        source_language TEXT NOT NULL,
                        target_language TEXT NOT NULL,
                        entity_type TEXT NOT NULL,
                        layer TEXT NOT NULL,
                        operation TEXT NOT NULL DEFAULT 'translate'
                    );

                    CREATE INDEX IF NOT EXISTS idx_timestamp ON translation_history(timestamp DESC);
                    CREATE INDEX IF NOT EXISTS idx_object_id ON translation_history(object_id_handle);
                    CREATE INDEX IF NOT EXISTS idx_operation ON translation_history(operation);
                ";
                await createTableCmd.ExecuteNonQueryAsync();

                _initialized = true;
                Log.Information("翻译历史数据库初始化成功: {DbPath}", _dbPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "初始化翻译历史数据库失败");
                throw;
            }
            finally
            {
                _initLock.Release();
            }
        }

        /// <summary>
        /// 添加翻译记录
        /// </summary>
        public async Task AddRecordAsync(HistoryRecord record)
        {
            try
            {
                await EnsureInitializedAsync();

                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                await using var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO translation_history
                    (timestamp, object_id_handle, original_text, translated_text,
                     source_language, target_language, entity_type, layer, operation)
                    VALUES
                    (@timestamp, @object_id_handle, @original_text, @translated_text,
                     @source_language, @target_language, @entity_type, @layer, @operation)
                ";

                insertCmd.Parameters.AddWithValue("@timestamp", record.Timestamp.ToString("o"));
                insertCmd.Parameters.AddWithValue("@object_id_handle", record.ObjectIdHandle);
                insertCmd.Parameters.AddWithValue("@original_text", record.OriginalText);
                insertCmd.Parameters.AddWithValue("@translated_text", record.TranslatedText);
                insertCmd.Parameters.AddWithValue("@source_language", record.SourceLanguage);
                insertCmd.Parameters.AddWithValue("@target_language", record.TargetLanguage);
                insertCmd.Parameters.AddWithValue("@entity_type", record.EntityType);
                insertCmd.Parameters.AddWithValue("@layer", record.Layer);
                insertCmd.Parameters.AddWithValue("@operation", record.Operation);

                await insertCmd.ExecuteNonQueryAsync();

                // 清理旧记录
                await CleanupOldRecordsAsync(connection);

                Log.Debug($"添加翻译历史记录: {record.OriginalText} -> {record.TranslatedText}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "添加翻译历史记录失败");
            }
        }

        /// <summary>
        /// ✅ 优化：批量添加记录（使用批量INSERT提高性能）
        /// </summary>
        public async Task AddRecordsAsync(List<HistoryRecord> records)
        {
            if (records == null || records.Count == 0) return;

            try
            {
                await EnsureInitializedAsync();

                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();

                using var transaction = connection.BeginTransaction();

                // ✅ 优化：批量插入（每批100条）
                const int batchSize = 100;
                for (int i = 0; i < records.Count; i += batchSize)
                {
                    var batch = records.Skip(i).Take(batchSize).ToList();

                    await using var insertCmd = connection.CreateCommand();
                    insertCmd.Transaction = transaction;

                    // 构建批量INSERT语句
                    var valueClauses = new List<string>();
                    for (int j = 0; j < batch.Count; j++)
                    {
                        valueClauses.Add($"(@ts{j}, @oid{j}, @orig{j}, @trans{j}, @sl{j}, @tl{j}, @et{j}, @l{j}, @op{j})");

                        var record = batch[j];
                        insertCmd.Parameters.AddWithValue($"@ts{j}", record.Timestamp.ToString("o"));
                        insertCmd.Parameters.AddWithValue($"@oid{j}", record.ObjectIdHandle);
                        insertCmd.Parameters.AddWithValue($"@orig{j}", record.OriginalText);
                        insertCmd.Parameters.AddWithValue($"@trans{j}", record.TranslatedText);
                        insertCmd.Parameters.AddWithValue($"@sl{j}", record.SourceLanguage);
                        insertCmd.Parameters.AddWithValue($"@tl{j}", record.TargetLanguage);
                        insertCmd.Parameters.AddWithValue($"@et{j}", record.EntityType);
                        insertCmd.Parameters.AddWithValue($"@l{j}", record.Layer);
                        insertCmd.Parameters.AddWithValue($"@op{j}", record.Operation);
                    }

                    insertCmd.CommandText = $@"
                        INSERT INTO translation_history
                        (timestamp, object_id_handle, original_text, translated_text,
                         source_language, target_language, entity_type, layer, operation)
                        VALUES {string.Join(", ", valueClauses)}
                    ";

                    await insertCmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();

                // 清理旧记录
                await CleanupOldRecordsAsync(connection);

                Log.Information($"批量添加翻译历史记录: {records.Count} 条");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "批量添加翻译历史记录失败");
            }
        }

        /// <summary>
        /// 获取最近的翻译记录
        /// </summary>
        public async Task<List<HistoryRecord>> GetRecentRecordsAsync(int limit = 100)
        {
            var records = new List<HistoryRecord>();

            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();

                var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = @"
                    SELECT id, timestamp, object_id_handle, original_text, translated_text,
                           source_language, target_language, entity_type, layer, operation
                    FROM translation_history
                    ORDER BY timestamp DESC
                    LIMIT @limit
                ";
                selectCmd.Parameters.AddWithValue("@limit", limit);

                using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    records.Add(new HistoryRecord
                    {
                        Id = reader.GetInt64(0),
                        Timestamp = DateTime.Parse(reader.GetString(1)),
                        ObjectIdHandle = reader.GetString(2),
                        OriginalText = reader.GetString(3),
                        TranslatedText = reader.GetString(4),
                        SourceLanguage = reader.GetString(5),
                        TargetLanguage = reader.GetString(6),
                        EntityType = reader.GetString(7),
                        Layer = reader.GetString(8),
                        Operation = reader.GetString(9)
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取翻译历史记录失败");
            }

            return records;
        }

        /// <summary>
        /// 根据ObjectId查询历史记录
        /// </summary>
        public async Task<List<HistoryRecord>> GetRecordsByObjectIdAsync(string objectIdHandle)
        {
            var records = new List<HistoryRecord>();

            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();

                var selectCmd = connection.CreateCommand();
                selectCmd.CommandText = @"
                    SELECT id, timestamp, object_id_handle, original_text, translated_text,
                           source_language, target_language, entity_type, layer, operation
                    FROM translation_history
                    WHERE object_id_handle = @object_id_handle
                    ORDER BY timestamp DESC
                ";
                selectCmd.Parameters.AddWithValue("@object_id_handle", objectIdHandle);

                using var reader = await selectCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    records.Add(new HistoryRecord
                    {
                        Id = reader.GetInt64(0),
                        Timestamp = DateTime.Parse(reader.GetString(1)),
                        ObjectIdHandle = reader.GetString(2),
                        OriginalText = reader.GetString(3),
                        TranslatedText = reader.GetString(4),
                        SourceLanguage = reader.GetString(5),
                        TargetLanguage = reader.GetString(6),
                        EntityType = reader.GetString(7),
                        Layer = reader.GetString(8),
                        Operation = reader.GetString(9)
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "根据ObjectId查询历史记录失败");
            }

            return records;
        }

        /// <summary>
        /// 清理旧记录（保留最近的N条）
        /// </summary>
        private async Task CleanupOldRecordsAsync(SqliteConnection connection)
        {
            try
            {
                var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = @"
                    DELETE FROM translation_history
                    WHERE id NOT IN (
                        SELECT id FROM translation_history
                        ORDER BY timestamp DESC
                        LIMIT @max_records
                    )
                ";
                deleteCmd.Parameters.AddWithValue("@max_records", _maxRecords);

                var deletedCount = await deleteCmd.ExecuteNonQueryAsync();
                if (deletedCount > 0)
                {
                    Log.Debug($"清理旧翻译历史记录: {deletedCount} 条");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "清理旧翻译历史记录失败");
            }
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public async Task<Dictionary<string, object>> GetStatisticsAsync()
        {
            var stats = new Dictionary<string, object>();

            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();

                // 总记录数
                var countCmd = connection.CreateCommand();
                countCmd.CommandText = "SELECT COUNT(*) FROM translation_history";
                stats["TotalRecords"] = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

                // 今天的记录数
                var todayCmd = connection.CreateCommand();
                todayCmd.CommandText = @"
                    SELECT COUNT(*) FROM translation_history
                    WHERE date(timestamp) = date('now')
                ";
                stats["TodayRecords"] = Convert.ToInt32(await todayCmd.ExecuteScalarAsync());

                // 最常翻译的语言对
                var langCmd = connection.CreateCommand();
                langCmd.CommandText = @"
                    SELECT source_language || ' -> ' || target_language as lang_pair, COUNT(*) as count
                    FROM translation_history
                    WHERE operation = 'translate'
                    GROUP BY lang_pair
                    ORDER BY count DESC
                    LIMIT 5
                ";

                var topLangPairs = new List<string>();
                using var reader = await langCmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    topLangPairs.Add($"{reader.GetString(0)} ({reader.GetInt32(1)}次)");
                }
                stats["TopLanguagePairs"] = topLangPairs;

                // 最早记录时间
                var firstCmd = connection.CreateCommand();
                firstCmd.CommandText = "SELECT MIN(timestamp) FROM translation_history";
                var firstTimestamp = await firstCmd.ExecuteScalarAsync();
                if (firstTimestamp != null && firstTimestamp != DBNull.Value)
                {
                    stats["FirstRecord"] = DateTime.Parse(firstTimestamp.ToString()!);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "获取翻译历史统计信息失败");
            }

            return stats;
        }

        /// <summary>
        /// 清除所有历史记录
        /// </summary>
        public async Task ClearAllAsync()
        {
            try
            {
                using var connection = new SqliteConnection($"Data Source={_dbPath}");
                await connection.OpenAsync();

                var deleteCmd = connection.CreateCommand();
                deleteCmd.CommandText = "DELETE FROM translation_history";
                await deleteCmd.ExecuteNonQueryAsync();

                Log.Information("已清除所有翻译历史记录");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "清除翻译历史记录失败");
                throw;
            }
        }
    }
}
