using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using BiaogPlugin.Services;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// SettingsDialog.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsDialog : Window
    {
        private readonly ConfigManager _configManager;
        private readonly BailianApiClient _bailianClient;

        public SettingsDialog()
        {
            InitializeComponent();

            // 从ServiceLocator获取服务
            _configManager = ServiceLocator.GetService<ConfigManager>()!;
            _bailianClient = ServiceLocator.GetService<BailianApiClient>()!;

            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                // 加载API密钥
                var apiKey = _configManager.GetString("Bailian:ApiKey");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    ApiKeyPasswordBox.Password = apiKey;
                }

                // 模型配置已内置，无需用户修改（对外隐藏）

                // 加载翻译设置
                UseCacheCheckBox.IsChecked = _configManager.GetBool("Translation:UseCache", true);
                SkipNumbersCheckBox.IsChecked = _configManager.GetBool("Translation:SkipNumbers", true);
                SkipShortTextCheckBox.IsChecked = _configManager.GetBool("Translation:SkipShortText", true);

                // 显示配置文件路径
                var configPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".biaoge",
                    "config.json"
                );
                ConfigPathText.Text = configPath;

                Log.Debug("设置已加载");
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "加载设置失败");
                MessageBox.Show($"加载设置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadModelSelection(string configKey, ComboBox comboBox, string defaultModel)
        {
            var selectedModel = _configManager.GetString(configKey, defaultModel);
            var modelItem = comboBox.Items
                .Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == selectedModel);
            if (modelItem != null)
            {
                comboBox.SelectedItem = modelItem;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 保存API密钥
                var apiKey = ApiKeyPasswordBox.Password;
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    Log.Information("开始保存API密钥，长度: {Length}", apiKey.Length);
                    _configManager.SetConfig("Bailian:ApiKey", apiKey);

                    // 刷新BailianApiClient的API密钥
                    _bailianClient.RefreshApiKey();

                    // ✅ 验证密钥是否真的保存成功
                    var savedKey = _configManager.GetString("Bailian:ApiKey");
                    if (string.IsNullOrEmpty(savedKey))
                    {
                        throw new System.Exception("API密钥保存后读取为空！");
                    }
                    Log.Information("✅ API密钥保存成功，验证读取长度: {Length}", savedKey.Length);
                }

                // 模型配置已内置，无需用户修改（对外隐藏）

                // 保存翻译设置
                _configManager.SetConfig("Translation:UseCache", UseCacheCheckBox.IsChecked ?? true);
                _configManager.SetConfig("Translation:SkipNumbers", SkipNumbersCheckBox.IsChecked ?? true);
                _configManager.SetConfig("Translation:SkipShortText", SkipShortTextCheckBox.IsChecked ?? true);

                Log.Information("✅ 所有设置已保存");

                // 显示配置文件路径，帮助用户验证
                var configPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".biaoge",
                    "config.json"
                );

                MessageBox.Show(
                    $"设置保存成功！\n\n" +
                    $"配置文件位置：\n{configPath}\n\n" +
                    $"提示：重启AutoCAD后不会再弹出密钥输入框。",
                    "保存成功",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "❌ 保存设置失败");
                MessageBox.Show(
                    $"保存设置失败！\n\n" +
                    $"错误信息：{ex.Message}\n\n" +
                    $"请查看日志文件获取详细信息：\n" +
                    $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}\\.biaoge\\logs\\",
                    "保存失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SaveModelSelection(string configKey, ComboBox comboBox)
        {
            var selectedModel = (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            if (!string.IsNullOrEmpty(selectedModel))
            {
                _configManager.SetConfig(configKey, selectedModel);
                Log.Debug($"保存模型配置: {configKey} = {selectedModel}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TestConnectionButton.IsEnabled = false;
                TestResultText.Text = "测试中...";
                TestResultText.Foreground = System.Windows.Media.Brushes.Yellow;

                // 临时保存API密钥用于测试
                var apiKey = ApiKeyPasswordBox.Password;
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    TestResultText.Text = "请先输入API密钥";
                    TestResultText.Foreground = System.Windows.Media.Brushes.Red;
                    return;
                }

                _configManager.SetConfig("Bailian:ApiKey", apiKey);
                _bailianClient.RefreshApiKey();

                // 测试连接
                bool success = await _bailianClient.TestConnectionAsync();

                if (success)
                {
                    TestResultText.Text = "✓ 连接成功";
                    TestResultText.Foreground = System.Windows.Media.Brushes.LimeGreen;
                    Log.Information("API连接测试成功");
                }
                else
                {
                    TestResultText.Text = "✗ 连接失败，请检查API密钥";
                    TestResultText.Foreground = System.Windows.Media.Brushes.Red;
                    Log.Warning("API连接测试失败");
                }
            }
            catch (System.Exception ex)
            {
                TestResultText.Text = $"✗ 测试失败: {ex.Message}";
                TestResultText.Foreground = System.Windows.Media.Brushes.Red;
                Log.Error(ex, "API连接测试异常");
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
            }
        }
    }
}
