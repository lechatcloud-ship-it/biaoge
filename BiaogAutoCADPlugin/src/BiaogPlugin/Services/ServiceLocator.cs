using System;
using System.Collections.Generic;
using Serilog;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 简单的服务定位器
    /// 用于管理插件中的单例服务
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private static readonly object _lock = new object();

        /// <summary>
        /// 注册服务
        /// </summary>
        public static void RegisterService<T>(T service) where T : class
        {
            lock (_lock)
            {
                var type = typeof(T);
                if (_services.ContainsKey(type))
                {
                    Log.Warning($"服务 {type.Name} 已存在，将被替换");
                }
                _services[type] = service;
                Log.Debug($"服务已注册: {type.Name}");
            }
        }

        /// <summary>
        /// 获取服务
        /// </summary>
        public static T? GetService<T>() where T : class
        {
            lock (_lock)
            {
                var type = typeof(T);
                if (_services.TryGetValue(type, out var service))
                {
                    return service as T;
                }

                Log.Warning($"服务未找到: {type.Name}");
                return null;
            }
        }

        /// <summary>
        /// 获取或创建服务
        /// </summary>
        public static T GetOrCreateService<T>() where T : class, new()
        {
            lock (_lock)
            {
                var type = typeof(T);
                if (_services.TryGetValue(type, out var service))
                {
                    return (T)service;
                }

                var newService = new T();
                _services[type] = newService;
                Log.Debug($"服务已创建并注册: {type.Name}");
                return newService;
            }
        }

        /// <summary>
        /// 检查服务是否已注册
        /// </summary>
        public static bool IsRegistered<T>() where T : class
        {
            lock (_lock)
            {
                return _services.ContainsKey(typeof(T));
            }
        }

        /// <summary>
        /// 清理所有服务
        /// </summary>
        public static void Cleanup()
        {
            lock (_lock)
            {
                foreach (var service in _services.Values)
                {
                    if (service is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                            Log.Debug($"服务已释放: {service.GetType().Name}");
                        }
                        catch (System.Exception ex)
                        {
                            Log.Error(ex, $"释放服务失败: {service.GetType().Name}");
                        }
                    }
                }

                _services.Clear();
                Log.Information("所有服务已清理");
            }
        }

        /// <summary>
        /// 初始化所有核心服务
        /// </summary>
        public static void InitializeServices()
        {
            try
            {
                Log.Information("开始初始化服务...");

                // 注册配置管理器（需要从BiaogeCSharp复制）
                // RegisterService(new ConfigManager());

                // 注册缓存服务（需要从BiaogeCSharp复制）
                // RegisterService(new CacheService());

                // 注册翻译引擎（需要从BiaogeCSharp复制）
                // RegisterService(new TranslationEngine());

                // 注册百炼API客户端（需要从BiaogeCSharp复制）
                // RegisterService(new BailianApiClient());

                Log.Information("服务初始化完成");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "服务初始化失败");
                throw;
            }
        }
    }
}
