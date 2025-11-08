# -*- coding: utf-8 -*-
"""
工程量计算结果验证器

基于建筑规范和工程经验验证识别结果的合理性
"""
from typing import List, Dict, Tuple
from dataclasses import dataclass
from enum import Enum

from .component_recognizer import Component, ComponentType
from ..utils.logger import logger


class ValidationLevel(Enum):
    """验证级别"""
    PASS = "通过"
    WARNING = "警告"
    ERROR = "错误"


@dataclass
class ValidationIssue:
    """验证问题"""
    level: ValidationLevel
    component_id: str
    component_type: ComponentType
    issue_type: str
    message: str
    suggestion: str = ""

    def to_dict(self) -> Dict:
        return {
            'level': self.level.value,
            'component_id': self.component_id,
            'component_type': self.component_type.value,
            'issue_type': self.issue_type,
            'message': self.message,
            'suggestion': self.suggestion
        }


@dataclass
class ValidationResult:
    """验证结果"""
    total_components: int
    passed: int
    warnings: int
    errors: int
    issues: List[ValidationIssue]

    def to_dict(self) -> Dict:
        return {
            'total': self.total_components,
            'passed': self.passed,
            'warnings': self.warnings,
            'errors': self.errors,
            'pass_rate': f"{self.passed / self.total_components * 100:.1f}%" if self.total_components > 0 else "0%",
            'issues': [issue.to_dict() for issue in self.issues]
        }

    def get_summary(self) -> str:
        """获取摘要"""
        if self.total_components == 0:
            return "无构件需要验证"

        pass_rate = self.passed / self.total_components * 100
        summary = f"验证完成: {self.total_components} 个构件\n"
        summary += f"  ✅ 通过: {self.passed} ({pass_rate:.1f}%)\n"
        summary += f"  ⚠️  警告: {self.warnings}\n"
        summary += f"  ❌ 错误: {self.errors}\n"

        if self.errors > 0:
            summary += f"\n严重问题需要修正！"
        elif self.warnings > 0:
            summary += f"\n存在警告，建议检查"
        else:
            summary += f"\n所有构件验证通过 ✅"

        return summary


class ResultValidator:
    """
    工程量计算结果验证器

    验证维度：
    1. 尺寸合理性验证（参考GB规范）
    2. 构件类型一致性验证
    3. 数值范围验证
    4. 工程经验验证
    """

    # 建筑规范尺寸范围（单位：mm）
    # 参考：GB 50011-2010, GB 50009-2012, 16G101-1
    DIMENSION_RANGES = {
        ComponentType.BEAM: {
            'width': (200, 1000),      # 梁宽 200-1000mm
            'height': (250, 2000),     # 梁高 250-2000mm
            'length': (1000, 12000),   # 跨度 1-12m
            'width_height_ratio': (0.3, 1.0),  # 宽高比 0.3-1.0
        },
        ComponentType.COLUMN: {
            'width': (200, 1200),      # 柱宽 200-1200mm
            'height': (200, 1200),     # 柱高 200-1200mm
            'length': (2400, 6000),    # 层高 2.4-6m
            'diameter': (200, 1200),   # 圆柱直径 200-1200mm
        },
        ComponentType.WALL: {
            'width': (50, 500),        # 墙厚 50-500mm (隔墙到剪力墙)
            'height': (2400, 6000),    # 墙高 2.4-6m
            'length': (500, 15000),    # 墙长 0.5-15m
        },
        ComponentType.SLAB: {
            'width': (1000, 12000),    # 板宽 1-12m
            'height': (60, 300),       # 板厚 60-300mm
            'length': (1000, 12000),   # 板长 1-12m
        },
        ComponentType.DOOR: {
            'width': (600, 3000),      # 门宽 0.6-3m
            'height': (1800, 3000),    # 门高 1.8-3m
            'length': (30, 60),        # 门扇厚度 30-60mm
        },
        ComponentType.WINDOW: {
            'width': (300, 4000),      # 窗宽 0.3-4m
            'height': (300, 3000),     # 窗高 0.3-3m
            'length': (40, 80),        # 窗框厚度 40-80mm
        },
        ComponentType.STAIR: {
            'width': (900, 2400),      # 梯宽 0.9-2.4m
            'height': (2400, 6000),    # 层高 2.4-6m
            'length': (2000, 8000),    # 梯跑长度 2-8m
        },
    }

    # 常见尺寸模数（mm）
    COMMON_MODULUS = {
        ComponentType.BEAM: {
            'width': [200, 250, 300, 350, 400, 450, 500, 600],
            'height': [300, 400, 450, 500, 600, 700, 800, 900, 1000],
        },
        ComponentType.COLUMN: {
            'width': [300, 400, 450, 500, 600, 700, 800],
            'height': [300, 400, 450, 500, 600, 700, 800],
            'diameter': [300, 400, 500, 600, 700, 800],
        },
        ComponentType.WALL: {
            'width': [100, 120, 150, 180, 200, 240, 300],  # 常见墙厚
        },
        ComponentType.SLAB: {
            'height': [80, 100, 120, 150, 180, 200, 250],  # 常见板厚
        },
    }

    def __init__(self):
        self.issues: List[ValidationIssue] = []
        logger.info("结果验证器初始化完成")

    def validate(self, components: List[Component]) -> ValidationResult:
        """
        验证构件列表

        Args:
            components: 构件列表

        Returns:
            ValidationResult: 验证结果
        """
        self.issues = []
        passed = 0
        warnings = 0
        errors = 0

        for component in components:
            component_issues = self._validate_component(component)

            if not component_issues:
                passed += 1
            else:
                has_error = any(issue.level == ValidationLevel.ERROR for issue in component_issues)
                if has_error:
                    errors += 1
                else:
                    warnings += 1

                self.issues.extend(component_issues)

        result = ValidationResult(
            total_components=len(components),
            passed=passed,
            warnings=warnings,
            errors=errors,
            issues=self.issues
        )

        logger.info(f"验证完成: {result.get_summary()}")
        return result

    def _validate_component(self, component: Component) -> List[ValidationIssue]:
        """验证单个构件"""
        issues = []

        # 1. 验证尺寸完整性
        issues.extend(self._validate_dimension_completeness(component))

        # 2. 验证尺寸范围
        issues.extend(self._validate_dimension_range(component))

        # 3. 验证尺寸比例
        issues.extend(self._validate_dimension_ratio(component))

        # 4. 验证尺寸模数
        issues.extend(self._validate_dimension_modulus(component))

        # 5. 验证体积/面积合理性
        issues.extend(self._validate_volume_area(component))

        return issues

    def _validate_dimension_completeness(self, component: Component) -> List[ValidationIssue]:
        """验证尺寸完整性"""
        issues = []

        if not component.dimensions:
            issues.append(ValidationIssue(
                level=ValidationLevel.ERROR,
                component_id=component.id,
                component_type=component.type,
                issue_type="缺失尺寸",
                message=f"构件 {component.name} 缺少尺寸信息",
                suggestion="请检查图纸标注或手动输入尺寸"
            ))
            return issues

        # 检查必需的维度
        required_dims = self._get_required_dimensions(component.type)
        missing_dims = []

        for dim in required_dims:
            if dim not in component.dimensions:
                missing_dims.append(dim)

        if missing_dims:
            issues.append(ValidationIssue(
                level=ValidationLevel.WARNING,
                component_id=component.id,
                component_type=component.type,
                issue_type="维度不完整",
                message=f"构件 {component.name} 缺少维度: {', '.join(missing_dims)}",
                suggestion=f"已使用规范默认值补充，建议核实: {component.dimensions}"
            ))

        return issues

    def _validate_dimension_range(self, component: Component) -> List[ValidationIssue]:
        """验证尺寸范围（基于建筑规范）"""
        issues = []

        ranges = self.DIMENSION_RANGES.get(component.type)
        if not ranges:
            return issues

        for dim_name, (min_val, max_val) in ranges.items():
            if dim_name in component.dimensions:
                value = component.dimensions[dim_name]

                if value < min_val or value > max_val:
                    level = ValidationLevel.ERROR if value < min_val * 0.5 or value > max_val * 2 else ValidationLevel.WARNING

                    issues.append(ValidationIssue(
                        level=level,
                        component_id=component.id,
                        component_type=component.type,
                        issue_type="尺寸超出规范范围",
                        message=f"构件 {component.name} 的 {dim_name}={value}mm 超出规范范围 [{min_val}, {max_val}]mm",
                        suggestion=f"请核实图纸标注，常规{component.type.value}{dim_name}范围为 {min_val}-{max_val}mm"
                    ))

        return issues

    def _validate_dimension_ratio(self, component: Component) -> List[ValidationIssue]:
        """验证尺寸比例"""
        issues = []

        dims = component.dimensions

        if component.type == ComponentType.BEAM:
            # 梁的宽高比验证
            if 'width' in dims and 'height' in dims:
                ratio = dims['width'] / dims['height']
                min_ratio, max_ratio = self.DIMENSION_RANGES[ComponentType.BEAM]['width_height_ratio']

                if ratio < min_ratio or ratio > max_ratio:
                    issues.append(ValidationIssue(
                        level=ValidationLevel.WARNING,
                        component_id=component.id,
                        component_type=component.type,
                        issue_type="宽高比异常",
                        message=f"梁 {component.name} 宽高比 {ratio:.2f} 异常（规范范围: {min_ratio}-{max_ratio}）",
                        suggestion=f"梁截面 {dims['width']}×{dims['height']}mm 可能不合理，请核实"
                    ))

        elif component.type == ComponentType.COLUMN:
            # 柱的截面尺寸比验证（方柱）
            if 'width' in dims and 'height' in dims:
                ratio = max(dims['width'], dims['height']) / min(dims['width'], dims['height'])

                if ratio > 3:  # 长宽比>3可能是墙而非柱
                    issues.append(ValidationIssue(
                        level=ValidationLevel.WARNING,
                        component_id=component.id,
                        component_type=component.type,
                        issue_type="构件类型可疑",
                        message=f"柱 {component.name} 长宽比过大 ({ratio:.1f})，可能是墙体",
                        suggestion=f"截面 {dims['width']}×{dims['height']}mm 更像墙体，建议核实构件类型"
                    ))

        elif component.type == ComponentType.SLAB:
            # 板的厚度与跨度比验证
            if 'height' in dims and 'length' in dims:
                span_thickness_ratio = dims['length'] / dims['height']

                # 单向板厚跨比 1/30~1/35，双向板 1/40~1/45
                if span_thickness_ratio < 20 or span_thickness_ratio > 50:
                    issues.append(ValidationIssue(
                        level=ValidationLevel.WARNING,
                        component_id=component.id,
                        component_type=component.type,
                        issue_type="厚跨比异常",
                        message=f"板 {component.name} 厚跨比 1/{span_thickness_ratio:.0f} 异常",
                        suggestion=f"板厚{dims['height']}mm，跨度{dims['length']}mm，厚跨比建议在1/30~1/45"
                    ))

        return issues

    def _validate_dimension_modulus(self, component: Component) -> List[ValidationIssue]:
        """验证尺寸模数（是否符合常见规格）"""
        issues = []

        modulus = self.COMMON_MODULUS.get(component.type)
        if not modulus:
            return issues

        for dim_name, common_values in modulus.items():
            if dim_name in component.dimensions:
                value = component.dimensions[dim_name]

                # 检查是否接近常见值（±20mm容差）
                is_common = any(abs(value - cv) <= 20 for cv in common_values)

                # 检查是否符合50mm模数（建筑模数）
                is_modular = (value % 50) <= 10 or (value % 50) >= 40

                if not is_common and not is_modular:
                    issues.append(ValidationIssue(
                        level=ValidationLevel.WARNING,
                        component_id=component.id,
                        component_type=component.type,
                        issue_type="非标准尺寸",
                        message=f"构件 {component.name} 的 {dim_name}={value}mm 不符合常见规格",
                        suggestion=f"常见{component.type.value}{dim_name}: {', '.join(map(str, common_values))}mm，请核实"
                    ))

        return issues

    def _validate_volume_area(self, component: Component) -> List[ValidationIssue]:
        """验证体积和面积合理性"""
        issues = []

        volume = component.calculate_volume()
        area = component.calculate_area()

        # 体积为0的ERROR
        if component.type in [ComponentType.BEAM, ComponentType.COLUMN, ComponentType.WALL, ComponentType.SLAB]:
            if volume == 0:
                issues.append(ValidationIssue(
                    level=ValidationLevel.ERROR,
                    component_id=component.id,
                    component_type=component.type,
                    issue_type="体积为零",
                    message=f"构件 {component.name} 体积为0，无法计算工程量",
                    suggestion=f"尺寸信息: {component.dimensions}，请补充缺失维度"
                ))

        # 体积过大的WARNING（可能是单位错误）
        if volume > 1000:  # 大于1000 m³
            issues.append(ValidationIssue(
                level=ValidationLevel.WARNING,
                component_id=component.id,
                component_type=component.type,
                issue_type="体积异常大",
                message=f"构件 {component.name} 体积 {volume:.2f} m³ 异常大",
                suggestion="请检查尺寸单位是否正确（应为mm）"
            ))

        return issues

    def _get_required_dimensions(self, component_type: ComponentType) -> List[str]:
        """获取构件类型所需的必需维度"""
        required = {
            ComponentType.BEAM: ['width', 'height', 'length'],
            ComponentType.COLUMN: ['width', 'height', 'length'],  # 或 ['diameter', 'length']
            ComponentType.WALL: ['width', 'height', 'length'],
            ComponentType.SLAB: ['width', 'height', 'length'],
            ComponentType.DOOR: ['width', 'height'],
            ComponentType.WINDOW: ['width', 'height'],
            ComponentType.STAIR: ['width', 'height', 'length'],
        }
        return required.get(component_type, [])

    def generate_report(self, result: ValidationResult) -> str:
        """生成验证报告"""
        report = "=" * 60 + "\n"
        report += "工程量计算结果验证报告\n"
        report += "=" * 60 + "\n\n"

        report += result.get_summary() + "\n"

        if result.issues:
            report += "\n" + "=" * 60 + "\n"
            report += "问题详情\n"
            report += "=" * 60 + "\n\n"

            # 按级别分组
            errors = [i for i in result.issues if i.level == ValidationLevel.ERROR]
            warnings = [i for i in result.issues if i.level == ValidationLevel.WARNING]

            if errors:
                report += "【严重错误】需要立即修正：\n\n"
                for issue in errors:
                    report += f"❌ {issue.component_type.value} - {issue.message}\n"
                    report += f"   建议: {issue.suggestion}\n\n"

            if warnings:
                report += "【警告】建议检查：\n\n"
                for issue in warnings:
                    report += f"⚠️  {issue.component_type.value} - {issue.message}\n"
                    report += f"   建议: {issue.suggestion}\n\n"

        report += "=" * 60 + "\n"
        return report
