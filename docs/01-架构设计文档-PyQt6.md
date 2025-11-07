# DWG智能翻译算量系统 - 架构设计文档 (PyQt6版本)

## 文档信息
- **项目名称**: DWG智能翻译算量系统
- **技术方案**: PyQt6 + Python 3.11+
- **版本**: v2.0 (PyQt6重构版)
- **创建日期**: 2025-11-07
- **最后更新**: 2025-11-07

---

## 1. 项目概述

### 1.1 项目背景
随着建筑工程行业国际化发展，对CAD图纸的跨语言翻译和自动化算量需求日益增长。本项目采用**PyQt6桌面框架 + Python技术栈**，利用阿里云百炼大模型能力，实现DWG图纸的智能翻译和精准算量。

**为什么选择PyQt6？**
- ✅ **渲染性能强**：Qt原生绘图引擎，CAD级别性能
- ✅ **Windows原生体验**：目标用户90%在Windows
- ✅ **开发效率高**：Python生态成熟，开发快速
- ✅ **CAD生态完善**：ezdxf等成熟库，无需重复造轮

### 1.2 核心功能
- DWG/DXF图纸文件的解析与原生渲染（QPainter）
- 基于阿里云百炼大模型的智能翻译（支持中英文及多语言）
- 自动化工程量计算与统计（AI辅助识别构件）
- 翻译后图纸的重新生成与导出（DWG/DXF/PDF）
- 算量结果HTML/Excel报表导出

### 1.3 技术目标
- **性能**: 10MB以内图纸加载时间 < 2秒
- **准确性**: 翻译准确率 > 95%，算量误差率 < 2%
- **稳定性**: 长时间运行不崩溃，渲染60 FPS+
- **兼容性**: 主力Windows 10/11，兼顾macOS/Linux

---

## 2. 技术架构

### 2.1 总体架构图

```
┌────────────────────────────────────────────────────────────┐
│                   表示层 (PyQt6 UI)                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │ MainWindow   │  │ DWG Viewer   │  │ Translation  │    │
│  │ (主窗口)     │  │ (图纸查看器) │  │ Dialog       │    │
│  └──────────────┘  └──────────────┘  └──────────────┘    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │ Calculation  │  │ Settings     │  │ Report       │    │
│  │ Panel        │  │ Dialog       │  │ Viewer       │    │
│  └──────────────┘  └──────────────┘  └──────────────┘    │
└────────────────────────────────────────────────────────────┘
                            ↕ (Qt Signals/Slots)
┌────────────────────────────────────────────────────────────┐
│                   业务逻辑层 (Python Core)                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │ DWG Parser   │  │ Translation  │  │ Calculation  │    │
│  │ (ezdxf)      │  │ Engine       │  │ Engine       │    │
│  └──────────────┘  └──────────────┘  └──────────────┘    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │ Renderer     │  │ Export       │  │ Cache        │    │
│  │ (QPainter)   │  │ Generator    │  │ Manager      │    │
│  └──────────────┘  └──────────────┘  └──────────────┘    │
└────────────────────────────────────────────────────────────┘
                            ↕
┌────────────────────────────────────────────────────────────┐
│                   数据访问层 (Data Layer)                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │ SQLite DB    │  │ File System  │  │ Bailian API  │    │
│  │ (缓存)       │  │ (配置/临时)  │  │ (HTTP Client)│    │
│  └──────────────┘  └──────────────┘  └──────────────┘    │
└────────────────────────────────────────────────────────────┘
                            ↕
┌────────────────────────────────────────────────────────────┐
│                  外部服务层 (External Services)             │
│  ┌──────────────────────────────────────────────────┐     │
│  │         阿里云百炼API (大模型服务)                 │     │
│  │    Qwen-Plus / Qwen-Max / Qwen-Flash             │     │
│  └──────────────────────────────────────────────────┘     │
└────────────────────────────────────────────────────────────┘
```

**架构特点**：
- **单体应用**：所有逻辑在同一进程（不像Tauri前后端分离）
- **事件驱动**：PyQt信号槽机制，松耦合
- **多线程**：QThread处理耗时任务（解析、翻译、算量）
- **原生渲染**：QPainter直接绘制，无Web渲染开销

### 2.2 技术栈选型

#### 2.2.1 核心框架
**选择: PyQt6 6.6+**

```python
# 技术栈总览
├── Python 3.11+                  # 核心语言
├── PyQt6 6.6+                    # GUI框架
├── PyQt-Fluent-Widgets 1.5+      # Fluent Design UI组件库 ⭐
├── ezdxf 1.1+                    # DWG/DXF解析
├── numpy 1.26+                   # 数值计算
├── requests 2.31+                # HTTP客户端
├── sqlite3                       # 内置数据库
├── openpyxl 3.1+                 # Excel导出
├── reportlab 4.0+                # PDF生成
├── pyqtgraph 0.13+               # 高性能绘图
└── numba 0.58+                   # JIT加速
```

**打包工具**：
```python
└── PyInstaller 6.0+         # 打包成.exe
```

**为什么选PyQt6而不是PyQt5？**
- ✅ Qt6性能更好（现代OpenGL）
- ✅ 更好的高DPI支持
- ✅ 更现代的API设计
- ✅ 长期支持（Qt5将停止维护）

**为什么使用PyQt-Fluent-Widgets？**
- ✅ **现代化UI**：Fluent Design设计语言，美观大方
- ✅ **Windows 11风格**：完美适配Windows 11用户体验
- ✅ **组件丰富**：导航、卡片、对话框、设置页等开箱即用
- ✅ **开源免费**：MIT协议，商业友好
- ✅ **活跃维护**：中国开发者社区活跃，文档完善
- ✅ **无缝集成**：基于PyQt6，完全兼容

#### 2.2.2 DWG解析库
**选择: ezdxf 1.1+**

```python
import ezdxf

# ezdxf是纯Python库，支持：
doc = ezdxf.readfile("drawing.dwg")
modelspace = doc.modelspace()

# 遍历所有实体
for entity in modelspace:
    if entity.dxftype() == 'LINE':
        start = entity.dxf.start
        end = entity.dxf.end
    elif entity.dxftype() == 'TEXT':
        text = entity.dxf.text
        position = entity.dxf.insert
```

**优势**：
- ✅ 纯Python实现，无需C编译
- ✅ 支持DWG/DXF R12-R2024
- ✅ 文档完善，社区活跃
- ✅ MIT协议，商业友好

**劣势**：
- ⚠️ 解析速度比libredwg稍慢（但可接受）
- ⚠️ 部分复杂图块支持有限

#### 2.2.3 UI组件库：PyQt-Fluent-Widgets

**主界面框架示例**：
```python
from qfluentwidgets import (
    FluentWindow, NavigationItemPosition,
    FluentIcon, SplitFluentWindow
)

class MainWindow(FluentWindow):
    """主窗口（使用Fluent Design）"""

    def __init__(self):
        super().__init__()
        self.setWindowTitle("DWG智能翻译算量系统")
        self.resize(1200, 800)

        # 创建子界面
        self.dwgViewerInterface = DWGViewerInterface(self)
        self.translationInterface = TranslationInterface(self)
        self.calculationInterface = CalculationInterface(self)
        self.settingsInterface = SettingsInterface(self)

        # 添加导航项
        self.addSubInterface(
            self.dwgViewerInterface,
            FluentIcon.DOCUMENT,
            '图纸查看'
        )
        self.addSubInterface(
            self.translationInterface,
            FluentIcon.LANGUAGE,
            '智能翻译'
        )
        self.addSubInterface(
            self.calculationInterface,
            FluentIcon.CALCULATOR,
            '工程算量'
        )
        self.addSubInterface(
            self.settingsInterface,
            FluentIcon.SETTING,
            '设置',
            NavigationItemPosition.BOTTOM
        )
```

**Fluent风格组件**：
```python
from qfluentwidgets import (
    CardWidget, PushButton, PrimaryPushButton,
    InfoBar, InfoBarPosition, ProgressBar,
    Dialog, MessageBox, SettingCard
)

# 1. 卡片组件
class TranslationCard(CardWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setTitle("翻译设置")

        # 添加控件
        self.translateBtn = PrimaryPushButton("开始翻译", self)

# 2. 信息提示
InfoBar.success(
    title='翻译完成',
    content='已成功翻译125条文本',
    orient=Qt.Horizontal,
    isClosable=True,
    position=InfoBarPosition.TOP,
    duration=2000,
    parent=self
)

# 3. 进度条
progressBar = ProgressBar(self)
progressBar.setValue(50)

# 4. 设置页
class SettingsInterface(ScrollArea):
    def __init__(self, parent=None):
        super().__init__(parent)

        # API设置卡片
        self.apiCard = SettingCard(
            FluentIcon.CLOUD,
            "API密钥",
            "配置阿里云百炼API密钥"
        )
```

#### 2.2.4 图纸渲染方案

**方案A：QPainter（2D平面图）** - 主力方案
```python
from PyQt6.QtGui import QPainter, QPen, QColor
from PyQt6.QtCore import Qt

class DWGCanvas(QWidget):
    def paintEvent(self, event):
        painter = QPainter(self)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)

        # 绘制实体
        for entity in self.entities:
            if entity.type == 'LINE':
                pen = QPen(QColor(entity.color), entity.linewidth)
                painter.setPen(pen)
                painter.drawLine(entity.start, entity.end)
            elif entity.type == 'TEXT':
                painter.drawText(entity.position, entity.text)
```

**优势**：
- ✅ 硬件加速（OpenGL后端）
- ✅ 绘图API简单直观
- ✅ 性能稳定（百万实体无压力）
- ✅ 完美支持中文字体

**方案B：PyQtGraph（高性能2D）** - 备选
```python
import pyqtgraph as pg

# 用于大量实体的高性能渲染
plotWidget = pg.PlotWidget()
plotWidget.plot(x_data, y_data, pen='r')
```

**方案C：PyOpenGL（3D渲染）** - 未来扩展
```python
from OpenGL.GL import *

# 3D DWG渲染
```

#### 2.2.4 大模型API客户端

```python
import requests
import json

class BailianClient:
    """阿里云百炼API客户端"""

    def __init__(self, api_key: str):
        self.api_key = api_key
        self.endpoint = "https://dashscope.aliyuncs.com/api/v1"
        self.headers = {
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json"
        }

    def translate(self, text: str, from_lang: str, to_lang: str) -> str:
        """翻译文本"""
        payload = {
            "model": "qwen-plus",
            "input": {
                "messages": [
                    {
                        "role": "system",
                        "content": "你是专业的建筑图纸翻译专家"
                    },
                    {
                        "role": "user",
                        "content": f"请将以下{from_lang}文本翻译成{to_lang}：{text}"
                    }
                ]
            },
            "parameters": {
                "temperature": 0.3
            }
        }

        response = requests.post(
            f"{self.endpoint}/services/aigc/text-generation/generation",
            headers=self.headers,
            json=payload,
            timeout=60
        )

        result = response.json()
        return result['output']['text']
```

---

## 3. 核心模块设计

### 3.1 DWG解析模块

#### 3.1.1 模块架构
```python
# src/dwg/parser.py
from dataclasses import dataclass
from typing import List, Dict, Any
import ezdxf

@dataclass
class DWGEntity:
    """DWG实体基类"""
    entity_id: str
    type: str  # LINE, CIRCLE, TEXT, MTEXT, etc.
    layer: str
    color: str
    properties: Dict[str, Any]

@dataclass
class LineEntity(DWGEntity):
    start: tuple[float, float, float]
    end: tuple[float, float, float]
    linewidth: float

@dataclass
class TextEntity(DWGEntity):
    text: str
    position: tuple[float, float, float]
    height: float
    rotation: float
    style: str

class DWGDocument:
    """DWG文档模型"""
    def __init__(self):
        self.version: str = ""
        self.layers: List[Layer] = []
        self.entities: List[DWGEntity] = []
        self.blocks: List[Block] = []
        self.text_styles: List[TextStyle] = []
        self.metadata: Dict[str, Any] = {}

class DWGParser:
    """DWG解析器"""

    def parse(self, filepath: str) -> DWGDocument:
        """解析DWG文件"""
        try:
            doc = ezdxf.readfile(filepath)
        except IOError:
            raise DWGParseError(f"无法读取文件: {filepath}")
        except ezdxf.DXFStructureError:
            raise DWGParseError(f"文件格式错误: {filepath}")

        dwg_doc = DWGDocument()
        dwg_doc.version = doc.dxfversion

        # 解析图层
        for layer in doc.layers:
            dwg_doc.layers.append(self._parse_layer(layer))

        # 解析实体
        modelspace = doc.modelspace()
        for entity in modelspace:
            parsed_entity = self._parse_entity(entity)
            if parsed_entity:
                dwg_doc.entities.append(parsed_entity)

        return dwg_doc

    def _parse_entity(self, entity) -> DWGEntity:
        """解析单个实体"""
        dxftype = entity.dxftype()

        if dxftype == 'LINE':
            return LineEntity(
                entity_id=str(entity.dxf.handle),
                type='LINE',
                layer=entity.dxf.layer,
                color=self._get_color(entity),
                properties={},
                start=tuple(entity.dxf.start),
                end=tuple(entity.dxf.end),
                linewidth=entity.dxf.lineweight / 100.0
            )

        elif dxftype in ['TEXT', 'MTEXT']:
            return TextEntity(
                entity_id=str(entity.dxf.handle),
                type=dxftype,
                layer=entity.dxf.layer,
                color=self._get_color(entity),
                properties={},
                text=entity.dxf.text,
                position=tuple(entity.dxf.insert if dxftype == 'TEXT' else entity.dxf.insert),
                height=entity.dxf.height,
                rotation=entity.dxf.rotation if hasattr(entity.dxf, 'rotation') else 0,
                style=entity.dxf.style
            )

        # 其他实体类型...
        return None
```

#### 3.1.2 性能优化

```python
from concurrent.futures import ThreadPoolExecutor
import numpy as np

class OptimizedDWGParser(DWGParser):
    """优化版解析器"""

    def parse_parallel(self, filepath: str) -> DWGDocument:
        """并行解析（大文件）"""
        doc = ezdxf.readfile(filepath)
        modelspace = doc.modelspace()

        # 将实体分组
        entities_list = list(modelspace)
        chunk_size = len(entities_list) // 4  # 4个线程

        with ThreadPoolExecutor(max_workers=4) as executor:
            futures = []
            for i in range(0, len(entities_list), chunk_size):
                chunk = entities_list[i:i+chunk_size]
                future = executor.submit(self._parse_entities_batch, chunk)
                futures.append(future)

            # 合并结果
            all_entities = []
            for future in futures:
                all_entities.extend(future.result())

        dwg_doc = DWGDocument()
        dwg_doc.entities = all_entities
        return dwg_doc

    def build_spatial_index(self, entities: List[DWGEntity]):
        """构建空间索引（R-tree）"""
        from rtree import index

        idx = index.Index()
        for i, entity in enumerate(entities):
            bbox = self._get_bounding_box(entity)
            idx.insert(i, bbox)

        return idx
```

### 3.2 翻译引擎模块

#### 3.2.1 翻译流程
```
┌─────────────┐
│ 提取文本    │  ← 从DWG实体中提取TEXT/MTEXT
└──────┬──────┘
       ↓
┌─────────────┐
│ 文本预处理  │  ← 去重、过滤纯数字、标准化
└──────┬──────┘
       ↓
┌─────────────┐
│ 查询缓存    │  ← SQLite缓存查询
└──────┬──────┘
       ↓
┌─────────────┐
│ 批量翻译    │  ← 调用百炼API（多线程）
└──────┬──────┘
       ↓
┌─────────────┐
│ 存储缓存    │  ← 保存到SQLite
└──────┬──────┘
       ↓
┌─────────────┐
│ 更新实体    │  ← 将翻译结果映射回实体
└─────────────┘
```

#### 3.2.2 实现代码

```python
# src/translation/engine.py
from PyQt6.QtCore import QThread, pyqtSignal
from typing import Dict, List
import sqlite3

class TranslationEngine:
    """翻译引擎"""

    def __init__(self, api_client: BailianClient, cache_db: str):
        self.client = api_client
        self.cache = TranslationCache(cache_db)

    def translate_document(
        self,
        document: DWGDocument,
        from_lang: str,
        to_lang: str
    ) -> Dict[str, str]:
        """翻译整个文档"""
        # 1. 提取所有文本实体
        text_entities = [
            e for e in document.entities
            if isinstance(e, TextEntity)
        ]

        # 2. 提取唯一文本
        unique_texts = set()
        for entity in text_entities:
            text = entity.text.strip()
            if text and not self._is_number(text):
                unique_texts.add(text)

        # 3. 查询缓存
        translations = {}
        to_translate = []

        for text in unique_texts:
            cached = self.cache.get(text, from_lang, to_lang)
            if cached:
                translations[text] = cached
            else:
                to_translate.append(text)

        # 4. 批量翻译未缓存的文本
        if to_translate:
            new_translations = self._batch_translate(
                to_translate,
                from_lang,
                to_lang
            )
            translations.update(new_translations)

            # 存入缓存
            for text, translation in new_translations.items():
                self.cache.set(text, translation, from_lang, to_lang)

        return translations

    def _batch_translate(
        self,
        texts: List[str],
        from_lang: str,
        to_lang: str,
        batch_size: int = 50
    ) -> Dict[str, str]:
        """批量翻译"""
        results = {}

        # 分批处理
        for i in range(0, len(texts), batch_size):
            batch = texts[i:i+batch_size]

            # 构建批量翻译提示词
            prompt = self._build_batch_prompt(batch, from_lang, to_lang)

            # 调用API
            try:
                response = self.client.translate_batch(prompt)
                batch_results = self._parse_batch_response(response, batch)
                results.update(batch_results)
            except Exception as e:
                # 降级到单条翻译
                for text in batch:
                    try:
                        translation = self.client.translate(text, from_lang, to_lang)
                        results[text] = translation
                    except Exception as e2:
                        results[text] = text  # 翻译失败保留原文

        return results

    def _build_batch_prompt(
        self,
        texts: List[str],
        from_lang: str,
        to_lang: str
    ) -> str:
        """构建批量翻译提示词"""
        text_list = "\n".join([f"{i+1}. {text}" for i, text in enumerate(texts)])

        prompt = f"""
你是专业的建筑工程图纸翻译专家。请将以下{from_lang}文本翻译成{to_lang}。

要求：
1. 保持专业术语准确性
2. 保留数字、单位、符号不变
3. 输出JSON格式：{{"原文1": "译文1", "原文2": "译文2"}}

待翻译文本：
{text_list}
"""
        return prompt.strip()

class TranslationCache:
    """翻译缓存"""

    def __init__(self, db_path: str):
        self.conn = sqlite3.connect(db_path)
        self._init_db()

    def _init_db(self):
        self.conn.execute("""
            CREATE TABLE IF NOT EXISTS translations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source_text TEXT NOT NULL,
                target_text TEXT NOT NULL,
                source_lang TEXT NOT NULL,
                target_lang TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(source_text, source_lang, target_lang)
            )
        """)
        self.conn.commit()

    def get(self, text: str, from_lang: str, to_lang: str) -> str:
        cursor = self.conn.execute(
            "SELECT target_text FROM translations WHERE source_text=? AND source_lang=? AND target_lang=?",
            (text, from_lang, to_lang)
        )
        result = cursor.fetchone()
        return result[0] if result else None

    def set(self, text: str, translation: str, from_lang: str, to_lang: str):
        self.conn.execute(
            "INSERT OR REPLACE INTO translations (source_text, target_text, source_lang, target_lang) VALUES (?, ?, ?, ?)",
            (text, translation, from_lang, to_lang)
        )
        self.conn.commit()
```

#### 3.2.3 多线程翻译

```python
# src/translation/worker.py
from PyQt6.QtCore import QThread, pyqtSignal

class TranslationWorker(QThread):
    """翻译工作线程"""

    # 信号定义
    progress_updated = pyqtSignal(int, int)  # 当前, 总数
    translation_completed = pyqtSignal(dict)  # 翻译结果
    error_occurred = pyqtSignal(str)  # 错误信息

    def __init__(self, engine: TranslationEngine, document: DWGDocument, from_lang: str, to_lang: str):
        super().__init__()
        self.engine = engine
        self.document = document
        self.from_lang = from_lang
        self.to_lang = to_lang
        self._is_running = True

    def run(self):
        """执行翻译"""
        try:
            # 提取文本实体
            text_entities = [e for e in self.document.entities if isinstance(e, TextEntity)]
            total = len(text_entities)

            # 执行翻译
            translations = {}
            for i, entity in enumerate(text_entities):
                if not self._is_running:
                    break

                text = entity.text
                if text not in translations:
                    # 先查缓存
                    cached = self.engine.cache.get(text, self.from_lang, self.to_lang)
                    if cached:
                        translations[text] = cached
                    else:
                        # 调用API翻译
                        translation = self.engine.client.translate(text, self.from_lang, self.to_lang)
                        translations[text] = translation
                        self.engine.cache.set(text, translation, self.from_lang, self.to_lang)

                # 发送进度
                self.progress_updated.emit(i + 1, total)

            # 完成
            self.translation_completed.emit(translations)

        except Exception as e:
            self.error_occurred.emit(str(e))

    def stop(self):
        """停止翻译"""
        self._is_running = False
```

### 3.3 算量计算模块

#### 3.3.1 算量架构

```python
# src/calculation/engine.py
from dataclasses import dataclass
from typing import List, Dict
import numpy as np
from numba import jit

@dataclass
class Component:
    """建筑构件"""
    id: str
    type: str  # 墙体、门窗、梁、柱等
    subtype: str  # 承重墙、非承重墙等
    layer: str
    entities: List[DWGEntity]
    properties: Dict[str, any]

@dataclass
class QuantityItem:
    """工程量项"""
    name: str
    specification: str
    quantity: float
    unit: str
    layer: str
    component_ids: List[str]

class CalculationEngine:
    """算量引擎"""

    def __init__(self):
        self.rules = self._load_default_rules()

    def calculate(self, document: DWGDocument) -> List[QuantityItem]:
        """计算工程量"""
        # 1. 识别构件
        components = self._identify_components(document)

        # 2. 应用算量规则
        results = []
        for component in components:
            rule = self.rules.get(component.type)
            if rule:
                quantity = self._apply_rule(component, rule)
                results.append(quantity)

        # 3. 汇总统计
        summary = self._summarize(results)

        return results

    def _identify_components(self, document: DWGDocument) -> List[Component]:
        """识别构件"""
        components = []

        # 按图层分组
        layer_groups = {}
        for entity in document.entities:
            if entity.layer not in layer_groups:
                layer_groups[entity.layer] = []
            layer_groups[entity.layer].append(entity)

        # 识别每个图层的构件
        for layer, entities in layer_groups.items():
            component_type = self._identify_component_type(layer, entities)
            if component_type:
                component = Component(
                    id=f"C_{len(components)}",
                    type=component_type,
                    subtype="",
                    layer=layer,
                    entities=entities,
                    properties={}
                )
                components.append(component)

        return components

    def _identify_component_type(self, layer: str, entities: List[DWGEntity]) -> str:
        """识别构件类型"""
        # 规则匹配
        layer_lower = layer.lower()

        if any(keyword in layer_lower for keyword in ['墙', 'wall', 'wall']):
            return '墙体'
        elif any(keyword in layer_lower for keyword in ['门', 'door', '窗', 'window']):
            return '门窗'
        elif any(keyword in layer_lower for keyword in ['梁', 'beam']):
            return '梁'
        elif any(keyword in layer_lower for keyword in ['柱', 'column']):
            return '柱'
        # ... 更多规则

        return None

    def _apply_rule(self, component: Component, rule: CalculationRule) -> QuantityItem:
        """应用算量规则"""
        if rule.calc_type == 'LENGTH':
            quantity = self._calculate_length(component)
        elif rule.calc_type == 'AREA':
            quantity = self._calculate_area(component)
        elif rule.calc_type == 'VOLUME':
            quantity = self._calculate_volume(component)
        elif rule.calc_type == 'COUNT':
            quantity = len(component.entities)
        else:
            quantity = 0

        return QuantityItem(
            name=component.type,
            specification=component.properties.get('规格', 'N/A'),
            quantity=quantity,
            unit=rule.unit,
            layer=component.layer,
            component_ids=[component.id]
        )

    @staticmethod
    @jit(nopython=True)
    def _calculate_length_fast(coordinates: np.ndarray) -> float:
        """使用Numba加速的长度计算"""
        total_length = 0.0
        for i in range(len(coordinates) - 1):
            dx = coordinates[i+1][0] - coordinates[i][0]
            dy = coordinates[i+1][1] - coordinates[i][1]
            total_length += np.sqrt(dx*dx + dy*dy)
        return total_length
```

### 3.4 图纸渲染模块

#### 3.4.1 渲染器实现

```python
# src/renderer/canvas.py
from PyQt6.QtWidgets import QWidget
from PyQt6.QtGui import QPainter, QPen, QBrush, QColor, QFont
from PyQt6.QtCore import Qt, QPointF, QRectF
from typing import List

class DWGCanvas(QWidget):
    """DWG画布"""

    def __init__(self, parent=None):
        super().__init__(parent)
        self.document: DWGDocument = None
        self.visible_entities: List[DWGEntity] = []
        self.transform = QTransform()  # 视图变换矩阵
        self.zoom_level = 1.0
        self.pan_offset = QPointF(0, 0)

        # 启用鼠标追踪
        self.setMouseTracking(True)

        # 性能优化
        self.setAttribute(Qt.WidgetAttribute.WA_OpaquePaintEvent)
        self.setAttribute(Qt.WidgetAttribute.WA_NoSystemBackground)

    def setDocument(self, document: DWGDocument):
        """设置文档"""
        self.document = document
        self._updateVisibleEntities()
        self.update()

    def paintEvent(self, event):
        """绘制事件"""
        if not self.document:
            return

        painter = QPainter(self)

        # 抗锯齿
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        painter.setRenderHint(QPainter.RenderHint.TextAntialiasing)

        # 应用变换
        painter.translate(self.pan_offset)
        painter.scale(self.zoom_level, self.zoom_level)

        # 绘制实体
        for entity in self.visible_entities:
            self._drawEntity(painter, entity)

    def _drawEntity(self, painter: QPainter, entity: DWGEntity):
        """绘制单个实体"""
        if isinstance(entity, LineEntity):
            self._drawLine(painter, entity)
        elif isinstance(entity, TextEntity):
            self._drawText(painter, entity)
        # ... 其他类型

    def _drawLine(self, painter: QPainter, line: LineEntity):
        """绘制线段"""
        pen = QPen(QColor(line.color))
        pen.setWidthF(line.linewidth)
        painter.setPen(pen)

        start = QPointF(line.start[0], line.start[1])
        end = QPointF(line.end[0], line.end[1])
        painter.drawLine(start, end)

    def _drawText(self, painter: QPainter, text: TextEntity):
        """绘制文字"""
        painter.save()

        # 设置字体
        font = QFont(text.style, int(text.height))
        painter.setFont(font)

        # 设置颜色
        painter.setPen(QColor(text.color))

        # 移动到文字位置
        pos = QPointF(text.position[0], text.position[1])

        # 旋转
        if text.rotation != 0:
            painter.translate(pos)
            painter.rotate(-text.rotation)  # CAD是逆时针，Qt是顺时针
            painter.drawText(QPointF(0, 0), text.text)
        else:
            painter.drawText(pos, text.text)

        painter.restore()

    def wheelEvent(self, event):
        """鼠标滚轮缩放"""
        delta = event.angleDelta().y()
        factor = 1.1 if delta > 0 else 0.9

        self.zoom_level *= factor
        self.zoom_level = max(0.1, min(10.0, self.zoom_level))

        self.update()

    def mousePressEvent(self, event):
        """鼠标按下"""
        if event.button() == Qt.MouseButton.MiddleButton:
            self._pan_start = event.pos()

    def mouseMoveEvent(self, event):
        """鼠标移动"""
        if event.buttons() & Qt.MouseButton.MiddleButton:
            delta = event.pos() - self._pan_start
            self.pan_offset += delta
            self._pan_start = event.pos()
            self.update()
```

#### 3.4.2 性能优化

```python
# 视锥剔除
def _updateVisibleEntities(self):
    """更新可见实体（视锥剔除）"""
    viewport_rect = self.viewport().rect()

    # 转换到世界坐标
    world_rect = self._viewportToWorld(viewport_rect)

    # 空间查询
    self.visible_entities = []
    for entity in self.document.entities:
        if self._intersects(entity, world_rect):
            self.visible_entities.append(entity)

# LOD（细节层次）
def _getLODLevel(self, entity: DWGEntity) -> int:
    """根据缩放级别返回LOD等级"""
    if self.zoom_level < 0.5:
        return 0  # 最简化
    elif self.zoom_level < 2.0:
        return 1  # 正常
    else:
        return 2  # 高精度

# 双缓冲
class OptimizedCanvas(DWGCanvas):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.cache_pixmap = None

    def paintEvent(self, event):
        if self.cache_pixmap is None or self._need_redraw:
            self._renderToCache()

        painter = QPainter(self)
        painter.drawPixmap(0, 0, self.cache_pixmap)
```

---

## 4. 数据流设计

### 4.1 图纸加载流程

```
用户操作          主线程(UI)         工作线程          数据层
   │                 │                  │                │
   ├─ 选择文件 ──────►│                  │                │
   │                 ├─ 启动解析线程 ──►│                │
   │                 │                  ├─ 读取文件 ─────►│
   │                 │                  │◄─ 返回数据 ─────┤
   │                 │                  ├─ ezdxf解析      │
   │                 │                  ├─ 构建实体对象    │
   │                 │◄─ 发送进度信号 ──┤                │
   │◄─ 更新进度条 ────┤                  │                │
   │                 │◄─ 解析完成信号 ──┤                │
   │                 ├─ 更新文档模型     │                │
   │                 ├─ 刷新画布        │                │
   │◄─ 显示图纸 ──────┤                  │                │
```

### 4.2 翻译流程

```
用户操作          主线程(UI)         翻译线程         API客户端      数据库
   │                 │                  │                │            │
   ├─ 点击翻译 ──────►│                  │                │            │
   │                 ├─ 显示进度对话框   │                │            │
   │                 ├─ 启动翻译线程 ──►│                │            │
   │                 │                  ├─ 提取文本       │            │
   │                 │                  ├─ 去重处理       │            │
   │                 │                  ├─ 查询缓存 ──────┼───────────►│
   │                 │                  │◄─ 缓存结果 ─────┼────────────┤
   │                 │                  ├─ 批量翻译 ──────►│            │
   │                 │                  │                 ├─ HTTP请求  │
   │                 │                  │◄─ 翻译结果 ──────┤            │
   │                 │                  ├─ 存入缓存 ──────┼───────────►│
   │                 │◄─ 进度信号 ──────┤                │            │
   │◄─ 更新进度 ──────┤                  │                │            │
   │                 │◄─ 完成信号 ──────┤                │            │
   │                 ├─ 更新实体文本     │                │            │
   │                 ├─ 刷新画布        │                │            │
   │◄─ 翻译完成 ──────┤                  │                │            │
```

---

## 5. 性能优化策略

### 5.1 解析优化

```python
# 1. 懒加载
class LazyDWGDocument:
    def __init__(self, filepath):
        self.filepath = filepath
        self._doc = None
        self._entities_cache = {}

    @property
    def entities(self):
        if not self._entities_cache:
            self._load_entities()
        return self._entities_cache

# 2. 增量加载
def load_entities_incremental(doc, chunk_size=1000):
    modelspace = doc.modelspace()
    for i in range(0, len(modelspace), chunk_size):
        chunk = list(modelspace)[i:i+chunk_size]
        yield [parse_entity(e) for e in chunk]

# 3. 内存映射（大文件）
import mmap

def parse_large_file(filepath):
    with open(filepath, 'rb') as f:
        with mmap.mmap(f.fileno(), 0, access=mmap.ACCESS_READ) as mmapped:
            # 使用内存映射读取
            pass
```

### 5.2 渲染优化

```python
# 1. 图层分组绘制
class LayerRenderer:
    def render(self, layers: List[Layer]):
        for layer in layers:
            if not layer.visible:
                continue

            # 同一图层批量绘制
            self._batch_render_layer(layer)

# 2. 绘制缓存
from functools import lru_cache

class CachedRenderer:
    @lru_cache(maxsize=1000)
    def get_entity_path(self, entity_id: str) -> QPainterPath:
        # 缓存复杂图形的QPainterPath
        pass

# 3. 多线程预渲染
class PreRenderer(QThread):
    def run(self):
        # 在后台线程预先计算复杂图形
        for entity in self.entities:
            path = self.compute_path(entity)
            self.cache[entity.id] = path
```

### 5.3 算量优化

```python
# 使用Numba JIT加速
from numba import jit
import numpy as np

@jit(nopython=True)
def calculate_lengths(coordinates: np.ndarray) -> np.ndarray:
    """批量计算长度（Numba加速）"""
    n = coordinates.shape[0]
    lengths = np.zeros(n - 1)

    for i in range(n - 1):
        dx = coordinates[i+1, 0] - coordinates[i, 0]
        dy = coordinates[i+1, 1] - coordinates[i, 1]
        lengths[i] = np.sqrt(dx*dx + dy*dy)

    return lengths

# 并行计算
from concurrent.futures import ProcessPoolExecutor

def calculate_parallel(components: List[Component]):
    with ProcessPoolExecutor() as executor:
        results = list(executor.map(calculate_single_component, components))
    return results
```

---

## 6. 安全性设计

### 6.1 API密钥管理

```python
# src/utils/keyring_manager.py
import keyring

class SecureStorage:
    """安全存储（使用系统密钥链）"""

    SERVICE_NAME = "biaoge-dwg-translator"

    @staticmethod
    def save_api_key(key: str):
        """保存API密钥"""
        keyring.set_password(SecureStorage.SERVICE_NAME, "bailian-api", key)

    @staticmethod
    def get_api_key() -> str:
        """获取API密钥"""
        return keyring.get_password(SecureStorage.SERVICE_NAME, "bailian-api")

    @staticmethod
    def delete_api_key():
        """删除API密钥"""
        keyring.delete_password(SecureStorage.SERVICE_NAME, "bailian-api")
```

### 6.2 数据加密

```python
# SQLite数据库加密（使用sqlcipher）
import sqlcipher3

class EncryptedDatabase:
    def __init__(self, db_path: str, password: str):
        self.conn = sqlcipher3.connect(db_path)
        self.conn.execute(f"PRAGMA key = '{password}'")
```

---

## 7. 错误处理

### 7.1 异常分类

```python
# src/exceptions.py

class BiaoGeException(Exception):
    """基础异常"""
    pass

class DWGParseError(BiaoGeException):
    """DWG解析错误"""
    pass

class TranslationError(BiaoGeException):
    """翻译错误"""
    pass

class CalculationError(BiaoGeException):
    """算量错误"""
    pass

class APIError(BiaoGeException):
    """API调用错误"""
    def __init__(self, message: str, status_code: int = None):
        super().__init__(message)
        self.status_code = status_code
```

### 7.2 错误处理机制

```python
# 统一错误处理
from PyQt6.QtWidgets import QMessageBox

class ErrorHandler:
    @staticmethod
    def handle(exception: Exception, parent=None):
        """统一错误处理"""
        if isinstance(exception, DWGParseError):
            QMessageBox.critical(
                parent,
                "解析错误",
                f"无法解析DWG文件：{str(exception)}"
            )
        elif isinstance(exception, APIError):
            if exception.status_code == 401:
                QMessageBox.warning(
                    parent,
                    "API错误",
                    "API密钥无效，请检查设置"
                )
            elif exception.status_code == 429:
                QMessageBox.warning(
                    parent,
                    "API错误",
                    "请求频率超限，请稍后再试"
                )
            else:
                QMessageBox.critical(
                    parent,
                    "API错误",
                    f"API调用失败：{str(exception)}"
                )
        else:
            QMessageBox.critical(
                parent,
                "未知错误",
                f"发生错误：{str(exception)}"
            )
```

---

## 8. 部署架构

### 8.1 打包方案

```python
# build.spec (PyInstaller配置)
# -*- mode: python ; coding: utf-8 -*-

block_cipher = None

a = Analysis(
    ['main.py'],
    pathex=[],
    binaries=[],
    datas=[
        ('resources', 'resources'),
        ('config.toml', '.'),
    ],
    hiddenimports=[
        'ezdxf',
        'numpy',
        'PyQt6',
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    win_no_prefer_redirects=False,
    win_private_assemblies=False,
    cipher=block_cipher,
    noarchive=False,
)

pyz = PYZ(a.pure, a.zipped_data, cipher=block_cipher)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.zipfiles,
    a.datas,
    [],
    name='BiaoGe',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,  # 不显示控制台
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
    icon='resources/icon.ico'  # 应用图标
)
```

### 8.2 构建命令

```bash
# Windows
pyinstaller build.spec

# 输出目录
dist/BiaoGe.exe  # 单文件可执行程序（约100MB）

# 创建安装包（使用NSIS）
makensis installer.nsi
```

---

## 9. 监控与日志

### 9.1 日志系统

```python
# src/utils/logger.py
import logging
from pathlib import Path

def setup_logger():
    """配置日志系统"""
    log_dir = Path.home() / '.biaoge' / 'logs'
    log_dir.mkdir(parents=True, exist_ok=True)

    log_file = log_dir / 'app.log'

    logging.basicConfig(
        level=logging.INFO,
        format='%(asctime)s [%(levelname)s] %(name)s: %(message)s',
        handlers=[
            logging.FileHandler(log_file, encoding='utf-8'),
            logging.StreamHandler()
        ]
    )

# 使用
import logging
logger = logging.getLogger(__name__)

logger.info("DWG文件加载成功: %s", filepath)
logger.warning("翻译配额即将用尽: %d%%", usage_percent)
logger.error("API调用失败: %s", error_msg, exc_info=True)
```

---

## 10. 技术债务与风险

### 10.1 已知限制

| 限制 | 影响 | 缓解方案 |
|------|------|----------|
| ezdxf解析速度 | 大文件(50MB+)加载慢 | 并行解析、增量加载 |
| Python GIL | 多核利用率低 | 使用多进程、Numba加速 |
| 打包体积大 | 100MB左右 | 可接受（用户不太在意） |
| PyQt商业授权 | GPL/商业双授权 | 开源版本免费 |

### 10.2 技术演进

**短期**（v1.0）:
- ✅ 基于ezdxf的完整解析
- ✅ QPainter原生渲染
- ✅ 基础算量功能

**中期**（v1.5）:
- 考虑C++扩展加速关键路径
- WebGPU渲染（PyQt6.6+支持）
- 更复杂的算量规则

**长期**（v2.0）:
- BIM集成（IFC格式）
- 云端协作
- 本地离线翻译模型

---

## 11. 总结

### 11.1 PyQt6方案优势

| 优势 | 说明 |
|------|------|
| **渲染性能** | QPainter硬件加速，百万实体流畅 |
| **Windows原生** | 完美适配Windows，用户体验好 |
| **开发效率** | Python简单，招聘容易，迭代快 |
| **CAD生态** | ezdxf成熟，大量参考项目 |
| **稳定可靠** | Qt框架30年历史，经过验证 |

### 11.2 关键技术决策

| 分类 | 技术选择 | 理由 |
|------|----------|------|
| GUI框架 | PyQt6 6.6+ | 性能强、原生体验、现代化 |
| DWG解析 | ezdxf 1.1+ | 纯Python、易集成、MIT协议 |
| 图纸渲染 | QPainter | CAD级性能、硬件加速 |
| 大模型 | 阿里云百炼 | 性价比高、中文强、合规 |
| 算量加速 | Numba JIT | 接近C速度、易用 |
| 打包工具 | PyInstaller 6.0+ | 成熟稳定、单文件打包 |

---

**文档结束**
