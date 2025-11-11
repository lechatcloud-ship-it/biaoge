using System.Windows;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// SettingsDialog.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsDialog : Window
    {
        public SettingsDialog()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // TODO: 从ConfigManager加载设置
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: 保存设置到ConfigManager
                DialogResult = true;
                Close();
            }
            catch
            {
                MessageBox.Show("保存设置失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
