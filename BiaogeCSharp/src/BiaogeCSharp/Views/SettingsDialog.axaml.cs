using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace BiaogeCSharp.Views;

/// <summary>
/// 设置对话框 - 对应Python版本的SettingsDialog
/// 包含6个选项卡：阿里云百炼、翻译设置、性能优化、界面设置、数据管理、高级
/// </summary>
public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
        InitializeControls();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeControls()
    {
        var applyButton = this.FindControl<Button>("ApplyButton");
        var okButton = this.FindControl<Button>("OkButton");
        var cancelButton = this.FindControl<Button>("CancelButton");

        if (applyButton != null)
            applyButton.Click += (s, e) => ApplySettings();

        if (okButton != null)
            okButton.Click += (s, e) =>
            {
                ApplySettings();
                Close();
            };

        if (cancelButton != null)
            cancelButton.Click += (s, e) => Close();
    }

    private void ApplySettings()
    {
        // TODO: 应用设置到ConfigManager
        // 这里将实现保存所有设置到配置文件的逻辑
    }
}
