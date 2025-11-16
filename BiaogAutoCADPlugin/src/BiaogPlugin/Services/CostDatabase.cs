using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// ✅ 工程造价数据库服务 - 动态加载JSON配置，替代硬编码单价
    ///
    /// 核心功能：
    /// 1. 从JSON文件加载成本数据库
    /// 2. 支持热重载（配置文件更新后自动刷新）
    /// 3. 模糊匹配构件类型（例如："C30混凝土柱300×600" → "C30混凝土柱"）
    /// 4. 层级查找（先精确匹配，再分类匹配，最后通用匹配）
    /// 5. 线程安全（单例模式 + 读写锁）
    ///
    /// 基于2024年建筑市场平均价格
    /// </summary>
    public class CostDatabase
    {
        private static readonly Lazy<CostDatabase> _instance = new(() => new CostDatabase());
        public static CostDatabase Instance => _instance.Value;

        private readonly object _lock = new();
        private CostDatabaseConfig? _config;
        private Dictionary<string, PriceItem> _flatPriceCache = new();
        private DateTime _lastLoadTime = DateTime.MinValue;
        private string _configFilePath = string.Empty;

        private CostDatabase()
        {
            // 私有构造函数（单例模式）
        }

        /// <summary>
        /// ✅ 初始化成本数据库（加载JSON配置）
        /// </summary>
        public void Initialize(string? configFilePath = null)
        {
            lock (_lock)
            {
                try
                {
                    // 默认配置文件路径
                    if (string.IsNullOrEmpty(configFilePath))
                    {
                        // 优先级1：用户自定义配置（~/.biaoge/cost-database.json）
                        var userConfigPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".biaoge",
                            "cost-database.json"
                        );

                        if (File.Exists(userConfigPath))
                        {
                            configFilePath = userConfigPath;
                            Log.Information("使用用户自定义成本数据库: {Path}", userConfigPath);
                        }
                        else
                        {
                            // 优先级2：默认内置配置（插件目录）
                            var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                            configFilePath = Path.Combine(assemblyPath!, "Config", "cost-database.json");
                            Log.Information("使用默认成本数据库: {Path}", configFilePath);
                        }
                    }

                    _configFilePath = configFilePath;

                    if (!File.Exists(configFilePath))
                    {
                        Log.Warning("成本数据库文件不存在: {Path}，将使用内置默认价格", configFilePath);
                        LoadDefaultPrices();
                        return;
                    }

                    // 加载JSON配置
                    var jsonString = File.ReadAllText(configFilePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };

                    _config = JsonSerializer.Deserialize<CostDatabaseConfig>(jsonString, options);

                    if (_config == null || _config.PriceData == null)
                    {
                        Log.Error("成本数据库解析失败，配置文件格式错误");
                        LoadDefaultPrices();
                        return;
                    }

                    // 构建扁平化价格缓存（加速查找）
                    BuildFlatPriceCache();

                    _lastLoadTime = DateTime.Now;

                    Log.Information("✅ 成本数据库加载成功: 版本{Version}, {Count}个价格项",
                        _config.Version, _flatPriceCache.Count);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "加载成本数据库失败");
                    LoadDefaultPrices();
                }
            }
        }

        /// <summary>
        /// ✅ 查询构件单价（支持模糊匹配）
        /// </summary>
        /// <param name="componentType">构件类型（如："C30混凝土柱"、"C30混凝土柱300×600"）</param>
        /// <returns>价格项（含单价、单位、描述）；如果未找到返回null</returns>
        public PriceItem? GetPrice(string componentType)
        {
            if (string.IsNullOrWhiteSpace(componentType))
                return null;

            lock (_lock)
            {
                // 检查是否需要重新加载（文件是否被修改）
                CheckAndReload();

                // 策略1：精确匹配（优先级最高）
                if (_flatPriceCache.TryGetValue(componentType, out var exactMatch))
                {
                    Log.Debug("精确匹配成本: {Type} → {Price}{Unit}", componentType, exactMatch.Price, exactMatch.Unit);
                    return exactMatch;
                }

                // 策略2：前缀模糊匹配（例如："C30混凝土柱300×600" → "C30混凝土柱"）
                var prefixMatch = _flatPriceCache
                    .Where(kv => componentType.StartsWith(kv.Key))
                    .OrderByDescending(kv => kv.Key.Length)  // 选择最长匹配
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(prefixMatch.Key))
                {
                    Log.Debug("前缀匹配成本: {Type} → {Key} → {Price}{Unit}",
                        componentType, prefixMatch.Key, prefixMatch.Value.Price, prefixMatch.Value.Unit);
                    return prefixMatch.Value;
                }

                // 策略3：包含匹配（例如："混凝土柱C30" → "C30混凝土柱"）
                var containsMatch = _flatPriceCache
                    .Where(kv => componentType.Contains(kv.Key) || kv.Key.Contains(componentType))
                    .OrderByDescending(kv => kv.Key.Length)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(containsMatch.Key))
                {
                    Log.Debug("包含匹配成本: {Type} → {Key} → {Price}{Unit}",
                        componentType, containsMatch.Key, containsMatch.Value.Price, containsMatch.Value.Unit);
                    return containsMatch.Value;
                }

                // 策略4：关键词匹配（提取构件基础类型）
                var baseType = ExtractBaseType(componentType);
                if (!string.IsNullOrEmpty(baseType) && _flatPriceCache.TryGetValue(baseType, out var baseMatch))
                {
                    Log.Debug("基础类型匹配成本: {Type} → {BaseType} → {Price}{Unit}",
                        componentType, baseType, baseMatch.Price, baseMatch.Unit);
                    return baseMatch;
                }

                Log.Debug("未找到成本数据: {Type}", componentType);
                return null;
            }
        }

        /// <summary>
        /// ✅ 提取构件基础类型（用于模糊匹配）
        /// 例如："C30混凝土柱300×600" → "柱"
        /// </summary>
        private string ExtractBaseType(string componentType)
        {
            var baseTypes = new[] { "柱", "梁", "板", "墙", "基础", "门", "窗", "钢筋", "砖", "砌块" };

            foreach (var baseType in baseTypes)
            {
                if (componentType.Contains(baseType))
                {
                    return baseType;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// ✅ 构建扁平化价格缓存（加速查找）
        /// </summary>
        private void BuildFlatPriceCache()
        {
            _flatPriceCache.Clear();

            if (_config?.PriceData == null)
                return;

            foreach (var category in _config.PriceData)
            {
                foreach (var item in category.Value)
                {
                    _flatPriceCache[item.Key] = item.Value;
                }
            }

            Log.Debug("构建价格缓存完成: {Count}个价格项", _flatPriceCache.Count);
        }

        /// <summary>
        /// ✅ 检查配置文件是否被修改，如需要则重新加载
        /// </summary>
        private void CheckAndReload()
        {
            if (string.IsNullOrEmpty(_configFilePath) || !File.Exists(_configFilePath))
                return;

            var fileInfo = new FileInfo(_configFilePath);
            if (fileInfo.LastWriteTime > _lastLoadTime)
            {
                Log.Information("检测到成本数据库文件已更新，重新加载...");
                Initialize(_configFilePath);
            }
        }

        /// <summary>
        /// ✅ 加载内置默认价格（当配置文件不存在时的后备方案）
        /// </summary>
        private void LoadDefaultPrices()
        {
            _flatPriceCache = new Dictionary<string, PriceItem>
            {
                ["C30混凝土柱"] = new() { Price = 500.0m, Unit = "m³", Description = "C30混凝土柱（内置默认）" },
                ["C35混凝土梁"] = new() { Price = 550.0m, Unit = "m³", Description = "C35混凝土梁（内置默认）" },
                ["C30混凝土板"] = new() { Price = 450.0m, Unit = "m³", Description = "C30混凝土板（内置默认）" },
                ["HRB400钢筋"] = new() { Price = 4500.0m, Unit = "吨", Description = "HRB400钢筋（内置默认）" },
                ["MU10砖墙"] = new() { Price = 200.0m, Unit = "m²", Description = "MU10砖墙（内置默认）" },
                ["柱"] = new() { Price = 500.0m, Unit = "m³", Description = "混凝土柱（内置默认）" },
                ["梁"] = new() { Price = 540.0m, Unit = "m³", Description = "混凝土梁（内置默认）" },
                ["板"] = new() { Price = 450.0m, Unit = "m³", Description = "混凝土板（内置默认）" },
                ["墙"] = new() { Price = 200.0m, Unit = "m²", Description = "砖墙（内置默认）" }
            };

            Log.Information("使用内置默认价格: {Count}个价格项", _flatPriceCache.Count);
        }

        /// <summary>
        /// 获取所有价格数据（用于调试/导出）
        /// </summary>
        public Dictionary<string, PriceItem> GetAllPrices()
        {
            lock (_lock)
            {
                return new Dictionary<string, PriceItem>(_flatPriceCache);
            }
        }

        /// <summary>
        /// 获取数据库元数据
        /// </summary>
        public CostDatabaseMetadata? GetMetadata()
        {
            lock (_lock)
            {
                return _config?.Metadata;
            }
        }
    }

    #region 数据模型

    /// <summary>
    /// 成本数据库配置（对应JSON文件结构）
    /// </summary>
    public class CostDatabaseConfig
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("lastUpdated")]
        public string LastUpdated { get; set; } = "";

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "CNY";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("priceData")]
        public Dictionary<string, Dictionary<string, PriceItem>>? PriceData { get; set; }

        [JsonPropertyName("metadata")]
        public CostDatabaseMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// 价格项
    /// </summary>
    public class PriceItem
    {
        [JsonPropertyName("unit")]
        public string Unit { get; set; } = "";

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// 数据库元数据
    /// </summary>
    public class CostDatabaseMetadata
    {
        [JsonPropertyName("dataSource")]
        public string DataSource { get; set; } = "";

        [JsonPropertyName("priceType")]
        public string PriceType { get; set; } = "";

        [JsonPropertyName("taxRate")]
        public string TaxRate { get; set; } = "";

        [JsonPropertyName("note")]
        public string Note { get; set; } = "";
    }

    #endregion
}
