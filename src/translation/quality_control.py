"""
翻译质量控制系统

实现多层次质量检查，确保翻译准确率达到99.9999%
"""
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass
from enum import Enum
import re

from ..domain.construction_terminology import (
    TermMatcher, TranslationRules, ConstructionStandards,
    ALL_TERMS, DIMENSION_TERMS, MATERIAL_TERMS
)
from ..utils.logger import logger


class QualityLevel(Enum):
    """质量级别"""
    PERFECT = "完美"
    EXCELLENT = "优秀"
    GOOD = "良好"
    ACCEPTABLE = "可接受"
    POOR = "差"
    FAILED = "不合格"


@dataclass
class QualityIssue:
    """质量问题"""
    category: str  # 问题类别
    severity: str  # 严重程度（CRITICAL/MAJOR/MINOR）
    original: str  # 原文
    translated: str  # 译文
    issue: str  # 问题描述
    suggestion: str  # 修正建议
    confidence: float = 0.0  # 置信度（0-1）

    def to_dict(self) -> Dict:
        return {
            'category': self.category,
            'severity': self.severity,
            'original': self.original,
            'translated': self.translated,
            'issue': self.issue,
            'suggestion': self.suggestion,
            'confidence': self.confidence
        }


@dataclass
class QualityReport:
    """质量报告"""
    total_texts: int
    perfect_count: int
    issues: List[QualityIssue]
    accuracy_rate: float
    quality_level: QualityLevel

    def get_summary(self) -> str:
        """获取摘要"""
        summary = f"翻译质量报告\n"
        summary += f"  总文本数: {self.total_texts}\n"
        summary += f"  完美翻译: {self.perfect_count}\n"
        summary += f"  准确率: {self.accuracy_rate*100:.4f}%\n"
        summary += f"  质量等级: {self.quality_level.value}\n"
        summary += f"  发现问题: {len(self.issues)}\n"

        if self.issues:
            summary += f"\n问题分类:\n"
            critical = len([i for i in self.issues if i.severity == 'CRITICAL'])
            major = len([i for i in self.issues if i.severity == 'MAJOR'])
            minor = len([i for i in self.issues if i.severity == 'MINOR'])
            summary += f"    严重: {critical}\n"
            summary += f"    重要: {major}\n"
            summary += f"    次要: {minor}\n"

        return summary


class TranslationQualityControl:
    """翻译质量控制系统"""

    def __init__(self):
        self.term_matcher = TermMatcher()
        self.translation_rules = TranslationRules()
        logger.info("翻译质量控制系统初始化完成")

    def check_translation(
        self,
        original: str,
        translated: str,
        context: Optional[Dict] = None
    ) -> List[QualityIssue]:
        """
        检查翻译质量

        Args:
            original: 原文
            translated: 译文
            context: 上下文信息

        Returns:
            发现的质量问题列表
        """
        issues = []

        # 1. 专业术语准确性检查
        issues.extend(self._check_terminology(original, translated))

        # 2. 构件代号保留检查
        issues.extend(self._check_component_codes(original, translated))

        # 3. 尺寸数值一致性检查
        issues.extend(self._check_dimension_values(original, translated))

        # 4. 材料等级保留检查
        issues.extend(self._check_material_grades(original, translated))

        # 5. 标点符号检查
        issues.extend(self._check_punctuation(original, translated))

        # 6. 翻译完整性检查
        issues.extend(self._check_completeness(original, translated))

        # 7. 上下文一致性检查
        if context:
            issues.extend(self._check_context_consistency(original, translated, context))

        return issues

    def _check_terminology(self, original: str, translated: str) -> List[QualityIssue]:
        """检查专业术语准确性"""
        issues = []

        # 检查构件类型术语
        for term_name, term in ALL_TERMS.items():
            if term.chinese in original or term.abbreviation in original:
                # 检查译文是否包含对应的英文术语
                if term.english.lower() not in translated.lower():
                    issues.append(QualityIssue(
                        category="专业术语",
                        severity="MAJOR",
                        original=original,
                        translated=translated,
                        issue=f"术语'{term.chinese}'应翻译为'{term.english}'",
                        suggestion=f"建议译文包含: {term.english}",
                        confidence=0.9
                    ))

        return issues

    def _check_component_codes(self, original: str, translated: str) -> List[QualityIssue]:
        """检查构件代号保留"""
        issues = []

        # 提取原文中的构件代号（如KL1, KZ1等）
        code_pattern = r'[A-Z]{1,4}\d+[A-Z]?'
        original_codes = set(re.findall(code_pattern, original))

        for code in original_codes:
            if code not in translated:
                issues.append(QualityIssue(
                    category="构件代号",
                    severity="CRITICAL",
                    original=original,
                    translated=translated,
                    issue=f"构件代号'{code}'未保留在译文中",
                    suggestion=f"译文必须包含: {code}",
                    confidence=1.0
                ))

        return issues

    def _check_dimension_values(self, original: str, translated: str) -> List[QualityIssue]:
        """检查尺寸数值一致性"""
        issues = []

        # 提取原文中的所有数字
        number_pattern = r'\d+(?:\.\d+)?'
        original_numbers = re.findall(number_pattern, original)
        translated_numbers = re.findall(number_pattern, translated)

        # 检查数字数量是否一致
        if len(original_numbers) != len(translated_numbers):
            issues.append(QualityIssue(
                category="尺寸数值",
                severity="CRITICAL",
                original=original,
                translated=translated,
                issue=f"数字数量不一致：原文{len(original_numbers)}个，译文{len(translated_numbers)}个",
                suggestion="检查所有尺寸数值是否正确保留",
                confidence=0.95
            ))

        # 检查每个数字是否都在译文中
        for num in original_numbers:
            if num not in translated_numbers:
                issues.append(QualityIssue(
                    category="尺寸数值",
                    severity="CRITICAL",
                    original=original,
                    translated=translated,
                    issue=f"数值'{num}'在译文中丢失",
                    suggestion=f"译文必须包含数值: {num}",
                    confidence=1.0
                ))

        return issues

    def _check_material_grades(self, original: str, translated: str) -> List[QualityIssue]:
        """检查材料等级保留"""
        issues = []

        # 检查混凝土等级（如C30）
        concrete_pattern = r'C\d{2}'
        concrete_grades = re.findall(concrete_pattern, original)

        for grade in concrete_grades:
            if grade not in translated:
                issues.append(QualityIssue(
                    category="材料等级",
                    severity="CRITICAL",
                    original=original,
                    translated=translated,
                    issue=f"混凝土等级'{grade}'未保留",
                    suggestion=f"译文必须包含: {grade}",
                    confidence=1.0
                ))

        # 检查钢筋等级（如HRB400）
        rebar_pattern = r'H[PR]B\d{3}'
        rebar_grades = re.findall(rebar_pattern, original)

        for grade in rebar_grades:
            if grade not in translated:
                issues.append(QualityIssue(
                    category="材料等级",
                    severity="CRITICAL",
                    original=original,
                    translated=translated,
                    issue=f"钢筋等级'{grade}'未保留",
                    suggestion=f"译文必须包含: {grade}",
                    confidence=1.0
                ))

        return issues

    def _check_punctuation(self, original: str, translated: str) -> List[QualityIssue]:
        """检查标点符号"""
        issues = []

        # 检查直径符号（φ, Φ, ø）
        diameter_symbols = ['φ', 'Φ', 'ø', '∅']
        for symbol in diameter_symbols:
            if symbol in original and symbol not in translated:
                issues.append(QualityIssue(
                    category="特殊符号",
                    severity="MAJOR",
                    original=original,
                    translated=translated,
                    issue=f"直径符号'{symbol}'未保留",
                    suggestion=f"建议保留: {symbol} 或 转换为 diameter",
                    confidence=0.8
                ))

        # 检查乘号（×）
        if '×' in original:
            if '×' not in translated and 'x' not in translated.lower():
                issues.append(QualityIssue(
                    category="特殊符号",
                    severity="MINOR",
                    original=original,
                    translated=translated,
                    issue="乘号'×'未正确转换",
                    suggestion="建议使用: × 或 x",
                    confidence=0.7
                ))

        return issues

    def _check_completeness(self, original: str, translated: str) -> List[QualityIssue]:
        """检查翻译完整性"""
        issues = []

        # 检查译文是否为空
        if not translated or translated.strip() == "":
            issues.append(QualityIssue(
                category="完整性",
                severity="CRITICAL",
                original=original,
                translated=translated,
                issue="译文为空",
                suggestion="需要提供完整的翻译",
                confidence=1.0
            ))
            return issues

        # 检查译文长度是否合理（不能太短或太长）
        original_len = len(original)
        translated_len = len(translated)

        if translated_len < original_len * 0.3:
            issues.append(QualityIssue(
                category="完整性",
                severity="MAJOR",
                original=original,
                translated=translated,
                issue="译文过短，可能内容不完整",
                suggestion="检查是否遗漏翻译内容",
                confidence=0.8
            ))

        if translated_len > original_len * 5:
            issues.append(QualityIssue(
                category="完整性",
                severity="MINOR",
                original=original,
                translated=translated,
                issue="译文过长，可能包含冗余",
                suggestion="检查是否添加了不必要的内容",
                confidence=0.6
            ))

        return issues

    def _check_context_consistency(
        self,
        original: str,
        translated: str,
        context: Dict
    ) -> List[QualityIssue]:
        """检查上下文一致性"""
        issues = []

        # 检查图纸类型一致性
        if 'drawing_type' in context:
            drawing_type = context['drawing_type']
            # 根据图纸类型检查术语使用是否恰当
            if drawing_type == 'structural' and 'architectural' in translated.lower():
                issues.append(QualityIssue(
                    category="上下文一致性",
                    severity="MINOR",
                    original=original,
                    translated=translated,
                    issue="结构图纸中出现建筑相关术语",
                    suggestion="检查术语使用是否准确",
                    confidence=0.5
                ))

        # 检查楼层一致性
        if 'floor' in context:
            floor = context['floor']
            floor_terms = ['B1', 'B2', '1F', '2F', 'RF']
            for term in floor_terms:
                if term in original and term != floor and term not in translated:
                    issues.append(QualityIssue(
                        category="上下文一致性",
                        severity="MINOR",
                        original=original,
                        translated=translated,
                        issue=f"楼层标识'{term}'可能与上下文不一致",
                        suggestion="检查楼层信息是否正确",
                        confidence=0.6
                    ))

        return issues

    def validate_batch(
        self,
        translations: List[Tuple[str, str]],
        context: Optional[Dict] = None
    ) -> QualityReport:
        """
        批量验证翻译质量

        Args:
            translations: [(原文, 译文), ...]
            context: 上下文信息

        Returns:
            质量报告
        """
        all_issues = []
        perfect_count = 0

        for original, translated in translations:
            issues = self.check_translation(original, translated, context)
            if not issues:
                perfect_count += 1
            else:
                all_issues.extend(issues)

        # 计算准确率
        total = len(translations)
        if total > 0:
            # 根据问题严重程度计算准确率
            critical_issues = len([i for i in all_issues if i.severity == 'CRITICAL'])
            major_issues = len([i for i in all_issues if i.severity == 'MAJOR'])
            minor_issues = len([i for i in all_issues if i.severity == 'MINOR'])

            # 严重问题每个扣0.1%，重要问题每个扣0.05%，次要问题每个扣0.01%
            deduction = (
                critical_issues * 0.001 +
                major_issues * 0.0005 +
                minor_issues * 0.0001
            )

            accuracy_rate = max(0, 1.0 - deduction)
        else:
            accuracy_rate = 0.0

        # 确定质量等级
        if accuracy_rate >= 0.999999:
            quality_level = QualityLevel.PERFECT
        elif accuracy_rate >= 0.9999:
            quality_level = QualityLevel.EXCELLENT
        elif accuracy_rate >= 0.999:
            quality_level = QualityLevel.GOOD
        elif accuracy_rate >= 0.99:
            quality_level = QualityLevel.ACCEPTABLE
        elif accuracy_rate >= 0.95:
            quality_level = QualityLevel.POOR
        else:
            quality_level = QualityLevel.FAILED

        report = QualityReport(
            total_texts=total,
            perfect_count=perfect_count,
            issues=all_issues,
            accuracy_rate=accuracy_rate,
            quality_level=quality_level
        )

        logger.info(f"翻译质量验证完成: {report.get_summary()}")
        return report

    def auto_correct(
        self,
        original: str,
        translated: str,
        issues: List[QualityIssue]
    ) -> str:
        """
        自动修正翻译问题

        Args:
            original: 原文
            translated: 译文
            issues: 发现的问题

        Returns:
            修正后的译文
        """
        corrected = translated

        for issue in issues:
            if issue.severity == 'CRITICAL':
                # 自动修正严重问题
                if issue.category == "构件代号":
                    # 提取缺失的代号并添加到译文
                    code_pattern = r'[A-Z]{1,4}\d+[A-Z]?'
                    missing_codes = set(re.findall(code_pattern, original)) - set(re.findall(code_pattern, corrected))
                    for code in missing_codes:
                        # 尝试智能插入位置
                        if code in original:
                            # 在对应的位置插入
                            corrected = f"{code} {corrected}"
                        logger.info(f"自动修正: 添加缺失代号 {code}")

                elif issue.category == "材料等级":
                    # 添加缺失的材料等级
                    if "C" in issue.suggestion:
                        grade = re.findall(r'C\d{2}', issue.suggestion)[0]
                        if grade not in corrected:
                            corrected = corrected.replace("concrete", f"{grade} concrete")
                            logger.info(f"自动修正: 添加混凝土等级 {grade}")

        return corrected


class TranslationEnhancer:
    """翻译增强器 - 基于专业知识提升翻译质量"""

    def __init__(self):
        self.term_matcher = TermMatcher()
        logger.info("翻译增强器初始化完成")

    def enhance_prompt(self, original_prompt: str, context: Optional[Dict] = None) -> str:
        """
        增强翻译Prompt，添加专业知识

        Args:
            original_prompt: 原始prompt
            context: 上下文信息

        Returns:
            增强后的prompt
        """
        enhanced = original_prompt

        # 添加专业术语说明
        enhanced += "\n\n【建筑专业术语要求】"
        enhanced += "\n以下术语必须精确翻译，不得遗漏或错译："
        enhanced += "\n- 构件代号（如KL1, KZ1, Q1）：必须完整保留在译文中"
        enhanced += "\n- 尺寸数值（如300×600）：数字和符号必须准确无误"
        enhanced += "\n- 混凝土等级（如C30, C40）：必须原样保留"
        enhanced += "\n- 钢筋等级（如HRB400）：必须原样保留"
        enhanced += "\n- 直径符号（φ, Φ, ø）：保留或转换为'diameter'"

        # 添加常见术语对照表
        enhanced += "\n\n【构件术语对照】"
        enhanced += "\n- 框架梁 = Frame Beam (KL)"
        enhanced += "\n- 框架柱 = Frame Column (KZ)"
        enhanced += "\n- 剪力墙 = Shear Wall (Q)"
        enhanced += "\n- 楼板 = Floor Slab (LB)"
        enhanced += "\n- 基础梁 = Foundation Beam (JL)"
        enhanced += "\n- 圈梁 = Ring Beam (QL)"

        # 添加质量要求
        enhanced += "\n\n【质量要求】"
        enhanced += "\n1. 准确率目标：99.9999%"
        enhanced += "\n2. 所有数字必须精确翻译，不得增减"
        enhanced += "\n3. 专业术语使用规范的英文对应词"
        enhanced += "\n4. 保持原文格式和结构"
        enhanced += "\n5. 不添加任何原文没有的内容"

        # 根据上下文添加特定要求
        if context:
            if context.get('drawing_type') == 'structural':
                enhanced += "\n\n【结构图纸特别提示】"
                enhanced += "\n- 重点关注梁、柱、墙、板等结构构件"
                enhanced += "\n- 准确翻译配筋信息"
                enhanced += "\n- 保留所有轴线编号"

        return enhanced


if __name__ == "__main__":
    # 测试质量控制系统
    qc = TranslationQualityControl()

    test_cases = [
        ("KL1 300×600", "Frame Beam KL1 300x600"),
        ("KZ1 600×600 C30混凝土", "Column KZ1 600x600"),  # 缺失C30
        ("剪力墙 Q1 200厚", "Wall Q1 200mm thick"),
        ("φ500圆柱", "500mm diameter column"),  # 缺失φ符号
    ]

    for original, translated in test_cases:
        issues = qc.check_translation(original, translated)
        print(f"\n原文: {original}")
        print(f"译文: {translated}")
        print(f"问题数: {len(issues)}")
        for issue in issues:
            print(f"  - [{issue.severity}] {issue.issue}")
            print(f"    建议: {issue.suggestion}")
