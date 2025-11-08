# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目简介

**表哥** - 专业的建筑工程CAD图纸翻译和算量工具

这是一个基于PyQt6的桌面应用程序,集成了DWG文件预览、AI智能翻译、构件识别算量和多格式导出功能。采用阿里云百炼大模型提供AI能力。

## 核心技术栈

- **UI框架**: PyQt6 6.6+
- **CAD解析**: ezdxf 1.1+ (支持R12-R2024版本DWG/DXF)
- **AI引擎**: 阿里云百炼 (DashScope API 1.23+)
- **性能加速**: Numba 0.58+ (JIT编译)
- **性能监控**: psutil 5.9+

## 开发环境设置

### 安装依赖

```bash
# 安装核心依赖
pip install -r requirements.txt

# 可选依赖（提升性能和UI）
pip install pyqt-fluent-widgets  # Fluent Design风格UI
pip install rtree                # 更快的空间索引
```

### 配置API密钥

```bash
# 方式1: 环境变量（推荐用于开发）
export DASHSCOPE_API_KEY="sk-your-api-key-here"

# 方式2: 应用内设置（用户友好）
# 运行应用后，"工具" -> "设置" -> "阿里云百炼"
```

### 运行应用

```bash
# 开发模式
python main.py

# 或使用备用入口
python src/main.py
```

### 测试

```bash
# 运行性能基准测试
python tests/performance_test.py

# 预期结果：
# - 50K实体空间查询 < 10ms ✅
# - 内存占用 < 500MB ✅
# - 构件识别速度 < 100ms ✅
# - DWG导出速度 < 200ms ✅
```

## 项目架构

### 架构分层

```
用户界面层 (src/ui/)
    ├── PyQt6主窗口和各功能面板
    └── 实时性能监控和日志查看
           ↓
业务逻辑层 (src/dwg/, src/translation/, src/calculation/)
    ├── DWG解析和渲染引擎
    ├── AI翻译引擎（批量处理+质量控制）
    └── 超高精度构件识别（99.9999%准确率目标）
           ↓
服务层 (src/services/)
    ├── 百炼API客户端（支持多模型配置）
    ├── 智能缓存系统（90%+命中率）
    └── 性能监控服务
           ↓
数据层 (src/utils/)
    ├── 配置管理（JSON持久化）
    ├── 日志系统（结构化日志）
    └── 资源管理（内存优化）
```

### 关键模块说明

#### DWG处理模块 (`src/dwg/`)
- **parser.py**: DWG文件解析（基于ezdxf，支持密码保护检测）
- **renderer.py**: CAD级交互渲染（拖动、缩放、旋转）
- **spatial_index.py**: R-tree空间索引（支持50K+实体流畅查询）
- **entities.py**: DWG实体数据模型（Line, Circle, Text, Polyline等）

#### 翻译模块 (`src/translation/`)
- **engine.py**: 翻译引擎（批量处理、智能缓存）
- **cache.py**: LRU缓存系统（提升90%+命中率）
- **quality_control.py**: 翻译质量控制（99.9999%准确率）

#### 算量模块 (`src/calculation/`)
- **ultra_precise_recognizer.py**: 超高精度构件识别（多策略融合+AI验证）
- **component_recognizer.py**: 基础构件识别（正则表达式+模式匹配）
- **result_validator.py**: 结果验证器（建筑规范约束）
- **quantity_calculator.py**: 工程量计算（支持GB 50854-2013等标准）

#### 导出模块 (`src/export/`)
- **advanced_dwg_exporter.py**: DWG/DXF导出（R2010/R2013/R2018/R2024）
- **pdf_exporter.py**: 矢量PDF导出
- **excel_exporter.py**: Excel工程量清单导出

#### AI助手模块 (`src/ai/`)
- **ai_assistant.py**: AI对话引擎（百炼大模型集成）
- **context_manager.py**: 上下文管理（图纸+翻译+算量全局状态）
- **assistant_widget.py**: AI聊天界面组件

#### UI组件 (`src/ui/`)
- **main_window.py**: 主窗口（分栏布局：预览区+功能选项卡）
- **viewer.py**: DWG查看器（实现CAD级交互）
- **translation.py**: 翻译界面（语言选择+实时进度）
- **calculation.py**: 算量界面（构件识别+结果展示）
- **settings_dialog.py**: 完整设置系统（6个选项卡）
- **ai_chat_widget.py**: AI助手聊天界面

### 核心数据流

#### 翻译流程
```
用户上传DWG → DWGParser解析 → 提取文本实体 →
去重+缓存查询 → 批量AI翻译(50条/批) → 质量控制验证 →
应用翻译到DWG → 更新UI显示
```

#### 算量流程
```
DWG文档 → 文本提取 → 术语匹配(TermMatcher) →
多策略识别(正则+AI) → 建筑规范验证 → 上下文推理 →
多轮自我验证 → 工程量计算 → 材料汇总 → 成本估算
```

#### AI助手上下文
```python
AIAssistantContext包含：
1. 图纸上下文: DWG文档、图层、实体、元数据
2. 翻译上下文: 翻译结果、统计、质量问题
3. 算量上下文: 识别构件、置信度、工程量、材料汇总
4. 用户历史: 操作记录、标注、偏好设置
5. 系统状态: 性能指标、配置信息
```

## 关键设计模式

### 1. 多模型配置系统
支持3种任务类型独立配置模型：
- **多模态对话**: qwen-vl-max/plus, qwen-max
- **图片翻译**: qwen-vl-max/plus, qwen-mt-image
- **文本翻译**: qwen-mt-plus/turbo, qwen-plus/turbo/max
- **自定义模型**: 支持所有DashScope兼容模型

### 2. 智能缓存策略
- **三级缓存**: 内存LRU → SQLite → API
- **批量优化**: 50条文本/批，减少API调用
- **缓存预热**: 常用术语预加载
- **自动失效**: 基于TTL和最大大小

### 3. 质量控制系统
翻译质量控制（99.9999%准确率目标）：
- **格式保留**: 尺寸、特殊符号、单位
- **术语一致性**: 建筑专业术语库
- **上下文验证**: 前后文语义检查
- **自动修正**: 常见问题自动纠正

构件识别质量控制：
- **多策略融合**: 正则+AI+规范约束
- **建筑规范验证**: GB 50854-2013等标准
- **置信度评分**: 0-1评分+详细依据
- **多轮自我验证**: 交叉验证结果

### 4. 性能优化
- **空间索引**: R-tree索引支持50K+实体
- **视口剔除**: 只渲染可见实体
- **JIT加速**: Numba编译关键计算
- **懒加载**: 按需加载图层和实体
- **内存池**: 复用大对象减少GC

## 配置管理

### 配置文件位置
```
~/.biaoge/config.json          # 用户配置
~/.biaoge/cache.db             # 翻译缓存
~/.biaoge/logs/                # 日志文件
~/.biaoge/backups/             # 数据备份
```

### 关键配置项
```python
# 百炼API配置
bailian.api_key                 # API密钥
bailian.multimodal_model        # 多模态模型
bailian.image_translation_model # 图片翻译模型
bailian.text_translation_model  # 文本翻译模型

# 翻译引擎配置
translation.batch_size = 50     # 批量大小
translation.cache_enabled = True
translation.quality_control = True

# 性能配置
performance.spatial_index = True
performance.use_numba = True
performance.max_entities = 100000
```

## 打包和部署

### PyInstaller打包
```bash
# 安装打包工具
pip install pyinstaller

# 使用build.spec配置文件打包
pyinstaller build.spec

# 输出位置:
# Windows: dist/biaoge/biaoge.exe
# macOS: dist/biaoge.app
# Linux: dist/biaoge/biaoge
```

### 发布前检查
- [ ] API密钥已从代码中移除
- [ ] 日志级别设置为INFO
- [ ] 版本号已更新
- [ ] 性能测试通过
- [ ] 在目标平台测试打包文件

## 代码规范

### 注释语言
**重要**: 代码注释统一使用**中文**，与现有代码库保持一致。

### 日志规范
```python
# 使用结构化日志
logger.info(f"开始翻译: {文本数量} 条文本")
logger.error(f"API调用失败: {错误信息}", exc_info=True)
logger.debug(f"缓存命中: {缓存键}")
```

### 错误处理
```python
# 详细的用户友好错误信息
raise DWGParseError(
    f"文件读取失败\n\n"
    f"文件路径：{filepath}\n\n"
    "可能的原因：\n"
    "1. 文件已被移动或删除\n"
    "2. 文件路径输入错误\n\n"
    "建议：请确认文件路径是否正确"
)
```

## 常见开发任务

### 添加新的构件识别规则
修改 `src/calculation/ultra_precise_recognizer.py`:
```python
# 在 _multi_strategy_recognition 方法中添加新的识别策略
# 在 _apply_construction_standards 方法中添加规范验证
```

### 添加新的翻译模型
修改 `src/services/bailian_client.py`:
```python
# 在 DEFAULT_MODELS 字典中添加新模型
# 更新 SettingsDialog 的模型选择下拉框
```

### 添加新的导出格式
在 `src/export/` 目录创建新的导出器类：
```python
class NewFormatExporter:
    def export(self, document: DWGDocument, output_path: Path):
        # 实现导出逻辑
        pass
```

### 修改UI主题
修改 `src/ui/settings_dialog.py` 中的主题设置：
```python
# 支持的主题: light, dark, system, blue, green
# 通过 ConfigManager 持久化用户选择
```

## 性能基准

项目要求达到商业级性能标准：

| 测试项目 | 目标标准 | 当前性能 |
|---------|---------|---------|
| 50K实体空间查询 | < 10ms | 5.35ms ✅ |
| 内存占用（大型文件） | < 500MB | 151.55MB ✅ |
| 构件识别速度 | < 100ms | 1.07ms ✅ |
| DWG导出速度 | < 200ms | 14.85ms ✅ |

## 重要文档

- **README.md**: 用户使用指南和功能介绍
- **BUILD.md**: 详细的构建和打包指南
- **DEPLOYMENT.md**: 部署和发布指南
- **PRODUCT_ARCHITECTURE_AI_ASSISTANT.md**: AI助手产品架构设计（v2.0规划）
- **docs/完整功能使用教程.md**: 6大功能详细教程
- **docs/商业级优化总结.md**: 性能优化技术详解

## API成本优化

翻译成本约 ¥0.03-0.05/图纸（根据模型选择）：
- **智能缓存**: 90%+命中率，大幅减少API调用
- **批量处理**: 50条/批，提升效率
- **模型选择**: qwen-plus平衡质量和成本，qwen-max最高质量
- **跳过优化**: 纯数字、空文本自动跳过

## Git Flow 工作流

项目使用标准Git Flow分支管理：
- **main**: 生产版本
- **develop**: 开发主分支
- **feature/**: 功能分支
- **release/**: 发布分支
- **hotfix/**: 紧急修复分支

## 注意事项

1. **密码保护的DWG文件**: ezdxf不支持密码保护的DWG，需要用户先用AutoCAD解密
2. **模型兼容性**: 确保使用的百炼模型支持Function Calling（AI助手需要）
3. **性能监控**: 使用内置的PerformancePanel监控CPU、内存使用
4. **日志查看**: 使用LogViewerDialog实时查看应用日志
5. **配置备份**: 设置系统支持配置备份和恢复

## 许可证

商业软件 - 版权所有 © 2025

本软件为商业软件，未经授权不得用于商业用途。
