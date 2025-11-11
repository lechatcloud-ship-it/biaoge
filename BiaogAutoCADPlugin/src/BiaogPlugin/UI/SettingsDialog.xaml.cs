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

                // 加载翻译模型
                var translationModel = _configManager.GetString("Bailian:TextTranslationModel", "qwen-mt-plus");
                var modelItem = TranslationModelComboBox.Items
                    .Cast<ComboBoxItem>()
                    .FirstOrDefault(item => item.Tag?.ToString() == translationModel);
                if (modelItem != null)
                {
                    TranslationModelComboBox.SelectedItem = modelItem;
                }

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
            catch (Exception ex)
            {
                Log.Error(ex, "加载设置失败");
                MessageBox.Show($"加载设置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                    _configManager.SetConfig("Bailian:ApiKey", apiKey);

                    // 刷新BailianApiClient的API密钥
                    _bailianClient.RefreshApiKey();
                }

                // 保存翻译模型
                var selectedModel = (TranslationModelComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
                if (!string.IsNullOrEmpty(selectedModel))
                {
                    _configManager.SetConfig("Bailian:TextTranslationModel", selectedModel);
                }

                // 保存翻译设置
                _configManager.SetConfig("Translation:UseCache", UseCacheCheckBox.IsChecked ?? true);
                _configManager.SetConfig("Translation:SkipNumbers", SkipNumbersCheckBox.IsChecked ?? true);
                _configManager.SetConfig("Translation:SkipShortText", SkipShortTextCheckBox.IsChecked ?? true);

                Log.Information("设置已保存");
                MessageBox.Show("设置保存成功！", "成功",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "保存设置失败");
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
            catch (Exception ex)
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
