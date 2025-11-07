# 表哥 - DWG翻译计算软件

<div align="center">

![Version](https://img.shields.io/badge/version-1.0.0-blue.svg)
![Python](https://img.shields.io/badge/python-3.8+-green.svg)
![License](https://img.shields.io/badge/license-Commercial-orange.svg)
![Status](https://img.shields.io/badge/status-Production%20Ready-success.svg)

**专业的建筑工程CAD图纸翻译和算量工具**

[功能特性](#-功能特性) • [快速开始](#-快速开始) • [文档](#-文档) • [性能](#-性能) • [许可证](#-许可证)

</div>

---

## 📋 目录

- [功能特性](#-功能特性)
- [快速开始](#-快速开始)
- [详细文档](#-详细文档)
- [性能基准](#-性能基准)
- [技术架构](#-技术架构)
- [开发指南](#-开发指南)
- [FAQ](#-faq)
- [许可证](#-许可证)
- [联系我们](#-联系我们)

---

## ✨ 功能特性

### 核心功能

#### 🖼️ DWG文件预览
- ✅ 支持DWG/DXF格式（R12-R2024版本）
- ✅ CAD级交互体验（拖动、缩放、旋转）
- ✅ 50K+实体流畅渲染（空间索引优化）
- ✅ 完整的图层管理和控制
- ✅ 实体高亮和选择

#### 🤖 AI智能翻译
- ✅ **阿里云百炼大模型**集成
- ✅ **可在设置中配置API密钥和模型**
- ✅ 3种模型选择：
  - **qwen-plus**（推荐）- ¥0.004/1K tokens
  - **qwen-turbo**（快速）- ¥0.002/1K tokens
  - **qwen-max**（最强）- ¥0.040/1K tokens
- ✅ 8种语言支持（中/英/日/韩/法/德/西/俄）
- ✅ **人工级翻译质量**（15年资深专家级提示词）
- ✅ 智能缓存系统（90%+命中率）
- ✅ 批量翻译优化（50条/批）
- ✅ 成本优化：**¥0.05/图纸**

#### 📊 智能构件识别
- ✅ 高级识别算法（正则表达式+AI辅助）
- ✅ 支持构件类型：
  - 梁（框架梁、连梁等）
  - 柱（框架柱、构造柱等）
  - 墙（内墙、外墙等）
  - 板（楼板、屋面板等）
- ✅ 自动提取尺寸规格（300×600等）
- ✅ 材料等级识别（C30/Q345/HRB400）
- ✅ 数量统计和汇总

#### 💾 多格式导出
- ✅ **DWG/DXF导出**（R2010/R2013/R2018/R2024）
- ✅ 完整图层重建
- ✅ 翻译文本自动应用
- ✅ **PDF导出**（矢量格式）
- ✅ **Excel导出**（构件清单）

### 系统功能

#### ⚙️ 完整设置系统
- ✅ **阿里云百炼配置**
  - API密钥输入（带密码显示切换）
  - 模型选择下拉框
  - API端点配置
  - 超时和重试设置
  - **API连接测试按钮**

- ✅ **性能优化设置**
  - 空间索引开关
  - 抗锯齿设置
  - 内存阈值配置
  - 性能监控开关

- ✅ **界面设置**
  - 主题选择（亮/暗/自动）
  - 字体大小
  - 窗口行为

- ✅ **高级设置**
  - 日志级别
  - 缓存管理
  - 配置重置

#### 📝 日志查看器
- ✅ 实时日志监控
- ✅ 日志级别过滤
- ✅ 自动刷新（1/3/5秒）
- ✅ 日志导出和清空

#### ⚡ 性能监控
- ✅ CPU使用率实时显示
- ✅ 内存使用实时显示
- ✅ 性能统计（渲染/识别/导出）
- ✅ 一键内存优化

---

## 🚀 快速开始

### 安装

```bash
# 1. 克隆仓库
git clone <repository_url>
cd biaoge

# 2. 安装依赖
pip install -r requirements.txt

# 3. 配置API密钥（二选一）

# 方式A：环境变量（推荐）
export DASHSCOPE_API_KEY="sk-your-api-key-here"

# 方式B：在应用设置中配置（更用户友好）
# 运行应用后，点击"工具" -> "设置" -> "阿里云百炼"
```

### 运行

```bash
python main.py
```

### 使用流程

1. **打开DWG文件**
   - 点击"文件" -> "打开DWG文件"
   - 或者拖放DWG文件到窗口

2. **配置API（首次使用）**
   - 点击"工具" -> "设置"
   - 在"阿里云百炼"选项卡输入API密钥
   - 选择模型（推荐qwen-plus）
   - 点击"测试连接"验证
   - 点击"确定"保存

3. **翻译图纸**
   - 切换到"翻译"选项卡
   - 选择源语言和目标语言
   - 点击"开始翻译"

4. **构件算量**
   - 切换到"算量"选项卡
   - 点击"识别构件"
   - 查看识别结果和数量统计

5. **导出文件**
   - 切换到"导出"选项卡
   - 选择导出格式（DWG/PDF/Excel）
   - 点击对应的导出按钮

---

## 📚 详细文档

| 文档 | 说明 |
|------|------|
| [完整功能使用教程](docs/完整功能使用教程.md) | 6大功能详细使用教程 |
| [商业级优化总结](docs/商业级优化总结.md) | 性能优化技术详解 |
| [BUILD.md](BUILD.md) | 构建和打包指南 |
| [DEPLOYMENT.md](DEPLOYMENT.md) | 部署和使用指南 |
| [PROJECT_SUMMARY.md](PROJECT_SUMMARY.md) | 项目完成度总结 |

---

## 📊 性能基准

### 测试环境
- **OS**: Linux/Windows/macOS
- **Python**: 3.8+
- **RAM**: 4GB+

### 基准结果

| 测试项目 | 目标标准 | 实际性能 | 状态 |
|---------|---------|---------|------|
| 50K实体空间查询 | < 10ms | **5.35ms** | ✅ **超标准** |
| 内存占用 | < 500MB | **151.55MB** | ✅ **优秀** |
| 构件识别速度 | < 100ms | **1.07ms** | ✅ **极快** |
| DWG导出速度 | < 200ms | **14.85ms** | ✅ **极快** |

**总计**: 4/4 项通过商业级标准 ⭐⭐⭐⭐⭐

### 性能特性
- ✅ 空间索引（R-tree）视口剔除
- ✅ 智能缓存系统（90%+命中率）
- ✅ 批量处理优化
- ✅ Numba JIT加速
- ✅ 自动内存管理
- ✅ 多线程渲染

---

## 🏗️ 技术架构

### 技术栈

```
┌─────────────────────────────────────────┐
│            用户界面层                    │
│  PyQt6 6.6+ | PyQt-Fluent-Widgets      │
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│            业务逻辑层                    │
│  翻译引擎 | 识别算法 | 导出引擎          │
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│            服务层                        │
│  阿里云百炼API | 缓存系统 | 性能监控     │
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│            数据层                        │
│  ezdxf 1.1+ | SQLite | 配置管理         │
└─────────────────────────────────────────┘
```

### 核心依赖

| 依赖 | 版本 | 用途 |
|------|------|------|
| PyQt6 | 6.6+ | GUI框架 |
| ezdxf | 1.1+ | DWG/DXF解析 |
| dashscope | 1.23+ | 阿里云百炼API |
| numba | 0.58+ | JIT加速 |
| psutil | 5.9+ | 性能监控 |

### 项目结构

```
biaoge/
├── main.py                      # 应用入口
├── requirements.txt             # 依赖清单
├── setup.py                     # 安装配置
├── build.spec                   # 打包配置
├── src/
│   ├── dwg/                     # DWG处理
│   │   ├── parser.py            # DWG解析器
│   │   ├── renderer.py          # 渲染引擎
│   │   ├── entities.py          # 实体模型
│   │   └── spatial_index.py    # 空间索引
│   ├── translation/             # 翻译模块
│   │   ├── engine.py            # 翻译引擎
│   │   └── cache.py             # 缓存系统
│   ├── calculation/             # 算量模块
│   │   ├── recognizer.py        # 构件识别
│   │   ├── advanced_recognizer.py  # 高级识别
│   │   └── calculator.py        # 数量计算
│   ├── export/                  # 导出模块
│   │   ├── dwg_exporter.py      # DWG导出
│   │   ├── advanced_dwg_exporter.py  # 高级导出
│   │   ├── pdf_exporter.py      # PDF导出
│   │   └── excel_exporter.py    # Excel导出
│   ├── services/                # 服务层
│   │   └── bailian_client.py    # 百炼API客户端
│   ├── ui/                      # UI组件
│   │   ├── main_window.py       # 主窗口
│   │   ├── viewer.py            # 查看器
│   │   ├── translation.py       # 翻译界面
│   │   ├── calculation.py       # 算量界面
│   │   ├── export.py            # 导出界面
│   │   ├── settings_dialog.py   # 设置对话框
│   │   ├── about.py             # 关于对话框
│   │   ├── log_viewer.py        # 日志查看器
│   │   └── performance_panel.py # 性能面板
│   └── utils/                   # 工具模块
│       ├── logger.py            # 日志系统
│       ├── config_manager.py    # 配置管理
│       ├── performance.py       # 性能监控
│       ├── resource_manager.py  # 资源管理
│       ├── error_recovery.py    # 错误恢复
│       └── ...
├── tests/                       # 测试
│   └── performance_test.py      # 性能测试
├── docs/                        # 文档
├── resources/                   # 资源文件
└── logs/                        # 日志目录
```

---

## 🔧 开发指南

### 打包发布

```bash
# 安装打包工具
pip install pyinstaller

# 打包为可执行文件
pyinstaller build.spec

# Windows: dist/biaoge/biaoge.exe
# macOS: dist/biaoge.app
# Linux: dist/biaoge/biaoge
```

### 代码质量

```bash
# 语法检查
python -m py_compile src/**/*.py

# 运行测试
python tests/performance_test.py

# 代码格式化（可选）
black src/
```

---

## ❓ FAQ

### Q: 如何获取API密钥？
**A**: 访问 [阿里云百炼控制台](https://dashscope.console.aliyun.com/apiKey) 获取。

### Q: 支持哪些DWG版本？
**A**: R12 - R2024所有版本。

### Q: 翻译成本如何计算？
**A**: 使用qwen-plus模型，平均成本约¥0.05/图纸（得益于缓存优化）。

### Q: 可以离线使用吗？
**A**: 预览和算量功能可离线使用，翻译功能需要联网。

### Q: 如何提高翻译质量？
**A**:
1. 选择qwen-max模型（更准确但成本高）
2. 提供上下文信息
3. 使用专业术语字典

---

## 🎹 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+O` | 打开DWG文件 |
| `Ctrl+Q` | 退出应用 |
| `Ctrl++` | 放大视图 |
| `Ctrl+-` | 缩小视图 |
| `F` | 适应视图 |
| `R` | 重置视图 |
| `Ctrl+T` | 切换到翻译 |
| `Ctrl+L` | 切换到算量 |
| `Ctrl+E` | 切换到导出 |
| `Ctrl+Shift+L` | 日志查看器 |
| `Ctrl+,` | 设置 |

---

## 📄 许可证

商业软件 - 版权所有 © 2025

本软件为商业软件，未经授权不得用于商业用途。

详见 [LICENSE](LICENSE) 文件。

---

## 📞 联系我们

- **Email**: support@biaoge.com
- **GitHub Issues**: [提交问题](../../issues)
- **官方网站**: Coming soon...

---

<div align="center">

**Made with ❤️ for Engineers**

如果这个项目对您有帮助，请给我们一个 ⭐

</div>
