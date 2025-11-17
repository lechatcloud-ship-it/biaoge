using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using BiaogPlugin.Services;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// ✅ 成本管理对话框 - 让用户手动管理价格数据
    /// </summary>
    public partial class CostManagementDialog : Window
    {
        private readonly ConfigManager _configManager;
        private string _costDatabasePath = "";

        public CostManagementDialog()
        {
            InitializeComponent();

            // 从ServiceLocator获取服务
            _configManager = ServiceLocator.GetService<ConfigManager>()!;

            LoadSettings();
            LoadDatabaseInfo();
        }

        /// <summary>
        /// 加载配置设置
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                var config = _configManager.Config;

                // 加载成本配置
                EnableCostCheckBox.IsChecked = config.Cost.EnableCostEstimation;
                ShowWarningCheckBox.IsChecked = config.Cost.ShowCostWarning;
                DataSourceTextBox.Text = config.Cost.PriceDataSource;

                // 加载地区选择
                var regionItem = RegionComboBox.Items
                    .Cast<ComboBoxItem>()
                    .FirstOrDefault(item => item.Tag?.ToString() == config.Cost.CurrentRegion);
                if (regionItem != null)
                {
                    RegionComboBox.SelectedItem = regionItem;
                }

                // 加载最后更新日期
                if (!string.IsNullOrEmpty(config.Cost.LastPriceUpdate) &&
                    DateTime.TryParse(config.Cost.LastPriceUpdate, out var lastUpdate))
                {
                    LastUpdateDatePicker.SelectedDate = lastUpdate;
                }
                else
                {
                    LastUpdateDatePicker.SelectedDate = DateTime.Now;
                }

                Log.Debug("成本管理设置已加载");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载成本管理设置失败");
                MessageBox.Show($"加载设置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 加载数据库信息
        /// </summary>
        private void LoadDatabaseInfo()
        {
            try
            {
                // 获取数据库路径
                var userConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".biaoge",
                    "cost-database.json"
                );

                if (File.Exists(userConfigPath))
                {
                    _costDatabasePath = userConfigPath;
                }
                else
                {
                    // 使用默认配置
                    var assemblyPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    _costDatabasePath = Path.Combine(assemblyPath!, "Config", "cost-database.json");
                }

                // 从CostDatabase获取元数据
                var metadata = CostDatabase.Instance.GetMetadata();
                var allPrices = CostDatabase.Instance.GetAllPrices();

                if (metadata != null)
                {
                    VersionText.Text = "1.0.0";
                    DataSourceInfoText.Text = metadata.DataSource;
                    RegionalVariationText.Text = metadata.RegionalVariation;
                    ApiAvailabilityText.Text = metadata.ApiAvailability;
                    PriceCountText.Text = $"{allPrices.Count} 个价格项";

                    // 显示地区价格示例
                    RegionalPriceExamplesText.Text =
                        "C30混凝土柱价格参考：\n" +
                        "• 华北（北京）: 550-600元/m³\n" +
                        "• 华东（上海）: 530-580元/m³\n" +
                        "• 华南（广州）: 510-560元/m³\n" +
                        "• 西南（成都）: 450-500元/m³\n" +
                        "• 西部（昆明）: 380-420元/m³\n" +
                        "• 本表参考价: 500元/m³（全国平均）";
                }

                Log.Debug("数据库信息已加载: {Path}", _costDatabasePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "加载数据库信息失败");
                MessageBox.Show($"加载数据库信息失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 启用成本估算复选框状态改变
        /// </summary>
        private void EnableCostCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // 如果用户尝试启用成本估算，显示警告
            if (EnableCostCheckBox.IsChecked == true)
            {
                var result = MessageBox.Show(
                    "⚠️ 重要提醒：\n\n" +
                    "1. 中国无公开工程造价API，所有价格数据需您自行维护\n" +
                    "2. 地区差异巨大（一线城市比西部高30-50%）\n" +
                    "3. 价格随材料市场波动，需定期更新\n" +
                    "4. 正式预算必须使用当地定额和造价咨询资质单位出具的报告\n\n" +
                    "启用前请确保已理解上述说明，并自行维护价格数据。\n\n" +
                    "是否确认启用成本估算？",
                    "⚠️ 确认启用成本估算",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    EnableCostCheckBox.IsChecked = false;
                }
            }
        }

        /// <summary>
        /// 打开价格数据库文件
        /// </summary>
        private void OpenDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(_costDatabasePath))
                {
                    MessageBox.Show($"数据库文件不存在: {_costDatabasePath}", "错误",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // 使用默认程序打开JSON文件
                Process.Start(new ProcessStartInfo
                {
                    FileName = _costDatabasePath,
                    UseShellExecute = true
                });

                Log.Information("打开价格数据库文件: {Path}", _costDatabasePath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "打开价格数据库文件失败");
                MessageBox.Show($"打开文件失败: {ex.Message}\n\n文件路径: {_costDatabasePath}",
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 重新加载数据库
        /// </summary>
        private void ReloadDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 重新初始化数据库
                CostDatabase.Instance.Initialize(_costDatabasePath);

                // 重新加载信息
                LoadDatabaseInfo();

                MessageBox.Show("价格数据库已重新加载！", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                Log.Information("价格数据库已重新加载");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "重新加载数据库失败");
                MessageBox.Show($"重新加载失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 保存按钮点击
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var config = _configManager.Config;

                // 保存成本配置
                config.Cost.EnableCostEstimation = EnableCostCheckBox.IsChecked ?? false;
                config.Cost.ShowCostWarning = ShowWarningCheckBox.IsChecked ?? true;
                config.Cost.PriceDataSource = DataSourceTextBox.Text;

                // 保存地区
                var selectedRegion = RegionComboBox.SelectedItem as ComboBoxItem;
                if (selectedRegion != null)
                {
                    config.Cost.CurrentRegion = selectedRegion.Tag?.ToString() ?? "华东";
                }

                // 保存最后更新日期
                if (LastUpdateDatePicker.SelectedDate.HasValue)
                {
                    config.Cost.LastPriceUpdate = LastUpdateDatePicker.SelectedDate.Value.ToString("yyyy-MM-dd");
                }

                // 保存配置文件
                _configManager.SaveConfig();

                Log.Information("成本管理设置已保存");
                MessageBox.Show("设置已保存！", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存成本管理设置失败");
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
