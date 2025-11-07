# 设置管理指南

本文档说明如何在DWG智能翻译系统中进行配置管理。

## 目录

- [快速开始](#快速开始)
- [模型选择建议](#模型选择建议)
- [配置文件说明](#配置文件说明)
- [API使用说明](#api使用说明)
- [UI集成示例](#ui集成示例)

---

## 快速开始

### 1. 用户首次使用流程

```
┌─────────────────────────────────────────────┐
│  1. 打开设置界面                                │
│  2. 输入API密钥                                 │
│  3. 选择翻译模型 (默认: qwen-mt-plus)            │
│  4. 点击"测试连接"                              │
│  5. 测试成功后，点击"保存"                       │
│  6. 设置自动保存到 ~/.biaoge/config.toml       │
│  7. 下次启动自动加载                             │
└─────────────────────────────────────────────┘
```

### 2. 代码示例

```python
from utils.settings_manager import settings_manager

# 测试API连接
success, message = settings_manager.test_api_connection(
    api_key="sk-xxxxx",
    model="qwen-mt-plus"
)

if success:
    # 保存设置
    settings_manager.save_api_key("sk-xxxxx")
    settings_manager.save_text_model("qwen-mt-plus")
    print("✓ 设置保存成功")
```

---

## 模型选择建议

### 推荐：使用MT（机器翻译）模型

对于CAD图纸文本翻译，**强烈推荐使用专门的MT模型**：

| 模型 | 说明 | 价格 | 推荐场景 |
|------|------|------|----------|
| **qwen-mt-plus** ⭐ | 高质量翻译，性价比高 | ¥0.006/1K tokens | 生产环境推荐（默认） |
| qwen-mt-turbo | 快速翻译，成本更低 | ¥0.003/1K tokens | 大批量翻译，预算有限 |
| qwen-plus | 通用模型，能力均衡 | ¥0.004/1K tokens | 需要额外AI能力时 |

### MT模型的优势

✅ **专门优化翻译任务**：翻译质量更高，更符合建筑/工程术语习惯
✅ **速度快**：响应时间短，用户体验好
✅ **成本低**：相比通用模型和多模态模型，成本更低
✅ **支持自动语言检测**：无需手动指定源语言

### 为什么不用多模态（VL）模型？

- ❌ CAD图纸文本是从DWG文件提取的**纯文本**，不需要视觉理解
- ❌ 多模态模型更贵（如qwen-vl-plus: ¥0.008/1K tokens）
- ❌ 多模态模型更慢，适合图片翻译，不适合纯文本翻译

---

## 配置文件说明

### 配置文件位置

- **默认配置**: `src/config/default.toml`（系统级，不应修改）
- **用户配置**: `~/.biaoge/config.toml`（用户级，可自动创建）

### 配置优先级

```
用户配置 (最高) → 默认配置 → 代码默认值
```

### 关键配置项

#### API配置

```toml
[api]
api_key = ""                      # API密钥（通过设置界面输入）
text_model = "qwen-mt-plus"       # 文本翻译模型（默认MT）
multimodal_model = "qwen-vl-plus" # 多模态模型（图片翻译用）
image_model = "qwen-vl-plus"      # 图片模型
use_custom_model = false          # 是否使用自定义模型
custom_model = ""                 # 自定义模型名称
```

#### 翻译配置

```toml
[translation]
default_source_lang = "auto"   # 自动检测源语言
default_target_lang = "zh-CN"  # 目标语言：简体中文
use_terminology = true         # 使用术语库
cache_enabled = true           # 启用翻译缓存
```

#### UI配置

```toml
[ui]
theme = 0                     # 主题（0=亮色，1=暗色）
font_family = "微软雅黑"      # 字体
font_size = 9                 # 字号
start_maximized = false       # 启动时最大化
```

---

## API使用说明

### SettingsManager API

#### 1. 保存API密钥

```python
from utils.settings_manager import settings_manager

# 保存API密钥
settings_manager.save_api_key("sk-xxxxx")

# 获取API密钥
api_key = settings_manager.get_api_key()
```

#### 2. 保存模型选择

```python
# 保存文本翻译模型
settings_manager.save_text_model("qwen-mt-plus")

# 获取当前模型
model = settings_manager.get_text_model()  # 默认: qwen-mt-plus
```

#### 3. 测试API连通性

```python
# 测试连接
success, message = settings_manager.test_api_connection(
    api_key="sk-xxxxx",
    model="qwen-mt-plus"
)

if success:
    print(f"✓ {message}")
    # 输出示例：
    # ✓ API连接成功
    #   模型: qwen-mt-plus
    #   测试翻译: Hello → 你好
    #   Token消耗: 15
    #   预估成本: ¥0.0001
else:
    print(f"✗ {message}")
```

#### 4. 批量保存设置

```python
# 一次性保存所有设置
settings_manager.save_all_settings({
    'api.api_key': 'sk-xxxxx',
    'api.text_model': 'qwen-mt-plus',
    'translation.default_source_lang': 'auto',
    'translation.default_target_lang': 'zh-CN',
    'ui.theme': 1,
    'ui.font_size': 10,
})
```

#### 5. 获取翻译设置

```python
# 获取翻译配置
trans_settings = settings_manager.get_translation_settings()
# 返回:
# {
#     'source_lang': 'auto',
#     'target_lang': 'zh-CN',
#     'use_terminology': True,
#     'use_cache': True,
# }
```

---

## UI集成示例

### PyQt6 设置对话框示例

```python
from PyQt6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout,
    QLabel, QLineEdit, QComboBox,
    QPushButton, QMessageBox, QProgressDialog
)
from utils.settings_manager import settings_manager


class SettingsDialog(QDialog):
    """设置对话框"""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.setWindowTitle("API设置")
        self.setup_ui()
        self.load_settings()

    def setup_ui(self):
        """设置UI"""
        layout = QVBoxLayout(self)

        # API密钥输入
        layout.addWidget(QLabel("API密钥:"))
        self.api_key_input = QLineEdit()
        self.api_key_input.setEchoMode(QLineEdit.EchoMode.Password)
        layout.addWidget(self.api_key_input)

        # 模型选择
        layout.addWidget(QLabel("翻译模型:"))
        self.model_combo = QComboBox()
        self.model_combo.addItems([
            "qwen-mt-plus (推荐)",
            "qwen-mt-turbo",
            "qwen-plus",
        ])
        layout.addWidget(self.model_combo)

        # 按钮
        button_layout = QHBoxLayout()

        self.test_button = QPushButton("测试连接")
        self.test_button.clicked.connect(self.on_test_connection)
        button_layout.addWidget(self.test_button)

        self.save_button = QPushButton("保存")
        self.save_button.clicked.connect(self.on_save)
        button_layout.addWidget(self.save_button)

        cancel_button = QPushButton("取消")
        cancel_button.clicked.connect(self.reject)
        button_layout.addWidget(cancel_button)

        layout.addLayout(button_layout)

    def load_settings(self):
        """加载现有设置"""
        # 加载API密钥
        api_key = settings_manager.get_api_key()
        self.api_key_input.setText(api_key)

        # 加载模型选择
        model = settings_manager.get_text_model()
        index = 0 if model == "qwen-mt-plus" else 1 if model == "qwen-mt-turbo" else 2
        self.model_combo.setCurrentIndex(index)

    def on_test_connection(self):
        """测试连接"""
        api_key = self.api_key_input.text().strip()
        if not api_key:
            QMessageBox.warning(self, "错误", "请先输入API密钥")
            return

        # 获取选择的模型
        model_text = self.model_combo.currentText()
        model = model_text.split()[0]  # 提取模型名称

        # 显示进度
        progress = QProgressDialog("正在测试API连接...", None, 0, 0, self)
        progress.setWindowModality(Qt.WindowModality.WindowModal)
        progress.show()

        # 测试连接
        success, message = settings_manager.test_api_connection(
            api_key=api_key,
            model=model
        )

        progress.close()

        # 显示结果
        if success:
            QMessageBox.information(self, "成功", message)
        else:
            QMessageBox.warning(self, "失败", message)

    def on_save(self):
        """保存设置"""
        api_key = self.api_key_input.text().strip()
        if not api_key:
            QMessageBox.warning(self, "错误", "请先输入API密钥")
            return

        model_text = self.model_combo.currentText()
        model = model_text.split()[0]

        # 保存所有设置
        success = settings_manager.save_all_settings({
            'api.api_key': api_key,
            'api.text_model': model,
            'translation.default_source_lang': 'auto',
            'translation.default_target_lang': 'zh-CN',
        })

        if success:
            QMessageBox.information(self, "成功", "设置已保存")
            self.accept()
        else:
            QMessageBox.warning(self, "失败", "保存设置失败")
```

### 使用方法

```python
# 在主窗口中打开设置对话框
def show_settings(self):
    dialog = SettingsDialog(self)
    if dialog.exec() == QDialog.DialogCode.Accepted:
        # 设置已保存，可以重新初始化翻译器
        self.reinitialize_translator()
```

---

## 测试

### 运行持久化测试

```bash
python tests/test_settings_persistence.py
```

测试内容：
- ✅ API密钥保存和加载
- ✅ 模型选择保存和加载
- ✅ 批量设置保存
- ✅ 翻译设置持久化
- ✅ 自定义模型设置
- ✅ UI设置持久化

### 运行UI示例

```bash
python examples/ui_settings_example.py
```

---

## 常见问题

### Q1: 配置文件保存在哪里？

**A:** 用户配置保存在 `~/.biaoge/config.toml`，这是标准的用户配置目录。

### Q2: 如何重置为默认设置？

**A:** 删除用户配置文件：

```python
settings_manager.reset_to_default()
```

或手动删除：

```bash
rm ~/.biaoge/config.toml
```

### Q3: 为什么推荐使用MT模型？

**A:** MT模型（如qwen-mt-plus）是专门为翻译任务优化的，相比通用模型和多模态模型：
- 翻译质量更高
- 响应速度更快
- 成本更低（节省约25-50%）

### Q4: 可以使用自己的模型吗？

**A:** 可以，通过自定义模型功能：

```python
settings_manager.save_custom_model(
    model_name="my-custom-model",
    enabled=True
)
```

### Q5: API密钥是否安全？

**A:** API密钥保存在用户配置文件中（`~/.biaoge/config.toml`），建议：
1. 设置合理的文件权限（chmod 600）
2. 不要提交配置文件到git仓库
3. 定期轮换API密钥

---

## 总结

配置管理系统的核心特性：

✅ **持久化存储**：用户设置自动保存，下次启动自动加载
✅ **API连通性测试**：保存前可以测试连接，确保配置正确
✅ **模型智能选择**：默认使用MT模型，成本低效果好
✅ **灵活配置**：支持多层配置，用户配置覆盖默认配置
✅ **简单易用**：一行代码保存，一行代码读取

---

更多信息请参考：
- [API文档](./API.md)
- [配置文件示例](../src/config/default.toml)
- [示例代码](../examples/ui_settings_example.py)
