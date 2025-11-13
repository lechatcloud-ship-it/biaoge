using System;
using System.IO;
using System.Windows;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// 缓存管理对话框 - 显示缓存统计和管理选项
    /// </summary>
    public partial class CacheManagementDialog : Window
    {
        private Services.CacheService? _cacheService;

        public CacheManagementDialog()
        {
            InitializeComponent();
            LoadCacheService();
            LoadStatistics();
        }

        /// <summary>
        /// 加载缓存服务
        /// </summary>
        private void LoadCacheService()
        {
            try
            {
                _cacheService = Services.ServiceLocator.GetService<Services.CacheService>();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载缓存服务失败");
            }
        }

        /// <summary>
        /// 加载统计信息
        /// </summary>
        private async void LoadStatistics()
        {
            try
            {
                if (_cacheService == null)
                {
                    DatabaseSizeText.Text = "服务未初始化";
                    CacheCountText.Text = "N/A";
                    HitRateText.Text = "N/A";
                    LastUpdateText.Text = "N/A";
                    DatabasePathText.Text = "N/A";
                    return;
                }

                // 获取统计信息
                var stats = await _cacheService.GetStatisticsAsync();

                // 数据库路径 - 使用反射获取私有字段
                var dbPathField = _cacheService.GetType().GetField("_dbPath",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var dbPath = dbPathField?.GetValue(_cacheService) as string ?? "未知";

                // 数据库大小
                if (File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    var sizeInMB = fileInfo.Length / 1024.0 / 1024.0;
                    DatabaseSizeText.Text = $"{sizeInMB:F2} MB";
                    LastUpdateText.Text = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
                }
                else
                {
                    DatabaseSizeText.Text = "数据库不存在";
                    LastUpdateText.Text = "N/A";
                }

                // 缓存条目数
                CacheCountText.Text = $"{stats.TotalCount:N0} 条";

                // 命中率（注：当前CacheStatistics没有命中率，显示为N/A）
                HitRateText.Text = "统计不可用";

                // 数据库路径
                DatabasePathText.Text = dbPath;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载缓存统计失败");
                MessageBox.Show($"加载统计信息失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清除全部缓存按钮点击事件
        /// </summary>
        private async void ClearAllCacheButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "确定要清除所有缓存吗？\n\n这将删除所有翻译缓存数据，此操作不可恢复。",
                    "确认清除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                if (_cacheService == null)
                {
                    MessageBox.Show("缓存服务未初始化", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 清除缓存
                await _cacheService.ClearCacheAsync();

                MessageBox.Show("所有缓存已清除", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // 刷新统计
                LoadStatistics();

                Log.Information("用户清除了所有缓存");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "清除缓存失败");
                MessageBox.Show($"清除缓存失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清除过期缓存按钮点击事件
        /// </summary>
        private async void ClearExpiredCacheButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_cacheService == null)
                {
                    MessageBox.Show("缓存服务未初始化", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 清除过期缓存（30天前）
                var deletedCount = await _cacheService.CleanExpiredCacheAsync(30);

                MessageBox.Show($"已清除 {deletedCount} 条过期缓存", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // 刷新统计
                LoadStatistics();

                Log.Information("清除了 {Count} 条过期缓存", deletedCount);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "清除过期缓存失败");
                MessageBox.Show($"清除过期缓存失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 优化数据库按钮点击事件
        /// </summary>
        private async void OptimizeDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_cacheService == null)
                {
                    MessageBox.Show("缓存服务未初始化", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 执行VACUUM（注：CacheService没有此方法，暂时显示消息）
                MessageBox.Show("数据库优化功能暂未实现", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;

                MessageBox.Show("数据库优化完成", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // 刷新统计
                LoadStatistics();

                Log.Information("数据库优化完成");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "优化数据库失败");
                MessageBox.Show($"优化数据库失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 刷新按钮点击事件
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadStatistics();
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
