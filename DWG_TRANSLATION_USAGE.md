# DWG翻译功能使用指南

## 🎯 功能概述

**DWG翻译功能**能够将CAD图纸中的文字翻译成另一种语言，并生成一个**与原图纸完全一样**的新图纸（只有文字内容不同，其他所有属性、位置、格式都完全不变）。

### ✅ 核心特性

1. **智能文本识别**
   - 支持6种文本实体类型：TEXT, MTEXT, DIMENSION, LEADER, MULTILEADER, ATTRIB/ATTDEF
   - 自动分类文本类型：纯数字、单位、纯文本、混合文本、公式、特殊符号

2. **精准翻译**
   - 术语一致性保证（同一术语在整个图纸中翻译一致）
   - 翻译记忆功能
   - 上下文感知翻译
   - MTEXT格式完整保持
   - 混合文本智能处理（如"3000mm"保持数字和单位不变）

3. **完全非破坏性修改**
   - ✅ 只修改文本内容
   - ❌ 不改变位置、大小、旋转、颜色等任何属性
   - ❌ 不创建、删除实体
   - ❌ 不改变文件结构
   - ✅ 自动备份原文件

4. **质量保证**
   - 置信度评分
   - 自动标记需要人工审查的文本
   - 详细的处理报告

---

## 🚀 快速开始

### 方法1：使用便捷函数（推荐）

```python
from src.dwg.translation_pipeline import translate_dwg_file

# 翻译单个文件
result = translate_dwg_file(
    input_path="原图纸.dwg",
    output_path="翻译后.dwg",  # 可选，默认自动生成
    api_key="your_api_key",     # 可选，默认从配置读取
    source_lang="Chinese",
    target_lang="English"
)

# 查看结果
print(f"成功: {result.success}")
print(f"翻译了 {result.translated_texts} 个文本")
print(f"耗时: {result.total_time:.2f} 秒")
```

### 方法2：使用流程管道（更灵活）

```python
from src.dwg.translation_pipeline import TranslationPipeline, PipelineConfig

# 创建配置
config = PipelineConfig(
    api_key="your_api_key",
    source_language="Chinese",
    target_language="English",
    use_terminology=True,      # 使用术语库
    use_memory=True,           # 使用翻译记忆
    create_backup=True,        # 创建备份
)

# 创建管道
pipeline = TranslationPipeline(config)

# 处理文件
result = pipeline.process_file("原图纸.dwg", "翻译后.dwg")

# 查看详细结果
print(f"总文本: {result.total_texts}")
print(f"翻译: {result.translated_texts}")
print(f"跳过: {result.skipped_texts}")
print(f"失败: {result.failed_texts}")
print(f"需审查: {result.needs_review_count}")
print(f"平均置信度: {result.average_confidence:.1%}")
```

### 方法3：批量处理

```python
from src.dwg.translation_pipeline import translate_dwg_files

# 批量翻译多个文件
results = translate_dwg_files(
    input_paths=["图纸1.dwg", "图纸2.dwg", "图纸3.dwg"],
    output_paths=None,  # 自动生成输出文件名
    api_key="your_api_key"
)

# 查看每个文件的结果
for result in results:
    print(f"{result.input_path}: {result.success}")
```

---

## 📋 详细使用步骤

### 步骤1：提取文本

```python
from src.dwg.text_extractor import TextExtractor

extractor = TextExtractor()
texts = extractor.extract_from_file("原图纸.dwg")

print(f"提取了 {len(texts)} 个文本实体")

# 查看提取统计
stats = extractor.get_statistics()
print(f"按类型: {stats['by_type']}")
```

**提取的文本信息包括**：
- 实体ID、类型
- 原始文本内容
- 完整的属性（位置、大小、旋转、样式、图层等）
- 实体引用（用于后续修改）

### 步骤2：分类文本

```python
from src.dwg.text_classifier import TextClassifier

classifier = TextClassifier()
texts = classifier.classify_batch(texts)

# 查看分类统计
stats = classifier.get_statistics()
print(stats)
```

**文本分类结果**：
- `PURE_NUMBER`: 纯数字 → 不翻译
- `UNIT`: 单位符号 → 可选转换
- `PURE_TEXT`: 纯文本 → AI翻译
- `MIXED`: 混合文本 → 智能拆分处理
- `SPECIAL_SYMBOL`: 特殊符号 → 保持不变
- `FORMULA`: 公式 → 不翻译

### 步骤3：智能翻译

```python
from src.dwg.smart_translator import SmartTranslator

translator = SmartTranslator(api_key="your_api_key")
texts = translator.translate_texts(
    texts,
    use_terminology=True,  # 使用术语库
    use_memory=True        # 使用翻译记忆
)

# 查看翻译结果
for text in texts:
    if text.translated_text:
        print(f"{text.original_text} → {text.translated_text}")
        print(f"  置信度: {text.confidence:.1%}")
        if text.needs_review:
            print(f"  ⚠️ 需要审查: {text.warning_message}")
```

**翻译策略**：
1. 先检查翻译记忆（确保一致性）
2. 再检查术语库（专业术语）
3. 根据文本分类选择处理方式
4. 对于纯文本，调用AI翻译（提供上下文）
5. 对于MTEXT，保持所有格式标记
6. 对于混合文本，智能拆分并只翻译文字部分

### 步骤4：精确修改

```python
from src.dwg.precision_modifier import PrecisionDWGModifier

modifier = PrecisionDWGModifier()
result = modifier.modify_file(
    input_path="原图纸.dwg",
    translations=texts,
    output_path="翻译后.dwg",
    create_backup=True
)

print(f"成功: {result.success}")
print(f"修改: {result.stats.success_count} 个文本")
print(f"失败: {result.stats.error_count} 个文本")
print(f"备份: {result.backup_path}")
```

**修改原则**：
- ✅ **只修改** `entity.dxf.text` 属性
- ❌ **不修改** 任何其他属性
- ❌ **不创建** 新实体
- ❌ **不删除** 实体
- ❌ **不改变** 实体顺序

---

## 🔧 高级配置

### 配置术语库

```python
from src.dwg.smart_translator import SmartTranslator

translator = SmartTranslator()

# 添加自定义术语
translator.terminology_db.add_term("卧室", "Bedroom")
translator.terminology_db.add_term("客厅", "Living Room")

# 从CSV文件加载术语库
translator.terminology_db.load_from_file("术语库.csv")

# 保存术语库到文件
translator.terminology_db.save_to_file("术语库.csv")
```

**术语库CSV格式**：
```csv
原文,译文
卧室,Bedroom
客厅,Living Room
厨房,Kitchen
```

### 配置翻译选项

```python
from src.dwg.translation_pipeline import PipelineConfig

config = PipelineConfig(
    # API配置
    api_key="your_api_key",

    # 翻译配置
    source_language="Chinese",
    target_language="English",
    use_terminology=True,      # 使用术语库
    use_memory=True,           # 使用翻译记忆（确保一致性）

    # 修改配置
    create_backup=True,        # 创建备份文件
    validate_result=False,     # 验证结果（耗时，不推荐）

    # 输出配置
    output_dir="/path/to/output",  # 输出目录
    output_suffix="_translated"    # 输出文件后缀
)
```

---

## 📊 处理结果分析

### 查看详细统计

```python
result = translate_dwg_file("图纸.dwg")

print("="*60)
print("处理结果")
print("="*60)
print(f"输入: {result.input_path}")
print(f"输出: {result.output_path}")
print(f"备份: {result.backup_path}")
print()
print(f"总文本数: {result.total_texts}")
print(f"  翻译: {result.translated_texts}")
print(f"  跳过: {result.skipped_texts}")
print(f"  失败: {result.failed_texts}")
print(f"  需审查: {result.needs_review_count}")
print()
print(f"平均置信度: {result.average_confidence:.1%}")
print()
print("分类统计:")
for category, count in result.classification_stats.items():
    print(f"  {category}: {count}")
print()
print("耗时统计:")
print(f"  提取: {result.extraction_time:.2f}s")
print(f"  翻译: {result.translation_time:.2f}s")
print(f"  修改: {result.modification_time:.2f}s")
print(f"  总计: {result.total_time:.2f}s")
```

### 查看警告和错误

```python
if result.warnings:
    print("\n警告:")
    for warning in result.warnings:
        print(f"  ⚠️ {warning}")

if result.errors:
    print("\n错误:")
    for error in result.errors:
        print(f"  ❌ {error}")
```

### 查看需要审查的文本

```python
# 获取需要审查的文本
needs_review = [
    text for text in result.extracted_texts
    if text.needs_review
]

print(f"\n需要人工审查的文本 ({len(needs_review)} 个):")
for text in needs_review:
    print(f"  [{text.entity_id}] {text.original_text} → {text.translated_text}")
    print(f"    原因: {text.warning_message}")
    print(f"    置信度: {text.confidence:.1%}")
```

---

## ⚠️ 重要注意事项

### 1. 尺寸标注 (DIMENSION)

**问题**：尺寸标注的数值通常是自动计算的，不应该翻译。

**处理**：
- 只翻译**覆盖文本**（用户手动设置的文本）
- 自动计算的数值保持不变
- 所有尺寸标注会被标记为"需要审查"

### 2. 块属性 (ATTRIB)

**问题**：块属性的修改会影响所有使用该块的实例。

**处理**：
- 修改前会发出警告
- 自动标记为"需要审查"
- 建议在修改前了解块的使用情况

### 3. MTEXT格式

**问题**：MTEXT包含特殊格式标记（如`\\f`, `\\P`, `\\C`等），破坏这些标记会导致显示错误。

**处理**：
- 自动识别并保持所有格式标记
- 只翻译纯文本部分
- 建议翻译后打开CAD软件验证格式

### 4. 混合文本

**示例**：
```
"3000mm" → "3000mm" (保持数字和单位)
"混凝土≥C30" → "Concrete ≥ C30" (保持符号和等级)
"φ200" → "φ200" (保持符号和数字)
```

**处理**：
- 智能拆分文本
- 只翻译文字部分
- 保持数字、符号、单位不变

### 5. 文本长度

**问题**：翻译后文本可能过长，覆盖其他内容。

**处理**：
- 自动检测长度比例
- 如果超过2倍，发出警告
- 标记为"需要审查"
- 建议人工调整或使用缩写

---

## 🧪 测试示例

### 示例1：简单文本翻译

```python
# 输入: "卧室"
# 输出: "Bedroom"
# 分类: PURE_TEXT
# 策略: 术语库匹配
```

### 示例2：数字保持

```python
# 输入: "3000"
# 输出: "3000"
# 分类: PURE_NUMBER
# 策略: 不翻译
```

### 示例3：混合文本

```python
# 输入: "混凝土强度≥C30"
# 输出: "Concrete strength ≥ C30"
# 分类: MIXED
# 策略: 智能拆分 - 翻译"混凝土强度"，保持"≥C30"
```

### 示例4：MTEXT格式保持

```python
# 输入: "{\\fSimSun;第一行\\P第二行}"
# 输出: "{\\fSimSun;First Line\\PSecond Line}"
# 分类: PURE_TEXT (MTEXT)
# 策略: 保持所有\\开头的格式标记
```

---

## 🔍 常见问题

### Q1: 如何确保翻译一致性？

**A**: 系统使用**翻译记忆**功能，同一文本在整个图纸中只翻译一次，后续直接使用记忆中的翻译，确保100%一致。

### Q2: 如何处理专业术语？

**A**: 使用**术语库**功能：
1. 软件内置常见建筑术语
2. 用户可添加自定义术语
3. 术语库优先级高于AI翻译

### Q3: 翻译后图纸真的"一模一样"吗？

**A**: 是的！除了文本内容，其他完全不变：
- 位置精确到10^-10（双精度浮点数）
- 所有属性（大小、旋转、颜色、图层等）完全一致
- 实体数量、顺序完全一致
- 文件结构完全一致

### Q4: 如何验证翻译质量？

**A**: 系统提供多重保障：
1. 置信度评分（0-1）
2. 自动标记需要审查的文本
3. 详细的警告和错误信息
4. 建议：翻译后在CAD软件中打开验证

### Q5: 如果翻译失败怎么办？

**A**: 系统有多重保护：
1. 自动创建备份文件
2. 失败的文本会保持原文
3. 详细的错误日志
4. 可以恢复到备份文件

### Q6: 支持哪些语言？

**A**: 支持Alibaba Cloud Bailian支持的所有语言对，包括：
- 中文 ↔ 英文
- 中文 ↔ 日文
- 中文 ↔ 韩文
- 等等

---

## 📝 完整使用示例

```python
#!/usr/bin/env python3
"""
DWG翻译完整示例
"""
from src.dwg.translation_pipeline import TranslationPipeline, PipelineConfig
from src.utils.logger import logger

def main():
    # 1. 创建配置
    config = PipelineConfig(
        api_key="your_api_key_here",
        source_language="Chinese",
        target_language="English",
        use_terminology=True,
        use_memory=True,
        create_backup=True,
        output_suffix="_EN"
    )

    # 2. 创建管道
    pipeline = TranslationPipeline(config)

    # 3. 添加自定义术语
    pipeline.translator.terminology_db.add_term("卧室", "BR")  # 使用缩写
    pipeline.translator.terminology_db.add_term("客厅", "LR")
    pipeline.translator.terminology_db.add_term("厨房", "KT")

    # 4. 处理文件
    result = pipeline.process_file(
        input_path="建筑平面图.dwg",
        output_path="建筑平面图_EN.dwg"
    )

    # 5. 检查结果
    if result.success:
        print("\n✅ 翻译成功!")
        print(f"输出文件: {result.output_path}")
        print(f"翻译了 {result.translated_texts}/{result.total_texts} 个文本")
        print(f"平均置信度: {result.average_confidence:.1%}")
        print(f"总耗时: {result.total_time:.2f}秒")

        # 6. 处理需要审查的文本
        if result.needs_review_count > 0:
            print(f"\n⚠️ 有 {result.needs_review_count} 个文本需要人工审查")
            needs_review = [t for t in result.extracted_texts if t.needs_review]
            for text in needs_review[:5]:  # 只显示前5个
                print(f"  • {text.original_text} → {text.translated_text}")
                print(f"    原因: {text.warning_message}")

        # 7. 显示警告和错误
        if result.warnings:
            print(f"\n警告 ({len(result.warnings)}):")
            for warning in result.warnings[:3]:  # 只显示前3个
                print(f"  ⚠️ {warning}")

        if result.errors:
            print(f"\n错误 ({len(result.errors)}):")
            for error in result.errors:
                print(f"  ❌ {error}")

    else:
        print("\n❌ 翻译失败!")
        for error in result.errors:
            print(f"  {error}")

if __name__ == "__main__":
    main()
```

---

## 🎓 进阶使用

### 自定义翻译策略

```python
from src.dwg.smart_translator import SmartTranslator

class CustomTranslator(SmartTranslator):
    """自定义翻译器"""

    def _translate_pure_text(self, text, all_texts):
        """覆盖纯文本翻译方法"""

        # 自定义逻辑
        if "重要" in text.original_text:
            # 对重要文本使用更强的模型
            pass

        return super()._translate_pure_text(text, all_texts)
```

### 批量处理多个项目

```python
import os
from pathlib import Path

def process_project(project_dir):
    """处理整个项目目录"""

    # 找到所有DWG文件
    dwg_files = list(Path(project_dir).rglob("*.dwg"))

    print(f"找到 {len(dwg_files)} 个DWG文件")

    # 创建输出目录
    output_dir = Path(project_dir) / "translated"
    output_dir.mkdir(exist_ok=True)

    # 批量处理
    from src.dwg.translation_pipeline import TranslationPipeline, PipelineConfig

    config = PipelineConfig(output_dir=str(output_dir))
    pipeline = TranslationPipeline(config)

    results = pipeline.process_batch([str(f) for f in dwg_files])

    # 生成报告
    print("\n处理完成!")
    print(f"成功: {sum(1 for r in results if r.success)}")
    print(f"失败: {sum(1 for r in results if not r.success)}")

# 使用
process_project("/path/to/project")
```

---

## 📚 相关文档

- **技术设计文档**: `DWG_TRANSLATION_DESIGN.md`
- **系统工作原理**: `HOW_IT_WORKS.md`
- **性能分析**: `PERFORMANCE_ANALYSIS.md`
- **快速使用指南**: `快速使用指南.md`

---

**提示**：翻译后请务必在CAD软件中打开验证，确保一切正常！
