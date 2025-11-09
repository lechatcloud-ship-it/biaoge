using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BiaogeCSharp.ViewModels;
using System.Linq;

namespace BiaogeCSharp.Views;

public partial class HomePage : UserControl
{
    private Border? _emptyStateOverlay;

    public HomePage()
    {
        InitializeComponent();

        // 设置拖放事件处理器
        AddHandler(DragDrop.DropEvent, OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _emptyStateOverlay = this.FindControl<Border>("EmptyStateOverlay");
    }

    private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
    {
        // 触发主窗口的打开文件命令
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.OpenDwgFileCommand.ExecuteAsync(null);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        // 检查是否包含文件
        if (e.Data.Contains(DataFormats.Files))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        // 拖入时高亮显示拖放区域
        if (_emptyStateOverlay != null && e.Data.Contains(DataFormats.Files))
        {
            // 可以添加视觉反馈，例如改变边框颜色
            e.Handled = true;
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
    {
        // 拖出时恢复原样
        if (_emptyStateOverlay != null)
        {
            // 恢复原始样式
            e.Handled = true;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
        {
            var files = e.Data.GetFiles()?.ToList();
            if (files != null && files.Count > 0)
            {
                var firstFile = files[0].Path.LocalPath;

                // 检查是否是DWG或DXF文件
                if (firstFile.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) ||
                    firstFile.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
                {
                    // 使用ViewModel加载文件
                    if (DataContext is MainWindowViewModel viewModel)
                    {
                        // 这里可以直接调用加载文件的方法
                        // 需要在ViewModel中添加一个接受文件路径的方法
                        await viewModel.OpenDwgFileCommand.ExecuteAsync(null);
                    }
                }
            }
            e.Handled = true;
        }
    }
}
