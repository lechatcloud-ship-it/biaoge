using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Serilog;

namespace BiaogPlugin.UI
{
    /// <summary>
    /// 图层选择对话框 - 用于图层翻译功能
    /// </summary>
    public partial class LayerSelectionDialog : Window
    {
        private List<LayerItem> _layerItems;

        public List<string> SelectedLayerNames { get; private set; }

        public LayerSelectionDialog()
        {
            InitializeComponent();
            _layerItems = new List<LayerItem>();
            SelectedLayerNames = new List<string>();
        }

        /// <summary>
        /// 设置图层列表数据
        /// </summary>
        public void SetLayers(List<Services.LayerTranslationService.LayerInfo> layers)
        {
            _layerItems = layers.Select(l => new LayerItem
            {
                LayerName = l.LayerName,
                TextCount = l.TextCount,
                ColorName = l.ColorName,
                IsLocked = l.IsLocked,
                IsOff = l.IsOff,
                IsFrozen = l.IsFrozen,
                IsSelected = l.TextCount > 0 // 默认选中有文本的图层
            }).ToList();

            LayersListBox.ItemsSource = _layerItems;

            // 更新汇总信息
            UpdateSummary();
            UpdateSelectionInfo();
        }

        /// <summary>
        /// 更新汇总信息
        /// </summary>
        private void UpdateSummary()
        {
            var totalLayers = _layerItems.Count;
            var layersWithText = _layerItems.Count(l => l.TextCount > 0);
            var totalTexts = _layerItems.Sum(l => l.TextCount);

            SummaryText.Text = $"共{totalLayers}个图层，其中{layersWithText}个图层包含{totalTexts}个文本";
        }

        /// <summary>
        /// 更新选中信息
        /// </summary>
        private void UpdateSelectionInfo()
        {
            var selectedItems = _layerItems.Where(l => l.IsSelected).ToList();
            var selectedCount = selectedItems.Count;
            var selectedTexts = selectedItems.Sum(l => l.TextCount);

            if (selectedCount == 0)
            {
                SelectionInfoText.Text = "未选择任何图层";
            }
            else
            {
                SelectionInfoText.Text = $"已选择 {selectedCount} 个图层，包含 {selectedTexts} 个文本";
            }
        }

        /// <summary>
        /// 全选按钮点击事件
        /// </summary>
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _layerItems)
            {
                item.IsSelected = true;
            }
            LayersListBox.Items.Refresh();
            UpdateSelectionInfo();
        }

        /// <summary>
        /// 反选按钮点击事件
        /// </summary>
        private void InvertSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _layerItems)
            {
                item.IsSelected = !item.IsSelected;
            }
            LayersListBox.Items.Refresh();
            UpdateSelectionInfo();
        }

        /// <summary>
        /// 清空按钮点击事件
        /// </summary>
        private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _layerItems)
            {
                item.IsSelected = false;
            }
            LayersListBox.Items.Refresh();
            UpdateSelectionInfo();
        }

        /// <summary>
        /// 翻译按钮点击事件
        /// </summary>
        private void TranslateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SelectedLayerNames = _layerItems
                    .Where(l => l.IsSelected)
                    .Select(l => l.LayerName)
                    .ToList();

                if (SelectedLayerNames.Count == 0)
                {
                    MessageBox.Show("请至少选择一个图层", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 检查是否选中的图层都没有文本
                var selectedItems = _layerItems.Where(l => l.IsSelected).ToList();
                var totalTexts = selectedItems.Sum(l => l.TextCount);

                if (totalTexts == 0)
                {
                    MessageBox.Show("选中的图层没有文本", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "确认图层选择失败");
                MessageBox.Show($"操作失败：{ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// 图层项数据模型
        /// </summary>
        public class LayerItem : System.ComponentModel.INotifyPropertyChanged
        {
            private bool _isSelected;

            public string LayerName { get; set; } = string.Empty;
            public int TextCount { get; set; }
            public string ColorName { get; set; } = string.Empty;
            public bool IsLocked { get; set; }
            public bool IsOff { get; set; }
            public bool IsFrozen { get; set; }

            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected != value)
                    {
                        _isSelected = value;
                        OnPropertyChanged(nameof(IsSelected));
                    }
                }
            }

            public string StatusText
            {
                get
                {
                    var status = new List<string>();
                    if (IsLocked) status.Add("锁定");
                    if (IsOff) status.Add("关闭");
                    if (IsFrozen) status.Add("冻结");
                    return status.Count > 0 ? string.Join(", ", status) : "正常";
                }
            }

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
