"""
智能翻译引擎
支持上下文感知、术语一致性、MTEXT格式保持
"""
from typing import List, Dict, Optional, Tuple
from dataclasses import dataclass
import re

from .text_extractor import ExtractedText, TextEntityType
from .text_classifier import TextCategory, TextClassifier, MixedTextParser
from ..services.bailian_client import BailianClient
from ..utils.logger import logger
from ..utils.config_manager import ConfigManager


@dataclass
class TranslationResult:
    """翻译结果"""
    original: str                # 原文
    translation: str             # 译文
    confidence: float            # 置信度
    source: str                  # 来源（api/terminology/memory）
    warnings: List[str] = None   # 警告信息
    needs_review: bool = False   # 是否需要审查

    def __post_init__(self):
        if self.warnings is None:
            self.warnings = []


class TerminologyDatabase:
    """
    术语库

    存储专业术语的标准翻译
    确保整个图纸中术语翻译的一致性
    """

    def __init__(self):
        # 内置建筑术语库（英文→中文）
        self.terminology = {
            # 房间类型（Room Types）
            "Bedroom": "卧室",
            "Living Room": "客厅",
            "Kitchen": "厨房",
            "Bathroom": "卫生间",
            "Dining Room": "餐厅",
            "Study": "书房",
            "Balcony": "阳台",
            "Corridor": "走廊",
            "Hallway": "走廊",
            "Storage": "储藏室",
            "Closet": "衣柜间",
            "Laundry": "洗衣房",
            "Garage": "车库",

            # 建筑元素（Building Elements）
            "Wall": "墙",
            "Door": "门",
            "Window": "窗",
            "Column": "柱",
            "Beam": "梁",
            "Slab": "板",
            "Staircase": "楼梯",
            "Stair": "楼梯",
            "Elevator": "电梯",
            "Roof": "屋顶",
            "Floor": "地板",
            "Ceiling": "天花板",

            # 材料（Materials）
            "Concrete": "混凝土",
            "Rebar": "钢筋",
            "Brick": "砖",
            "Glass": "玻璃",
            "Wood": "木材",
            "Stone": "石材",
            "Steel": "钢",
            "Aluminum": "铝",

            # 单位（Units）
            "mm": "毫米",
            "cm": "厘米",
            "m": "米",
            "m²": "平方米",
            "m³": "立方米",
        }

        # 用户自定义术语（优先级更高）
        self.custom_terminology: Dict[str, str] = {}

    def match(self, text: str) -> Optional[Tuple[str, str]]:
        """
        匹配术语

        Args:
            text: 要匹配的文本

        Returns:
            (原术语, 翻译) 或 None
        """
        # 优先匹配用户自定义术语
        if text in self.custom_terminology:
            return (text, self.custom_terminology[text])

        # 匹配内置术语
        if text in self.terminology:
            return (text, self.terminology[text])

        return None

    def add_term(self, source: str, target: str):
        """添加自定义术语"""
        self.custom_terminology[source] = target

    def load_from_file(self, file_path: str):
        """从文件加载术语库（CSV格式）"""
        import csv
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                reader = csv.reader(f)
                next(reader)  # 跳过标题行
                for row in reader:
                    if len(row) >= 2:
                        self.custom_terminology[row[0]] = row[1]
            logger.info(f"从文件加载了 {len(self.custom_terminology)} 条术语")
        except Exception as e:
            logger.error(f"加载术语库失败: {e}")

    def save_to_file(self, file_path: str):
        """保存术语库到文件"""
        import csv
        try:
            with open(file_path, 'w', encoding='utf-8', newline='') as f:
                writer = csv.writer(f)
                writer.writerow(['原文', '译文'])
                for source, target in self.custom_terminology.items():
                    writer.writerow([source, target])
            logger.info(f"保存了 {len(self.custom_terminology)} 条术语")
        except Exception as e:
            logger.error(f"保存术语库失败: {e}")


class MTextFormatter:
    """
    MTEXT格式处理器

    保持MTEXT的所有格式标记（\\f, \\P, \\C等）
    """

    # MTEXT格式标记正则表达式
    FORMAT_PATTERN = re.compile(r'(\\[A-Za-z][^;]*?;|\\[A-Za-z]\d+|\\P|\\X)')

    @classmethod
    def parse(cls, mtext: str) -> List[Tuple[str, str]]:
        """
        解析MTEXT，分离格式标记和纯文本

        Args:
            mtext: MTEXT内容

        Returns:
            [('format', '\\fSimSun;'), ('text', '第一行'), ('format', '\\P'), ...]
        """
        parts = []
        last_end = 0

        for match in cls.FORMAT_PATTERN.finditer(mtext):
            # 添加格式标记之前的文本
            if match.start() > last_end:
                text = mtext[last_end:match.start()]
                if text:
                    parts.append(('text', text))

            # 添加格式标记
            parts.append(('format', match.group(0)))
            last_end = match.end()

        # 添加最后的文本
        if last_end < len(mtext):
            text = mtext[last_end:]
            if text:
                parts.append(('text', text))

        return parts

    @classmethod
    def reconstruct(cls, parts: List[Tuple[str, str]]) -> str:
        """
        重新组装MTEXT

        Args:
            parts: [('format', '...'), ('text', '...'), ...]

        Returns:
            完整的MTEXT字符串
        """
        return ''.join(content for _, content in parts)

    @classmethod
    def translate_mtext(cls, mtext: str, translator_func) -> str:
        """
        翻译MTEXT，保持所有格式

        Args:
            mtext: MTEXT内容
            translator_func: 翻译函数

        Returns:
            翻译后的MTEXT（保持格式）
        """
        parts = cls.parse(mtext)

        # 只翻译text部分
        for i, (part_type, content) in enumerate(parts):
            if part_type == 'text':
                translated = translator_func(content)
                parts[i] = ('text', translated)

        return cls.reconstruct(parts)


class SmartTranslator:
    """
    智能翻译器

    功能：
    1. 上下文感知翻译
    2. 术语一致性保证
    3. 翻译记忆
    4. MTEXT格式保持
    5. 混合文本智能处理
    """

    def __init__(self, api_key: Optional[str] = None):
        self.config = ConfigManager()

        # 初始化百炼客户端
        if api_key:
            self.client = BailianClient(api_key)
        else:
            # 从配置读取
            api_key = self.config.get('api.api_key', '')
            if api_key:
                self.client = BailianClient(api_key)
            else:
                logger.warning("未配置API密钥，翻译功能将不可用")
                self.client = None

        # 术语库
        self.terminology_db = TerminologyDatabase()

        # 翻译记忆（确保一致性）
        self.translation_memory: Dict[str, str] = {}

        # 文本分类器
        self.classifier = TextClassifier()

        # 混合文本解析器
        self.mixed_parser = MixedTextParser()

        # 翻译配置
        self.source_lang = self.config.get('translation.default_source_lang', 'en')
        self.target_lang = self.config.get('translation.default_target_lang', 'zh-CN')

        logger.info(
            f"智能翻译器初始化完成: {self.source_lang} → {self.target_lang}"
        )

    def translate_texts(
        self,
        texts: List[ExtractedText],
        use_terminology: bool = True,
        use_memory: bool = True
    ) -> List[ExtractedText]:
        """
        批量翻译文本

        Args:
            texts: 文本实体列表
            use_terminology: 是否使用术语库
            use_memory: 是否使用翻译记忆

        Returns:
            更新了translated_text的文本实体列表
        """
        logger.info(f"开始翻译 {len(texts)} 个文本...")

        # 1. 分类所有文本
        texts = self.classifier.classify_batch(texts)

        # 2. 逐个翻译
        for text in texts:
            result = self.translate_single(
                text,
                use_terminology=use_terminology,
                use_memory=use_memory,
                all_texts=texts  # 提供完整上下文
            )

            # 更新翻译结果
            text.translated_text = result.translation
            text.confidence = result.confidence
            text.needs_review = result.needs_review
            text.warning_message = '; '.join(result.warnings) if result.warnings else ""

        # 3. 统计
        translated_count = sum(1 for t in texts if t.translated_text)
        logger.info(f"翻译完成: {translated_count}/{len(texts)}")

        return texts

    def translate_single(
        self,
        text: ExtractedText,
        use_terminology: bool = True,
        use_memory: bool = True,
        all_texts: Optional[List[ExtractedText]] = None
    ) -> TranslationResult:
        """
        翻译单个文本

        策略优先级：
        1. 翻译记忆（最高优先级，确保一致性）
        2. 术语库匹配
        3. 文本分类处理
        4. AI翻译（提供上下文）
        """
        original = text.original_text.strip()

        # 策略0：空文本
        if not original:
            return TranslationResult(
                original="",
                translation="",
                confidence=1.0,
                source="empty"
            )

        # 策略1：翻译记忆（确保一致性）
        if use_memory and original in self.translation_memory:
            return TranslationResult(
                original=original,
                translation=self.translation_memory[original],
                confidence=1.0,
                source="memory"
            )

        # 策略2：术语库匹配
        if use_terminology:
            term_match = self.terminology_db.match(original)
            if term_match:
                translation = term_match[1]
                self.translation_memory[original] = translation
                return TranslationResult(
                    original=original,
                    translation=translation,
                    confidence=1.0,
                    source="terminology"
                )

        # 策略3：根据文本分类处理
        category = text.text_category

        if category == TextCategory.PURE_NUMBER:
            # 纯数字不翻译
            return TranslationResult(
                original=original,
                translation=original,
                confidence=1.0,
                source="no_translation_needed"
            )

        elif category == TextCategory.UNIT:
            # 单位符号可选转换
            return self._translate_unit(original)

        elif category == TextCategory.FORMULA:
            # 公式不翻译
            return TranslationResult(
                original=original,
                translation=original,
                confidence=1.0,
                source="no_translation_needed",
                warnings=["公式不应翻译"]
            )

        elif category == TextCategory.SPECIAL_SYMBOL:
            # 特殊符号保持
            return TranslationResult(
                original=original,
                translation=original,
                confidence=1.0,
                source="no_translation_needed"
            )

        elif category == TextCategory.MIXED:
            # 混合文本智能处理
            return self._translate_mixed(text, all_texts)

        elif category == TextCategory.PURE_TEXT:
            # 纯文本AI翻译
            if text.entity_type == TextEntityType.MTEXT:
                # MTEXT特殊处理（保持格式）
                return self._translate_mtext(text, all_texts)
            else:
                # 普通文本翻译
                return self._translate_pure_text(text, all_texts)

        else:
            # 未知类别，默认翻译
            return self._translate_pure_text(text, all_texts)

    def _translate_unit(self, unit: str) -> TranslationResult:
        """翻译单位（可选）"""
        # 默认保持不变
        # 用户可以选择是否转换单位
        return TranslationResult(
            original=unit,
            translation=unit,
            confidence=1.0,
            source="unit_keep"
        )

    def _translate_mixed(
        self,
        text: ExtractedText,
        all_texts: Optional[List[ExtractedText]]
    ) -> TranslationResult:
        """
        翻译混合文本

        例如："3000mm" → "3000mm" (数字和单位保持)
        例如："混凝土≥C30" → "Concrete ≥ C30"
        """
        original = text.original_text

        # 解析混合文本
        parts = self.mixed_parser.parse(original)

        # 只翻译text部分
        translated_parts = []
        for part_type, content in parts:
            if part_type == 'text':
                # 翻译文字部分
                trans_result = self._call_api_translate(
                    content,
                    text,
                    all_texts,
                    is_fragment=True
                )
                translated_parts.append((part_type, trans_result.translation))
            else:
                # 保持数字、符号、单位
                translated_parts.append((part_type, content))

        # 重新组装
        translation = self.mixed_parser.reconstruct(translated_parts)

        # 记录到翻译记忆
        self.translation_memory[original] = translation

        return TranslationResult(
            original=original,
            translation=translation,
            confidence=0.9,  # 混合文本置信度稍低
            source="api_mixed"
        )

    def _translate_mtext(
        self,
        text: ExtractedText,
        all_texts: Optional[List[ExtractedText]]
    ) -> TranslationResult:
        """
        翻译MTEXT（保持所有格式标记）
        """
        original = text.original_text

        def translate_fragment(fragment: str) -> str:
            """翻译片段"""
            result = self._call_api_translate(
                fragment,
                text,
                all_texts,
                is_fragment=True
            )
            return result.translation

        # 使用MTEXT格式器翻译
        translation = MTextFormatter.translate_mtext(original, translate_fragment)

        # 记录到翻译记忆
        self.translation_memory[original] = translation

        return TranslationResult(
            original=original,
            translation=translation,
            confidence=0.85,  # MTEXT置信度稍低（格式复杂）
            source="api_mtext",
            warnings=["MTEXT包含格式标记，请验证格式是否正确"]
        )

    def _translate_pure_text(
        self,
        text: ExtractedText,
        all_texts: Optional[List[ExtractedText]]
    ) -> TranslationResult:
        """翻译纯文本（调用AI API）"""
        result = self._call_api_translate(text.original_text, text, all_texts)

        # 记录到翻译记忆
        self.translation_memory[text.original_text] = result.translation

        return result

    def _call_api_translate(
        self,
        text: str,
        text_entity: ExtractedText,
        all_texts: Optional[List[ExtractedText]],
        is_fragment: bool = False
    ) -> TranslationResult:
        """
        调用API进行翻译

        Args:
            text: 要翻译的文本
            text_entity: 文本实体（提供上下文）
            all_texts: 所有文本（提供更广泛的上下文）
            is_fragment: 是否为片段（影响prompt）
        """
        if not self.client:
            # 没有配置API，返回原文
            return TranslationResult(
                original=text,
                translation=text,
                confidence=0.0,
                source="no_api",
                warnings=["未配置API密钥，无法翻译"],
                needs_review=True
            )

        try:
            # 构建上下文信息
            context = self._build_context(text_entity, all_texts)

            # 构建prompt
            prompt = self._build_translation_prompt(text, context, is_fragment)

            # 调用API
            translation = self.client.translate_batch(
                [prompt],
                self.source_lang,
                self.target_lang,
                task_type='text'
            )[0]

            # 清理翻译结果
            translation = translation.strip()

            # 长度检查
            warnings = []
            if len(translation) > len(text) * 2.5:
                warnings.append("翻译后文本过长，可能影响布局")

            return TranslationResult(
                original=text,
                translation=translation,
                confidence=0.95,
                source="api",
                warnings=warnings
            )

        except Exception as e:
            logger.error(f"API翻译失败: {e}")
            return TranslationResult(
                original=text,
                translation=text,
                confidence=0.0,
                source="api_error",
                warnings=[f"翻译失败: {str(e)}"],
                needs_review=True
            )

    def _build_context(
        self,
        text: ExtractedText,
        all_texts: Optional[List[ExtractedText]]
    ) -> Dict:
        """构建上下文信息"""
        context = {
            'entity_type': text.entity_type.value,
            'layer': text.layer,
            'nearby_texts': text.nearby_texts,
            'text_category': text.text_category.value
        }

        # TODO: 添加更多上下文信息
        # - 周围的图形实体类型
        # - 相邻文本的翻译结果
        # - 图纸类型推断

        return context

    def _build_translation_prompt(
        self,
        text: str,
        context: Dict,
        is_fragment: bool
    ) -> str:
        """构建翻译prompt"""
        if is_fragment:
            # 片段翻译，简洁prompt
            return f"翻译以下CAD图纸文本片段，保持简洁：{text}"
        else:
            # 完整文本翻译，提供上下文
            prompt = f"""
你是专业的CAD图纸翻译专家。请翻译以下文本：

原文：{text}

上下文信息：
- 实体类型：{context.get('entity_type', 'TEXT')}
- 所在图层：{context.get('layer', '0')}
- 文本分类：{context.get('text_category', 'pure_text')}

翻译要求：
1. 使用专业建筑/工程术语
2. 保持简洁（建议不超过原文长度的2倍）
3. 保留所有数字、符号和单位
4. 如果是标准缩写，使用标准英文缩写

只返回翻译结果，不要解释。
"""
            return prompt.strip()


# 便捷函数
def translate_dwg_texts(
    texts: List[ExtractedText],
    api_key: Optional[str] = None
) -> List[ExtractedText]:
    """
    翻译DWG文本的便捷函数

    Args:
        texts: 文本实体列表
        api_key: API密钥（可选）

    Returns:
        更新了translated_text的文本实体列表
    """
    translator = SmartTranslator(api_key)
    return translator.translate_texts(texts)
