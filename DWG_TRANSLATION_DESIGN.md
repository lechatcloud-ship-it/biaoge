# DWG图纸翻译修改技术设计方案

## 🎯 核心目标

**生成翻译后的DWG图纸，与原图纸完全一样，只有文字内容不同，其他任何属性、位置、格式都不能有丝毫差异。**

---

## ⚠️ 核心挑战分析

### 挑战1：DWG文本实体的复杂性

DWG文件中的文本不是简单的字符串，而是包含大量属性的复杂实体：

#### 1.1 文本实体类型（至少6种）

| 类型 | 说明 | 复杂度 | 风险等级 |
|------|------|--------|---------|
| **TEXT** | 单行文本 | ⭐ 低 | 🟢 低 |
| **MTEXT** | 多行文本（支持富文本格式） | ⭐⭐⭐ 高 | 🟡 中 |
| **DIMENSION** | 尺寸标注（包含数值+文本） | ⭐⭐⭐⭐ 很高 | 🔴 高 |
| **LEADER** | 引线标注（箭头+文本） | ⭐⭐⭐ 高 | 🟡 中 |
| **ATTRIB** | 块属性文本 | ⭐⭐⭐⭐ 很高 | 🔴 高 |
| **TABLE** | 表格文本 | ⭐⭐⭐⭐⭐ 极高 | 🔴 很高 |

#### 1.2 每个文本实体的关键属性（必须完全保持）

```python
TEXT实体必须保持的属性（20+个）：
- dxf.text          # 文本内容 ✅ 这是唯一要改的
- dxf.insert        # 插入点位置 (x, y, z) ❌ 不能变
- dxf.height        # 文字高度 ❌ 不能变
- dxf.rotation      # 旋转角度 ❌ 不能变
- dxf.width_factor  # 宽度因子 ❌ 不能变
- dxf.oblique       # 倾斜角度 ❌ 不能变
- dxf.style         # 文本样式 ❌ 不能变
- dxf.layer         # 图层 ❌ 不能变
- dxf.color         # 颜色 ❌ 不能变
- dxf.linetype      # 线型 ❌ 不能变
- dxf.lineweight    # 线宽 ❌ 不能变
- dxf.halign        # 水平对齐 ❌ 不能变
- dxf.valign        # 垂直对齐 ❌ 不能变
- dxf.text_generation_flag  # 文本生成标志 ❌ 不能变
... 还有更多
```

#### 1.3 MTEXT的特殊复杂性

MTEXT支持富文本格式，使用特殊标记：

```
原始MTEXT内容：
"{\\fSimSun|b0|i0|c134|p34;这是\\C1;红色\\C7;文字}"

包含：
- 字体控制：\\f字体名
- 粗体/斜体：\\b1, \\i1
- 颜色控制：\\C颜色码
- 换行符：\\P
- 对齐：\\A1, \\A2
- 上下标：\\S
... 等等
```

**风险**：如果破坏这些格式标记，文本将显示错误！

---

### 挑战2：文本内容的复杂性

#### 2.1 文本类型分类

| 类型 | 示例 | 处理策略 | 风险 |
|------|------|----------|------|
| **纯数字** | `3000`, `45.5` | ❌ **不翻译** | 🟢 如果误翻译会导致数据错误 |
| **单位** | `mm`, `m²`, `kg/m³` | ⚠️ **规则转换**（可选） | 🟡 必须正确映射 |
| **纯文本** | `卧室`, `Living Room` | ✅ **AI翻译** | 🟢 主要翻译对象 |
| **混合文本** | `3000mm`, `φ200` | ⚠️ **智能拆分** | 🔴 最高风险！ |
| **特殊符号** | `φ`, `≥`, `±`, `℃` | ❌ **保持不变** | 🔴 如果丢失会导致语义错误 |
| **公式/表达式** | `A=πr²`, `1:100` | ❌ **不翻译** | 🔴 数学/比例不能变 |

#### 2.2 真实案例分析

```python
案例1：尺寸标注
原文: "3000"  # 纯数字，墙体长度
错误: "3,000" 或 "三千"  # ❌ 灾难性错误！
正确: "3000"  # ✅ 完全不变

案例2：房间名称
原文: "卧室"
错误: "Bedroom" (可能太长，覆盖其他内容)  # ⚠️ 布局问题
正确: 检查长度，必要时缩小字体或使用缩写 "BR"

案例3：混合文本
原文: "混凝土强度≥C30"
错误: "Concrete strength ≥ C30" (符号丢失) # ❌ 错误
正确: "Concrete strength ≥ C30" (保留≥和C30) # ✅

案例4：MTEXT格式文本
原文: "{\\fSimSun;第一层\\P第二层}"  # 两行文字
错误: "{\\fSimSun;First FloorSecond Floor}"  # ❌ 破坏换行
正确: "{\\fSimSun;First Floor\\PSecond Floor}" # ✅ 保留\\P
```

---

### 挑战3：翻译一致性

#### 3.1 术语一致性问题

```
问题：同一图纸中，相同的术语必须翻译一致

例子：
❌ 错误（不一致）：
  - "卧室" → "Bedroom"
  - "卧室" → "Sleeping Room"
  - "卧室" → "BR"

✅ 正确（一致）：
  - "卧室" → "Bedroom" (所有地方)
```

**解决方案**：术语库 + 翻译记忆

#### 3.2 上下文理解

```
问题：CAD文本需要结合图形理解

例子1：
图纸位置：厨房区域
文本："水槽"
正确翻译："Sink" ✅
错误翻译："Water Tank" ❌ (字面翻译)

例子2：
图纸位置：平面图上的箭头
文本："N"
正确翻译："N" ✅ (保持，这是北向标记)
错误翻译："Nitrogen" ❌ (误解)
```

**解决方案**：多模态AI + 上下文窗口

---

### 挑战4：图纸修改的精确性

#### 4.1 必须保证的不变性

```python
✅ 必须保证：
1. 文件结构完全一致
   - 所有非文本实体：byte-level 一致
   - 实体顺序：完全一致
   - 图层结构：完全一致
   - 块定义：完全一致

2. 文本实体属性完全一致（除了text内容）
   - 位置：精确到10^-10（双精度浮点数）
   - 大小：完全一致
   - 旋转：完全一致
   - 样式：完全一致
   - 图层：完全一致

3. 文件可用性
   - AutoCAD可以正常打开
   - 所有CAD软件都可以正常打开
   - 没有任何警告或错误
```

#### 4.2 修改方法对比

| 方法 | 优点 | 缺点 | 推荐 |
|------|------|------|------|
| **方法1：直接修改** | • 最简单<br>• 保真度最高 | • 必须处理每种实体类型 | ✅ **推荐** |
| **方法2：删除重建** | • 代码简单 | • ❌ 风险极高<br>• ❌ 可能丢失属性<br>• ❌ 可能改变顺序 | ❌ **不推荐** |
| **方法3：导出导入** | • 可以用其他库 | • ❌ 格式转换风险<br>• ❌ 信息丢失 | ❌ **不推荐** |

**结论：必须使用方法1 - 直接修改text属性**

---

## ✅ 完整解决方案设计

### 架构总览

```
┌─────────────────────────────────────────────────────────────┐
│                    DWG翻译修改系统                            │
└─────────────────────────────────────────────────────────────┘
                              │
    ┌─────────────────────────┼─────────────────────────┐
    │                         │                         │
    ▼                         ▼                         ▼
┌─────────┐            ┌─────────┐              ┌─────────┐
│阶段1     │            │阶段2     │              │阶段3     │
│智能提取  │  ───────▶  │智能翻译  │  ──────────▶ │精确修改  │
└─────────┘            └─────────┘              └─────────┘
    │                         │                         │
    │                         │                         │
    ▼                         ▼                         ▼
┌─────────┐            ┌─────────┐              ┌─────────┐
│阶段4     │            │阶段5     │              │阶段6     │
│严格验证  │  ───────▶  │人工审查  │  ──────────▶ │最终输出  │
└─────────┘            └─────────┘              └─────────┘
```

---

### 阶段1：智能文本提取

#### 1.1 提取所有类型的文本实体

```python
class TextExtractor:
    """智能文本提取器"""

    def extract_all_texts(self, dwg_document) -> List[TextEntity]:
        """提取所有文本实体"""

        texts = []

        # 1. TEXT - 单行文本（最常见）
        for entity in modelspace.query('TEXT'):
            texts.append(self._extract_text(entity))

        # 2. MTEXT - 多行文本（复杂格式）
        for entity in modelspace.query('MTEXT'):
            texts.append(self._extract_mtext(entity))

        # 3. DIMENSION - 尺寸标注（高风险！）
        for entity in modelspace.query('DIMENSION'):
            texts.append(self._extract_dimension(entity))

        # 4. LEADER/MULTILEADER - 引线标注
        for entity in modelspace.query('LEADER'):
            texts.append(self._extract_leader(entity))

        # 5. ATTRIB - 块属性（在块定义中）
        for block in doc.blocks:
            for entity in block.query('ATTRIB'):
                texts.append(self._extract_attrib(entity))

        # 6. TABLE - 表格文本
        for entity in modelspace.query('TABLE'):
            texts.append(self._extract_table(entity))

        return texts
```

#### 1.2 记录完整的实体信息

```python
@dataclass
class ExtractedText:
    """提取的文本实体（完整信息）"""

    # 唯一标识
    entity_id: str           # 实体句柄
    entity_type: str         # TEXT/MTEXT/DIMENSION/等

    # 原始实体引用（用于后续修改）
    entity_ref: Any          # ezdxf entity对象引用

    # 文本内容
    original_text: str       # 原始文本
    translated_text: str     # 翻译后文本（初始为空）

    # 完整属性（必须保持）
    position: Tuple[float, float, float]  # 位置
    height: float                         # 高度
    rotation: float                       # 旋转
    style: str                            # 样式名
    layer: str                            # 图层名
    color: int                            # 颜色
    # ... 所有其他属性

    # 上下文信息（用于翻译）
    nearby_entities: List[Entity]  # 周围的图形实体
    nearby_texts: List[str]        # 周围的文本
    text_classification: str       # 文本分类

    # 元数据
    confidence: float = 0.0        # 翻译置信度
    needs_review: bool = False     # 是否需要人工审查
    warning_message: str = ""      # 警告信息
```

---

### 阶段2：智能翻译引擎

#### 2.1 文本分类器

```python
class TextClassifier:
    """文本分类器 - 判断如何处理每个文本"""

    def classify(self, text: str) -> TextCategory:
        """
        分类文本，决定翻译策略

        返回类型：
        - PURE_NUMBER: 纯数字，不翻译
        - UNIT: 单位符号，可选转换
        - PURE_TEXT: 纯文本，AI翻译
        - MIXED: 混合文本，智能拆分
        - SPECIAL_SYMBOL: 特殊符号，保持
        - FORMULA: 公式，不翻译
        """

        # 1. 纯数字检测
        if self._is_pure_number(text):
            return TextCategory.PURE_NUMBER

        # 2. 单位符号检测
        if self._is_unit(text):
            return TextCategory.UNIT

        # 3. 特殊符号检测（φ, ∅, ≥, ±, ℃等）
        if self._contains_special_symbols(text):
            return TextCategory.SPECIAL_SYMBOL

        # 4. 公式检测（A=πr², 1:100等）
        if self._is_formula(text):
            return TextCategory.FORMULA

        # 5. 混合文本检测（3000mm, φ200等）
        if self._is_mixed(text):
            return TextCategory.MIXED

        # 6. 纯文本（默认）
        return TextCategory.PURE_TEXT
```

#### 2.2 上下文感知翻译引擎

```python
class ContextAwareTranslator:
    """上下文感知翻译引擎"""

    def __init__(self):
        self.terminology_db = TerminologyDatabase()  # 术语库
        self.translation_memory = {}                 # 翻译记忆
        self.client = BailianClient()

    def translate_with_context(
        self,
        text: ExtractedText,
        all_texts: List[ExtractedText]
    ) -> TranslationResult:
        """
        结合上下文进行翻译

        策略：
        1. 检查翻译记忆（确保一致性）
        2. 检查术语库（专业术语）
        3. 使用AI翻译（提供上下文）
        4. 质量检查
        """

        # 策略1：翻译记忆（一致性保证）
        if text.original_text in self.translation_memory:
            return self.translation_memory[text.original_text]

        # 策略2：术语库匹配
        term_match = self.terminology_db.match(text.original_text)
        if term_match:
            result = TranslationResult(
                translation=term_match.target_term,
                confidence=1.0,
                source="terminology_db"
            )
            self.translation_memory[text.original_text] = result
            return result

        # 策略3：AI翻译（提供丰富上下文）
        context = self._build_context(text, all_texts)

        prompt = f"""
你是专业的CAD图纸翻译专家。请翻译以下文本：

原文：{text.original_text}

上下文信息：
- 文本类型：{text.entity_type}
- 所在图层：{text.layer}
- 周围文本：{context['nearby_texts']}
- 图纸类型：建筑平面图

翻译要求：
1. 使用专业建筑术语
2. 保持简洁（原文长度：{len(text.original_text)}字符，译文建议不超过{len(text.original_text) * 2}字符）
3. 保留所有数字和特殊符号
4. 如果是标准缩写，使用标准英文缩写

只返回翻译结果，不要解释。
"""

        translation = self.client.translate(prompt)

        # 策略4：质量检查
        quality_check = self._check_translation_quality(
            text.original_text,
            translation,
            text
        )

        result = TranslationResult(
            translation=translation,
            confidence=quality_check.confidence,
            warnings=quality_check.warnings,
            needs_review=quality_check.needs_review
        )

        # 记录到翻译记忆
        self.translation_memory[text.original_text] = result

        return result
```

#### 2.3 混合文本智能处理

```python
class MixedTextHandler:
    """混合文本智能处理器"""

    def handle_mixed_text(self, text: str) -> str:
        """
        处理混合文本（如"3000mm", "φ200"）

        策略：
        1. 解析出数字、单位、文字部分
        2. 只翻译文字部分
        3. 保持数字和符号不变
        4. 按正确顺序组装
        """

        # 示例："混凝土强度≥C30"
        # 解析：["混凝土强度", "≥", "C30"]
        parts = self._parse_mixed_text(text)

        translated_parts = []
        for part in parts:
            if part.type == 'text':
                # 翻译文字部分
                translated_parts.append(self.translate(part.content))
            else:
                # 保持数字、符号、单位不变
                translated_parts.append(part.content)

        # 重新组装
        result = ''.join(translated_parts)

        # 示例结果："Concrete strength ≥ C30"
        return result
```

#### 2.4 MTEXT格式保持

```python
class MTextFormatter:
    """MTEXT格式保持器"""

    def translate_mtext(self, mtext_content: str) -> str:
        """
        翻译MTEXT，保持所有格式标记

        输入："{\\fSimSun|b0;第一行\\P第二行}"
        输出："{\\fSimSun|b0;First Line\\PSecond Line}"

        关键：保持所有\\开头的格式标记
        """

        # 1. 解析MTEXT，提取格式标记和纯文本
        parsed = self._parse_mtext(mtext_content)
        # 结果：[
        #   FormatTag("{\\fSimSun|b0;"),
        #   Text("第一行"),
        #   FormatTag("\\P"),
        #   Text("第二行"),
        #   FormatTag("}")
        # ]

        # 2. 只翻译Text部分
        for item in parsed:
            if isinstance(item, Text):
                item.content = self.translate(item.content)

        # 3. 重新组装，保持所有格式
        result = ''.join(item.content for item in parsed)

        return result
```

---

### 阶段3：精确图纸修改

#### 3.1 非破坏性修改器

```python
class PrecisionDWGModifier:
    """精确DWG修改器 - 保证一模一样"""

    def modify_dwg(
        self,
        dwg_path: str,
        translations: List[ExtractedText],
        output_path: str
    ) -> ModificationResult:
        """
        修改DWG文件，只改变文本内容，其他完全不变

        核心原则：
        1. 只修改entity.dxf.text属性
        2. 不创建新实体
        3. 不删除实体
        4. 不改变任何其他属性
        5. 不改变实体顺序
        """

        # 1. 读取DWG
        doc = ezdxf.readfile(dwg_path)
        modelspace = doc.modelspace()

        # 2. 修改统计
        stats = ModificationStats()

        # 3. 遍历所有需要翻译的文本
        for trans in translations:
            if not trans.translated_text:
                continue  # 跳过没有翻译的

            try:
                # 获取原始实体引用
                entity = trans.entity_ref

                # 根据实体类型进行修改
                if trans.entity_type == 'TEXT':
                    self._modify_text(entity, trans)

                elif trans.entity_type == 'MTEXT':
                    self._modify_mtext(entity, trans)

                elif trans.entity_type == 'DIMENSION':
                    self._modify_dimension(entity, trans)

                elif trans.entity_type == 'LEADER':
                    self._modify_leader(entity, trans)

                elif trans.entity_type == 'ATTRIB':
                    self._modify_attrib(entity, trans)

                elif trans.entity_type == 'TABLE':
                    self._modify_table(entity, trans)

                stats.success_count += 1

            except Exception as e:
                stats.error_count += 1
                stats.errors.append({
                    'entity_id': trans.entity_id,
                    'error': str(e)
                })
                logger.error(f"修改实体失败: {trans.entity_id}, {e}")

        # 4. 保存修改后的文件
        doc.saveas(output_path)

        # 5. 返回修改结果
        return ModificationResult(
            success=True,
            stats=stats,
            output_path=output_path
        )

    def _modify_text(self, entity, trans: ExtractedText):
        """修改TEXT实体 - 最简单"""
        # ✅ 只改这一行！
        entity.dxf.text = trans.translated_text
        # ❌ 不改任何其他属性！

    def _modify_mtext(self, entity, trans: ExtractedText):
        """修改MTEXT实体 - 保持格式"""
        # ✅ 只改text属性，保持所有格式标记
        entity.dxf.text = trans.translated_text

    def _modify_dimension(self, entity, trans: ExtractedText):
        """
        修改DIMENSION实体 - 高风险！

        注意：尺寸标注通常不应该翻译数值部分，
        只应该翻译前缀/后缀文本
        """
        # 检查是否只是覆盖文本（override text）
        if hasattr(entity.dxf, 'text_override'):
            entity.dxf.text_override = trans.translated_text
        else:
            # 标准尺寸，可能需要设置前缀/后缀
            # 这里需要非常小心！
            logger.warning(f"尝试修改标准尺寸标注: {trans.entity_id}")
```

#### 3.2 文本长度自适应

```python
class TextLengthAdapter:
    """文本长度自适应器"""

    def adapt_text_length(
        self,
        original_text: str,
        translated_text: str,
        entity: ExtractedText
    ) -> str:
        """
        如果翻译后文本过长，进行智能调整

        策略：
        1. 检查长度比例
        2. 如果过长，尝试使用缩写
        3. 如果还是太长，提醒用户需要人工调整
        """

        # 1. 计算长度比例
        length_ratio = len(translated_text) / len(original_text)

        # 2. 如果长度在合理范围（2倍以内），直接返回
        if length_ratio <= 2.0:
            return translated_text

        # 3. 尝试使用标准缩写
        if length_ratio > 2.0:
            abbreviated = self._try_abbreviation(translated_text)
            if len(abbreviated) / len(original_text) <= 2.0:
                return abbreviated

        # 4. 如果还是太长，标记需要人工审查
        entity.needs_review = True
        entity.warning_message = (
            f"翻译后文本过长（{len(translated_text)}字符 vs "
            f"{len(original_text)}字符），可能覆盖其他内容，请人工审查"
        )

        return translated_text  # 仍然返回，但标记为需要审查
```

---

### 阶段4：严格验证系统

#### 4.1 完整性验证

```python
class DWGIntegrityValidator:
    """DWG完整性验证器"""

    def validate(
        self,
        original_path: str,
        modified_path: str,
        translations: List[ExtractedText]
    ) -> ValidationReport:
        """
        严格验证修改后的DWG文件

        检查项：
        1. 文件可以正常打开
        2. 实体数量完全一致
        3. 非文本实体完全一致（byte-level）
        4. 文本实体属性完全一致（除了text内容）
        5. 图层结构完全一致
        6. 块定义完全一致
        """

        report = ValidationReport()

        # 1. 打开两个文件
        try:
            original_doc = ezdxf.readfile(original_path)
            modified_doc = ezdxf.readfile(modified_path)
        except Exception as e:
            report.add_error(f"文件打开失败: {e}")
            return report

        # 2. 检查实体数量
        original_count = len(list(original_doc.modelspace()))
        modified_count = len(list(modified_doc.modelspace()))

        if original_count != modified_count:
            report.add_critical_error(
                f"实体数量不一致: {original_count} vs {modified_count}"
            )

        # 3. 检查每个实体
        original_entities = list(original_doc.modelspace())
        modified_entities = list(modified_doc.modelspace())

        for i, (orig, modi) in enumerate(zip(original_entities, modified_entities)):
            # 3.1 检查实体类型
            if orig.dxftype() != modi.dxftype():
                report.add_critical_error(
                    f"实体{i}类型不一致: {orig.dxftype()} vs {modi.dxftype()}"
                )
                continue

            # 3.2 如果是文本实体，检查除text外的所有属性
            if orig.dxftype() in ['TEXT', 'MTEXT']:
                self._validate_text_entity(orig, modi, report)
            else:
                # 3.3 非文本实体必须完全一致
                self._validate_non_text_entity(orig, modi, report)

        # 4. 检查图层
        self._validate_layers(original_doc, modified_doc, report)

        # 5. 检查块定义
        self._validate_blocks(original_doc, modified_doc, report)

        return report

    def _validate_text_entity(self, orig, modi, report):
        """验证文本实体（除text外完全一致）"""

        # 检查所有属性（除了text）
        text_attrs_to_check = [
            'insert', 'height', 'rotation', 'width_factor',
            'oblique', 'style', 'layer', 'color', 'linetype',
            'lineweight', 'halign', 'valign'
        ]

        for attr in text_attrs_to_check:
            if hasattr(orig.dxf, attr) and hasattr(modi.dxf, attr):
                orig_val = getattr(orig.dxf, attr)
                modi_val = getattr(modi.dxf, attr)

                # 浮点数比较（容差10^-10）
                if isinstance(orig_val, float):
                    if abs(orig_val - modi_val) > 1e-10:
                        report.add_error(
                            f"TEXT实体属性{attr}不一致: "
                            f"{orig_val} vs {modi_val}"
                        )
                else:
                    if orig_val != modi_val:
                        report.add_error(
                            f"TEXT实体属性{attr}不一致: "
                            f"{orig_val} vs {modi_val}"
                        )
```

#### 4.2 翻译质量评分

```python
class TranslationQualityScorer:
    """翻译质量评分器"""

    def score_translation(
        self,
        original: str,
        translation: str,
        context: Dict
    ) -> QualityScore:
        """
        评估翻译质量

        评分维度：
        1. 长度合理性（0-25分）
        2. 格式保持（0-25分）
        3. 语义准确性（0-25分）
        4. 专业性（0-25分）

        总分：0-100分
        """

        score = QualityScore()

        # 1. 长度合理性
        length_ratio = len(translation) / max(len(original), 1)
        if length_ratio <= 2.0:
            score.length_score = 25
        elif length_ratio <= 3.0:
            score.length_score = 15
        else:
            score.length_score = 5  # 过长，很可能有问题

        # 2. 格式保持（检查是否保留了特殊符号）
        special_symbols = ['φ', '∅', '≥', '≤', '±', '℃', '°']
        format_score = 25
        for symbol in special_symbols:
            if symbol in original and symbol not in translation:
                format_score -= 5  # 每丢失一个符号扣5分
        score.format_score = max(0, format_score)

        # 3. 语义准确性（使用AI评估）
        # 这里可以用一个小模型快速评估
        semantic_score = self._evaluate_semantic_accuracy(
            original, translation, context
        )
        score.semantic_score = semantic_score

        # 4. 专业性（检查是否使用了专业术语）
        terminology_score = self._evaluate_terminology(translation)
        score.terminology_score = terminology_score

        # 总分
        score.total_score = (
            score.length_score +
            score.format_score +
            score.semantic_score +
            score.terminology_score
        )

        # 置信度
        score.confidence = score.total_score / 100.0

        # 是否需要审查
        if score.total_score < 70:
            score.needs_review = True
            score.warning = "质量评分较低，建议人工审查"

        return score
```

---

### 阶段5：人工审查界面

#### 5.1 对比视图设计

```python
class TranslationReviewDialog(QDialog):
    """翻译审查对话框"""

    def __init__(self, original_doc, translations):
        super().__init__()

        self.setWindowTitle("翻译审查 - 逐项检查")
        self.resize(1600, 900)

        # 主布局：左右分屏
        main_layout = QHBoxLayout()

        # 左侧：原始图纸
        left_panel = QVBoxLayout()
        left_panel.addWidget(QLabel("原始图纸"))
        self.original_viewer = DWGCanvas()
        self.original_viewer.setDocument(original_doc)
        left_panel.addWidget(self.original_viewer)

        # 右侧：翻译后图纸（预览）
        right_panel = QVBoxLayout()
        right_panel.addWidget(QLabel("翻译后图纸（预览）"))
        self.translated_viewer = DWGCanvas()
        self.translated_viewer.setDocument(self._create_preview_doc())
        right_panel.addWidget(self.translated_viewer)

        main_layout.addLayout(left_panel)
        main_layout.addLayout(right_panel)

        # 底部：翻译列表和编辑区
        bottom_panel = self._create_bottom_panel(translations)

        # 总布局
        layout = QVBoxLayout()
        layout.addLayout(main_layout, stretch=7)
        layout.addWidget(bottom_panel, stretch=3)

        self.setLayout(layout)

    def _create_bottom_panel(self, translations):
        """创建底部翻译列表和编辑区"""

        panel = QWidget()
        layout = QVBoxLayout()

        # 翻译列表（表格）
        self.translation_table = QTableWidget()
        self.translation_table.setColumnCount(6)
        self.translation_table.setHorizontalHeaderLabels([
            "原文", "译文", "类型", "图层", "质量评分", "操作"
        ])

        # 填充数据
        self.translation_table.setRowCount(len(translations))
        for i, trans in enumerate(translations):
            # 原文
            self.translation_table.setItem(
                i, 0, QTableWidgetItem(trans.original_text)
            )

            # 译文（可编辑）
            translated_item = QTableWidgetItem(trans.translated_text)
            translated_item.setBackground(QColor(255, 255, 200))  # 高亮
            self.translation_table.setItem(i, 1, translated_item)

            # 类型
            self.translation_table.setItem(
                i, 2, QTableWidgetItem(trans.entity_type)
            )

            # 图层
            self.translation_table.setItem(
                i, 3, QTableWidgetItem(trans.layer)
            )

            # 质量评分
            score = trans.confidence * 100
            score_item = QTableWidgetItem(f"{score:.0f}")
            if score >= 80:
                score_item.setForeground(QColor(0, 150, 0))  # 绿色
            elif score >= 60:
                score_item.setForeground(QColor(200, 150, 0))  # 黄色
            else:
                score_item.setForeground(QColor(200, 0, 0))  # 红色
            self.translation_table.setItem(i, 4, score_item)

            # 操作按钮
            btn_widget = QWidget()
            btn_layout = QHBoxLayout()
            accept_btn = QPushButton("✓")
            reject_btn = QPushButton("✗")
            btn_layout.addWidget(accept_btn)
            btn_layout.addWidget(reject_btn)
            btn_widget.setLayout(btn_layout)
            self.translation_table.setCellWidget(i, 5, btn_widget)

        layout.addWidget(QLabel("翻译列表（双击可编辑译文）"))
        layout.addWidget(self.translation_table)

        # 批量操作按钮
        button_layout = QHBoxLayout()
        accept_all_btn = QPushButton("✓ 接受全部")
        reject_all_btn = QPushButton("✗ 拒绝全部")
        export_review_btn = QPushButton("📄 导出审查报告")
        button_layout.addWidget(accept_all_btn)
        button_layout.addWidget(reject_all_btn)
        button_layout.addWidget(export_review_btn)
        button_layout.addStretch()

        layout.addLayout(button_layout)

        panel.setLayout(layout)
        return panel
```

#### 5.2 实时预览

```python
class LivePreview:
    """实时预览功能"""

    def __init__(self, viewer_widget):
        self.viewer = viewer_widget
        self.current_translations = []

    def update_preview(self, translations: List[ExtractedText]):
        """
        实时更新预览图纸

        不需要真正修改DWG文件，只是在渲染时显示翻译后的文本
        """

        # 更新渲染器的翻译映射
        translation_map = {
            trans.entity_id: trans.translated_text
            for trans in translations
            if trans.translated_text
        }

        # 通知渲染器使用新的翻译
        self.viewer.set_translation_map(translation_map)

        # 重新渲染
        self.viewer.update()
```

---

## 🧪 测试策略

### 测试用例设计

```python
class DWGTranslationTests:
    """DWG翻译修改测试套件"""

    def test_01_simple_text_translation(self):
        """测试1：简单TEXT实体翻译"""
        # 创建简单DWG，包含一个TEXT
        # 翻译
        # 验证：只有text改变，其他属性完全不变

    def test_02_mtext_format_preservation(self):
        """测试2：MTEXT格式保持"""
        # 创建包含格式的MTEXT
        # 翻译
        # 验证：格式标记完全保留

    def test_03_mixed_text_handling(self):
        """测试3：混合文本处理"""
        # 测试"3000mm", "φ200", "≥C30"等
        # 验证：数字和符号不变

    def test_04_pure_number_preservation(self):
        """测试4：纯数字不翻译"""
        # 测试尺寸标注中的数字
        # 验证：完全不变

    def test_05_dimension_safety(self):
        """测试5：尺寸标注安全性"""
        # 测试DIMENSION实体
        # 验证：不破坏尺寸数值

    def test_06_entity_count_consistency(self):
        """测试6：实体数量一致性"""
        # 验证：修改前后实体数量完全相同

    def test_07_non_text_entity_immutability(self):
        """测试7：非文本实体不可变性"""
        # 验证：LINE, CIRCLE等完全不变（byte-level）

    def test_08_layer_structure_preservation(self):
        """测试8：图层结构保持"""
        # 验证：图层数量、属性完全一致

    def test_09_file_compatibility(self):
        """测试9：文件兼容性"""
        # 验证：修改后文件可以在AutoCAD中正常打开

    def test_10_terminology_consistency(self):
        """测试10：术语一致性"""
        # 同一术语在整个图纸中翻译一致

    def test_11_large_file_performance(self):
        """测试11：大文件性能"""
        # 测试10000+文本实体的翻译

    def test_12_error_recovery(self):
        """测试12：错误恢复"""
        # 测试翻译过程中出错，不应破坏原文件
```

---

## 📊 质量保证清单

### 开发阶段

- [ ] 实现6种文本实体类型的提取
- [ ] 实现文本分类器（6种类型）
- [ ] 实现上下文感知翻译引擎
- [ ] 实现术语库和翻译记忆
- [ ] 实现MTEXT格式保持
- [ ] 实现混合文本智能处理
- [ ] 实现精确DWG修改器
- [ ] 实现完整性验证器
- [ ] 实现质量评分系统
- [ ] 实现人工审查界面

### 测试阶段

- [ ] 单元测试：每个组件独立测试
- [ ] 集成测试：完整工作流测试
- [ ] 边界测试：极端情况（空文本、超长文本、特殊字符）
- [ ] 性能测试：大文件（10000+文本）
- [ ] 兼容性测试：不同CAD软件打开
- [ ] 真实案例测试：实际建筑图纸、机械图纸等

### 部署前检查

- [ ] 所有测试100%通过
- [ ] 代码审查完成
- [ ] 文档完整
- [ ] 性能达标
- [ ] 安全审计
- [ ] 用户培训材料准备

---

## 🎯 成功标准

### 功能性标准

✅ **必须达到**：
1. 翻译后DWG文件可以在AutoCAD中正常打开，无任何警告
2. 所有非文本实体完全不变（byte-level一致）
3. 所有文本实体属性完全不变（除text内容）
4. 同一术语在整个图纸中翻译100%一致
5. 纯数字和特殊符号100%保持不变

### 质量标准

✅ **必须达到**：
1. 翻译准确率 ≥ 95%（人工抽检100个样本）
2. 格式保持率 = 100%（所有MTEXT格式完全保留）
3. 文件完整性 = 100%（所有验证项通过）
4. 零破坏性错误（不能有任何导致文件损坏的情况）

### 性能标准

✅ **必须达到**：
1. 1000个文本实体翻译 < 60秒
2. 10000个文本实体翻译 < 10分钟
3. 验证时间 < 翻译时间的10%
4. 内存占用 < 原文件大小的5倍

---

## 🚧 风险控制

### 高风险操作

🔴 **极高风险**（必须特别小心）：
1. 修改DIMENSION尺寸标注
2. 修改TABLE表格
3. 修改ATTRIB块属性
4. 处理包含公式的文本

⚠️ **高风险**（需要充分测试）：
1. MTEXT格式保持
2. 混合文本拆分
3. 文本长度自适应

### 风险缓解措施

1. **自动备份**：修改前自动创建备份
2. **严格验证**：修改后进行多重验证
3. **人工审查**：高风险项强制人工审查
4. **渐进式修改**：先修改低风险实体，成功后再修改高风险
5. **错误恢复**：任何错误都可以回滚到原文件

---

## 📝 总结

这是一个**极其复杂和关键**的功能，需要：

1. **深刻理解DWG文件结构**
2. **精确的文本分类和处理**
3. **智能的上下文感知翻译**
4. **完全非破坏性的修改方法**
5. **严格的验证和质量控制**
6. **完善的人工审查流程**

只有做到以上所有点，才能保证：
> **生成一个一模一样的图纸，不会有任何区别和错误**

下一步：开始实现这个完整的系统。
