"""
上下文管理器 - 聚合所有软件数据，为AI助手提供完整上下文
"""
from typing import Optional, Dict, List, Any
from dataclasses import dataclass

from ..dwg.entities import DWGDocument, EntityType, TextEntity
from ..translation.engine import TranslationStats
from ..calculation.component_recognizer import Component, ComponentType
from ..utils.logger import logger


@dataclass
class DWGContext:
    """DWG图纸上下文"""
    document: Optional[DWGDocument] = None
    filename: str = ""
    file_path: str = ""
    loaded_at: str = ""


@dataclass
class TranslationContext:
    """翻译上下文"""
    stats: Optional[TranslationStats] = None
    from_lang: str = ""
    to_lang: str = ""
    completed_at: str = ""


@dataclass
class CalculationContext:
    """算量上下文"""
    components: List[Component] = None
    confidences: List[float] = None
    completed_at: str = ""

    def __post_init__(self):
        if self.components is None:
            self.components = []
        if self.confidences is None:
            self.confidences = []


class ContextManager:
    """
    上下文管理器

    功能：
    1. 聚合DWG、翻译、算量等所有软件数据
    2. 提供统一的数据访问接口
    3. 为AI助手提供完整的软件上下文
    4. 支持数据更新通知
    """

    def __init__(self):
        """初始化上下文管理器"""
        # 各模块上下文
        self.dwg_context = DWGContext()
        self.translation_context = TranslationContext()
        self.calculation_context = CalculationContext()

        # 价格配置（用于成本估算）
        self.concrete_prices = {
            'C20': 350,  # 元/m³
            'C25': 370,
            'C30': 390,
            'C35': 410,
            'C40': 430,
            'C45': 450,
            'C50': 470,
        }

        self.rebar_prices = {
            'HPB300': 4000,  # 元/吨
            'HRB400': 4200,
            'HRB500': 4500,
        }

        logger.info("上下文管理器初始化完成")

    # ========== 数据设置接口 ==========

    def set_dwg_document(
        self,
        document: DWGDocument,
        filename: str = "",
        file_path: str = "",
        loaded_at: str = ""
    ):
        """
        设置DWG文档

        Args:
            document: DWG文档对象
            filename: 文件名
            file_path: 文件路径
            loaded_at: 加载时间
        """
        self.dwg_context.document = document
        self.dwg_context.filename = filename
        self.dwg_context.file_path = file_path
        self.dwg_context.loaded_at = loaded_at

        logger.info(f"DWG上下文已更新: {filename}")

    def set_translation_results(
        self,
        stats: TranslationStats,
        from_lang: str = "",
        to_lang: str = "",
        completed_at: str = ""
    ):
        """
        设置翻译结果

        Args:
            stats: 翻译统计信息
            from_lang: 源语言
            to_lang: 目标语言
            completed_at: 完成时间
        """
        self.translation_context.stats = stats
        self.translation_context.from_lang = from_lang
        self.translation_context.to_lang = to_lang
        self.translation_context.completed_at = completed_at

        logger.info(f"翻译上下文已更新: {from_lang}->{to_lang}, {stats.translated_count}条")

    def set_calculation_results(
        self,
        components: List[Component],
        confidences: Optional[List[float]] = None,
        completed_at: str = ""
    ):
        """
        设置算量结果

        Args:
            components: 识别的构件列表
            confidences: 构件识别置信度列表
            completed_at: 完成时间
        """
        self.calculation_context.components = components
        self.calculation_context.confidences = confidences or []
        self.calculation_context.completed_at = completed_at

        logger.info(f"算量上下文已更新: {len(components)}个构件")

    def clear_all(self):
        """清空所有上下文"""
        self.dwg_context = DWGContext()
        self.translation_context = TranslationContext()
        self.calculation_context = CalculationContext()

        logger.info("所有上下文已清空")

    # ========== 数据访问接口 ==========

    def get_dwg_info(self) -> Optional[Dict[str, Any]]:
        """
        获取DWG图纸信息

        Returns:
            Dict: 图纸信息字典，如果没有加载图纸则返回None
        """
        if not self.dwg_context.document:
            return None

        doc = self.dwg_context.document

        # 统计各类型实体
        text_count = sum(1 for e in doc.entities if isinstance(e, TextEntity))
        line_count = sum(1 for e in doc.entities if e.entity_type == EntityType.LINE)
        circle_count = sum(1 for e in doc.entities if e.entity_type in (EntityType.CIRCLE, EntityType.ARC))

        # 统计图层
        layers = set(e.layer for e in doc.entities if hasattr(e, 'layer'))

        return {
            'filename': self.dwg_context.filename,
            'file_path': self.dwg_context.file_path,
            'loaded_at': self.dwg_context.loaded_at,
            'entity_count': len(doc.entities),
            'text_entity_count': text_count,
            'line_entity_count': line_count,
            'circle_entity_count': circle_count,
            'layer_count': len(layers),
            'layers': list(layers)
        }

    def get_translation_info(self) -> Optional[Dict[str, Any]]:
        """
        获取翻译信息

        Returns:
            Dict: 翻译信息字典，如果没有翻译则返回None
        """
        if not self.translation_context.stats:
            return None

        stats = self.translation_context.stats
        stats_dict = stats.to_dict()

        # 添加额外信息
        stats_dict.update({
            'from_lang': self.translation_context.from_lang,
            'to_lang': self.translation_context.to_lang,
            'completed_at': self.translation_context.completed_at
        })

        return stats_dict

    def get_calculation_info(self, component_type: str = 'ALL') -> Optional[Dict[str, Any]]:
        """
        获取算量信息

        Args:
            component_type: 构件类型过滤 (BEAM/COLUMN/WALL/SLAB/ALL)

        Returns:
            Dict: 算量信息字典，如果没有算量结果则返回None
        """
        if not self.calculation_context.components:
            return None

        components = self.calculation_context.components

        # 按类型过滤
        if component_type != 'ALL':
            try:
                filter_type = ComponentType[component_type]
                components = [c for c in components if c.component_type == filter_type]
            except KeyError:
                logger.warning(f"未知构件类型: {component_type}")

        if not components:
            return {
                'component_count': 0,
                'total_volume': 0.0,
                'total_area': 0.0,
                'total_cost': 0.0,
                'by_type': {}
            }

        # 总计
        total_volume = sum(c.volume for c in components if c.volume)
        total_area = sum(c.area for c in components if c.area)
        total_cost = sum(c.cost_estimate for c in components if c.cost_estimate)

        # 按类型统计
        by_type = {}
        for comp_type in ComponentType:
            type_components = [c for c in components if c.component_type == comp_type]
            if type_components:
                by_type[comp_type.name] = {
                    'count': len(type_components),
                    'volume': sum(c.volume for c in type_components if c.volume),
                    'area': sum(c.area for c in type_components if c.area),
                    'cost': sum(c.cost_estimate for c in type_components if c.cost_estimate)
                }

        return {
            'component_count': len(components),
            'total_volume': total_volume,
            'total_area': total_area,
            'total_cost': total_cost,
            'by_type': by_type,
            'completed_at': self.calculation_context.completed_at
        }

    def get_material_summary(self) -> Optional[Dict[str, Any]]:
        """
        获取材料用量汇总

        Returns:
            Dict: 材料汇总字典，如果没有算量结果则返回None
        """
        if not self.calculation_context.components:
            return None

        components = self.calculation_context.components

        # 混凝土用量（按标号统计）
        concrete = {}
        for comp in components:
            if comp.material and 'C' in comp.material:  # C20, C30等
                grade = comp.material
                if comp.volume:
                    concrete[grade] = concrete.get(grade, 0.0) + comp.volume

        # 钢筋用量（简化统计，实际需要更详细的钢筋配置数据）
        # 这里按构件类型估算钢筋含量
        rebar = {
            'HRB400': 0.0,  # 主要钢筋
            'HPB300': 0.0,  # 箍筋
        }

        # 钢筋含量估算（kg/m³）
        rebar_density = {
            ComponentType.BEAM: 120,  # 梁：120kg/m³
            ComponentType.COLUMN: 150,  # 柱：150kg/m³
            ComponentType.WALL: 80,  # 墙：80kg/m³
            ComponentType.SLAB: 60,  # 板：60kg/m³
        }

        for comp in components:
            if comp.volume and comp.component_type in rebar_density:
                density = rebar_density[comp.component_type]
                weight = comp.volume * density / 1000  # 转换为吨
                # 80%主筋，20%箍筋
                rebar['HRB400'] += weight * 0.8
                rebar['HPB300'] += weight * 0.2

        return {
            'concrete': concrete,
            'rebar': rebar
        }

    def get_cost_estimate(self) -> Optional[Dict[str, Any]]:
        """
        获取成本估算

        Returns:
            Dict: 成本估算字典，如果没有算量结果则返回None
        """
        material_summary = self.get_material_summary()
        if not material_summary:
            return None

        # 混凝土成本
        concrete_cost = 0.0
        for grade, volume in material_summary['concrete'].items():
            price = self.concrete_prices.get(grade, 400)  # 默认400元/m³
            concrete_cost += volume * price

        # 钢筋成本
        rebar_cost = 0.0
        for spec, weight in material_summary['rebar'].items():
            price = self.rebar_prices.get(spec, 4200)  # 默认4200元/吨
            rebar_cost += weight * price

        # 其他成本（模板、人工等，按混凝土体积估算）
        total_concrete_volume = sum(material_summary['concrete'].values())
        other_cost = total_concrete_volume * 300  # 300元/m³

        total_cost = concrete_cost + rebar_cost + other_cost

        return {
            'total_cost': total_cost,
            'concrete_cost': concrete_cost,
            'rebar_cost': rebar_cost,
            'other_cost': other_cost,
            'breakdown': {
                '混凝土': concrete_cost,
                '钢筋': rebar_cost,
                '模板': other_cost * 0.4,
                '人工': other_cost * 0.6,
            }
        }

    def generate_report(self, report_type: str, format: str = 'text') -> str:
        """
        生成报表

        Args:
            report_type: 报表类型 (quantity_list/material_summary/cost_breakdown)
            format: 输出格式 (text/excel/pdf)

        Returns:
            str: 报表内容或路径
        """
        if report_type == 'quantity_list':
            return self._generate_quantity_list(format)
        elif report_type == 'material_summary':
            return self._generate_material_summary(format)
        elif report_type == 'cost_breakdown':
            return self._generate_cost_breakdown(format)
        else:
            raise ValueError(f"未知报表类型: {report_type}")

    def _generate_quantity_list(self, format: str) -> str:
        """生成工程量清单"""
        calc_info = self.get_calculation_info()
        if not calc_info:
            return "无算量数据"

        if format == 'text':
            lines = ["**工程量清单**\n"]
            lines.append(f"总构件数: {calc_info['component_count']}")
            lines.append(f"总体积: {calc_info['total_volume']:.2f} m³")
            lines.append(f"总面积: {calc_info['total_area']:.2f} m²")
            lines.append(f"总费用: ¥{calc_info['total_cost']:,.2f}\n")

            lines.append("**按构件类型：**")
            for ctype, data in calc_info['by_type'].items():
                lines.append(f"\n{ctype}:")
                lines.append(f"  数量: {data['count']}个")
                lines.append(f"  体积: {data['volume']:.2f} m³")
                lines.append(f"  面积: {data['area']:.2f} m²")
                lines.append(f"  费用: ¥{data['cost']:,.2f}")

            return '\n'.join(lines)

        elif format == 'excel':
            # TODO: 实现Excel导出
            return "Excel格式报表（待实现）"

        elif format == 'pdf':
            # TODO: 实现PDF导出
            return "PDF格式报表（待实现）"

        else:
            return f"不支持的格式: {format}"

    def _generate_material_summary(self, format: str) -> str:
        """生成材料汇总表"""
        material_summary = self.get_material_summary()
        if not material_summary:
            return "无材料数据"

        if format == 'text':
            lines = ["**材料用量汇总表**\n"]

            lines.append("**混凝土：**")
            total_concrete = 0.0
            for grade, volume in material_summary['concrete'].items():
                lines.append(f"  {grade}: {volume:.2f} m³")
                total_concrete += volume
            lines.append(f"  合计: {total_concrete:.2f} m³\n")

            lines.append("**钢筋：**")
            total_rebar = 0.0
            for spec, weight in material_summary['rebar'].items():
                lines.append(f"  {spec}: {weight:.2f} t")
                total_rebar += weight
            lines.append(f"  合计: {total_rebar:.2f} t")

            return '\n'.join(lines)

        else:
            return f"不支持的格式: {format}"

    def _generate_cost_breakdown(self, format: str) -> str:
        """生成成本分解表"""
        cost_info = self.get_cost_estimate()
        if not cost_info:
            return "无成本数据"

        if format == 'text':
            lines = ["**工程成本分解表**\n"]

            lines.append(f"**总成本: ¥{cost_info['total_cost']:,.2f}**\n")

            lines.append("**主要成本项：**")
            lines.append(f"  混凝土: ¥{cost_info['concrete_cost']:,.2f}")
            lines.append(f"  钢筋: ¥{cost_info['rebar_cost']:,.2f}")
            lines.append(f"  其他: ¥{cost_info['other_cost']:,.2f}\n")

            lines.append("**详细分解：**")
            for item, cost in cost_info['breakdown'].items():
                lines.append(f"  {item}: ¥{cost:,.2f}")

            return '\n'.join(lines)

        else:
            return f"不支持的格式: {format}"

    # ========== 辅助方法 ==========

    def has_dwg_data(self) -> bool:
        """是否有DWG数据"""
        return self.dwg_context.document is not None

    def has_translation_data(self) -> bool:
        """是否有翻译数据"""
        return self.translation_context.stats is not None

    def has_calculation_data(self) -> bool:
        """是否有算量数据"""
        return len(self.calculation_context.components) > 0

    def get_status_summary(self) -> str:
        """获取状态摘要"""
        parts = []

        if self.has_dwg_data():
            parts.append(f"✓ DWG图纸: {self.dwg_context.filename}")
        else:
            parts.append("✗ DWG图纸: 未加载")

        if self.has_translation_data():
            stats = self.translation_context.stats
            parts.append(f"✓ 翻译: {stats.translated_count}条 ({stats.average_quality_score})")
        else:
            parts.append("✗ 翻译: 未完成")

        if self.has_calculation_data():
            parts.append(f"✓ 算量: {len(self.calculation_context.components)}个构件")
        else:
            parts.append("✗ 算量: 未完成")

        return '\n'.join(parts)
