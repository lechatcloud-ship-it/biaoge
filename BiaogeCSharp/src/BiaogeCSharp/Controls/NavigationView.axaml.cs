using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Material.Icons;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BiaogeCSharp.Controls;

public partial class NavigationView : UserControl
{
    private ListBox _topNavigationList;
    private ListBox _bottomNavigationList;
    private ContentControl _contentArea;

    private readonly ObservableCollection<NavigationItem> _topItems = new();
    private readonly ObservableCollection<NavigationItem> _bottomItems = new();

    public NavigationView()
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
        _topNavigationList = this.FindControl<ListBox>("TopNavigationList")!;
        _bottomNavigationList = this.FindControl<ListBox>("BottomNavigationList")!;
        _contentArea = this.FindControl<ContentControl>("ContentArea")!;

        _topNavigationList.ItemsSource = _topItems;
        _bottomNavigationList.ItemsSource = _bottomItems;
    }

    /// <summary>
    /// 添加顶部导航项
    /// </summary>
    public void AddTopNavigationItem(string text, MaterialIconKind iconKind, Control content)
    {
        var item = new NavigationItem
        {
            Text = text,
            IconKind = iconKind,
            Content = content
        };
        _topItems.Add(item);

        // 如果是第一项，自动选中
        if (_topItems.Count == 1)
        {
            _topNavigationList.SelectedIndex = 0;
            _contentArea.Content = content;
        }
    }

    /// <summary>
    /// 添加底部导航项
    /// </summary>
    public void AddBottomNavigationItem(string text, MaterialIconKind iconKind, Control content)
    {
        var item = new NavigationItem
        {
            Text = text,
            IconKind = iconKind,
            Content = content
        };
        _bottomItems.Add(item);
    }

    private void OnNavigationSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox listBox || listBox.SelectedItem is not NavigationItem item)
            return;

        // 切换内容
        _contentArea.Content = item.Content;

        // 清除另一个列表的选择
        if (listBox == _topNavigationList)
        {
            _bottomNavigationList.SelectedIndex = -1;
        }
        else
        {
            _topNavigationList.SelectedIndex = -1;
        }
    }
}

/// <summary>
/// 导航项数据模型
/// </summary>
public class NavigationItem
{
    public string Text { get; set; } = string.Empty;
    public MaterialIconKind IconKind { get; set; } = MaterialIconKind.CircleOutline;
    public Control? Content { get; set; }
}
