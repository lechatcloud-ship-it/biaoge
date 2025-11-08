# -*- coding: utf-8 -*-
"""
文本分类器
判断文本类型，决定翻译策略
"""
import re
from typing import Tuple, List
from .text_extractor import TextCategory, ExtractedText
from ..utils.logger import logger


class TextClassifier:
    """
    文本分类器

    将文本分为6类，决定如何处理：
    1. PURE_NUMBER - 纯数字，不翻译
    2. UNIT - 单位符号，可选转换
    3. PURE_TEXT - 纯文本，AI翻译
    4. MIXED - 混合文本，智能拆分
    5. SPECIAL_SYMBOL - 特殊符号，保持
    6. FORMULA - 公式，不翻译
    """

    # 常见单位
    COMMON_UNITS = {
        # 长度
        'mm', 'cm', 'm', 'km', 'in', 'ft', 'yd',
        # 面积
        'm²', 'm2', 'cm²', 'cm2', 'km²', 'km2', 'ft²', 'ft2',
        # 体积
        'm³', 'm3', 'cm³', 'cm3', 'L', 'ml',
        # 重量
        'kg', 'g', 'mg', 't', 'lb', 'oz',
        # 角度
        '°', 'deg', 'rad',
        # 温度
        '℃', '℉', 'C', 'F',
        # 其他
        '%', '‰', 'kPa', 'MPa', 'N', 'kN',
    }

    # 特殊符号
    SPECIAL_SYMBOLS = {
        'φ', '∅',      # 直径
        '±',           # 正负
        '≥', '≤',      # 大于等于、小于等于
        '>', '<', '=', '≠',  # 比较
        '×', '÷',      # 乘除
        '∑', '∏',      # 求和、求积
        '√',           # 根号
        '∞',           # 无穷
        '∠',           # 角度
        '⊥', '∥',      # 垂直、平行
        '→', '←', '↑', '↓',  # 箭头
    }

    # 数学函数
    MATH_FUNCTIONS = {
        'sin', 'cos', 'tan', 'log', 'ln', 'exp',
        'sqrt', 'pow', 'abs', 'min', 'max'
    }

    def __init__(self):
        self.stats = {
            'pure_number': 0,
            'unit': 0,
            'pure_text': 0,
            'mixed': 0,
            'special_symbol': 0,
            'formula': 0,
            'empty': 0
        }

    def classify(self, text: str) -> TextCategory:
        """
        分类文本

        Args:
            text: 要分类的文本

        Returns:
            文本类别
        """
        # 去除首尾空白
        text = text.strip()

        # 1. 空文本
        if not text:
            self.stats['empty'] += 1
            return TextCategory.EMPTY

        # 2. 纯数字检测（包括小数、负数、科学计数法）
        if self._is_pure_number(text):
            self.stats['pure_number'] += 1
            return TextCategory.PURE_NUMBER

        # 3. 单位符号检测
        if self._is_unit(text):
            self.stats['unit'] += 1
            return TextCategory.UNIT

        # 4. 公式检测（需要在special_symbol之前，因为公式包含符号）
        if self._is_formula(text):
            self.stats['formula'] += 1
            return TextCategory.FORMULA

        # 5. 特殊符号检测（只有符号，没有其他字母）
        if self._is_special_symbol(text):
            self.stats['special_symbol'] += 1
            return TextCategory.SPECIAL_SYMBOL

        # 6. 混合文本检测（数字+文字+符号）
        if self._is_mixed(text):
            self.stats['mixed'] += 1
            return TextCategory.MIXED

        # 7. 纯文本（默认）
        self.stats['pure_text'] += 1
        return TextCategory.PURE_TEXT

    def classify_batch(self, texts: List[ExtractedText]) -> List[ExtractedText]:
        """
        批量分类文本

        Args:
            texts: 文本实体列表

        Returns:
            更新了text_category的文本实体列表
        """
        for text in texts:
            text.text_category = self.classify(text.original_text)

        logger.info(f"文本分类统计: {self.stats}")
        return texts

    def _is_pure_number(self, text: str) -> bool:
        """
        检测是否为纯数字

        支持：
        - 整数：123, -456
        - 小数：3.14, -0.5
        - 科学计数法：1.23e10, -4.5E-3
        - 分数：1/2（可选）
        """
        # 去除空格
        text = text.replace(' ', '').replace(',', '')

        # 正则表达式匹配数字
        number_pattern = r'^[+-]?(\d+\.?\d*|\.\d+)([eE][+-]?\d+)?$'

        if re.match(number_pattern, text):
            return True

        # 检查分数格式（如 1/2, 3/4）
        fraction_pattern = r'^[+-]?\d+/\d+$'
        if re.match(fraction_pattern, text):
            return True

        return False

    def _is_unit(self, text: str) -> bool:
        """
        检测是否为单位符号

        例如：mm, m², kg/m³, m/s
        """
        # 去除空格
        text = text.strip()

        # 直接匹配常见单位
        if text in self.COMMON_UNITS:
            return True

        # 复合单位检测（如 kg/m³, m/s, kN·m）
        composite_pattern = r'^[a-zA-Z]+[²³]?(/[a-zA-Z]+[²³]?|·[a-zA-Z]+[²³]?)*$'
        if re.match(composite_pattern, text):
            return True

        return False

    def _is_special_symbol(self, text: str) -> bool:
        """
        检测是否只包含特殊符号

        例如：φ, ≥, ±
        """
        # 去除空格
        text = text.replace(' ', '')

        # 检查是否只包含特殊符号
        if len(text) <= 3:  # 通常符号很短
            if all(c in self.SPECIAL_SYMBOLS for c in text):
                return True

        return False

    def _is_formula(self, text: str) -> bool:
        """
        检测是否为数学公式

        例如：
        - A=πr²
        - 1:100
        - f(x)=2x+1
        - V=IR
        """
        # 去除空格
        text = text.replace(' ', '')

        # 特征1：包含等号或冒号
        if '=' in text or ':' in text:
            # 检查是否包含字母（变量名）
            if re.search(r'[a-zA-Z]', text):
                return True

        # 特征2：包含数学运算符和字母
        if re.search(r'[a-zA-Z]', text) and re.search(r'[+\-×÷*/^]', text):
            return True

        # 特征3：包含数学函数
        for func in self.MATH_FUNCTIONS:
            if func in text.lower():
                return True

        # 特征4：比例（如 1:100, 1:50）
        ratio_pattern = r'^\d+:\d+$'
        if re.match(ratio_pattern, text):
            return True

        return False

    def _is_mixed(self, text: str) -> bool:
        """
        检测是否为混合文本

        混合文本包含：
        - 数字+单位：3000mm, φ200
        - 数字+文字：C30混凝土
        - 文字+符号：强度≥C30
        """
        # 特征1：包含数字和字母
        has_digit = bool(re.search(r'\d', text))
        has_letter = bool(re.search(r'[a-zA-Z\u4e00-\u9fa5]', text))

        if has_digit and has_letter:
            return True

        # 特征2：包含数字和特殊符号
        has_special = any(s in text for s in self.SPECIAL_SYMBOLS)
        if has_digit and has_special:
            return True

        # 特征3：包含字母和特殊符号
        if has_letter and has_special:
            return True

        return False

    def get_statistics(self) -> dict:
        """获取分类统计"""
        total = sum(self.stats.values())
        if total == 0:
            return self.stats

        percentages = {
            k: f"{v} ({v*100/total:.1f}%)"
            for k, v in self.stats.items()
        }
        return percentages


class MixedTextParser:
    """
    混合文本解析器

    将混合文本拆分为可翻译和不可翻译的部分
    """

    def parse(self, text: str) -> List[Tuple[str, str]]:
        """
        解析混合文本

        Args:
            text: 混合文本

        Returns:
            [(类型, 内容), ...]
            类型: 'number', 'unit', 'symbol', 'text'
        """
        parts = []

        # 简单实现：按字符类型分组
        current_type = None
        current_text = ""

        for char in text:
            char_type = self._get_char_type(char)

            if char_type == current_type:
                current_text += char
            else:
                if current_text:
                    parts.append((current_type, current_text))
                current_type = char_type
                current_text = char

        if current_text:
            parts.append((current_type, current_text))

        return parts

    def _get_char_type(self, char: str) -> str:
        """获取字符类型"""
        if char.isdigit() or char in '.-+':
            return 'number'
        elif char in TextClassifier.SPECIAL_SYMBOLS:
            return 'symbol'
        elif char in TextClassifier.COMMON_UNITS:
            return 'unit'
        elif char.isspace():
            return 'space'
        else:
            return 'text'

    def reconstruct(self, parts: List[Tuple[str, str]]) -> str:
        """
        重新组装文本

        Args:
            parts: [(类型, 内容), ...]

        Returns:
            重组后的文本
        """
        return ''.join(content for _, content in parts)


# 便捷函数
def classify_text(text: str) -> TextCategory:
    """分类单个文本的便捷函数"""
    classifier = TextClassifier()
    return classifier.classify(text)


def classify_texts(texts: List[ExtractedText]) -> List[ExtractedText]:
    """批量分类文本的便捷函数"""
    classifier = TextClassifier()
    return classifier.classify_batch(texts)
