# -*- coding: utf-8 -*-
"""
工程量计算模块（Numba加速）
"""
from typing import List, Dict
from dataclasses import dataclass
import numpy as np

try:
    from numba import jit
    NUMBA_AVAILABLE = True
except ImportError:
    NUMBA_AVAILABLE = False
    # Fallback decorator
    def jit(*args, **kwargs):
        def decorator(func):
            return func
        return decorator

from .component_recognizer import Component, ComponentType
from ..utils.logger import logger


@dataclass
class QuantityResult:
    """工程量计算结果"""
    component_type: ComponentType
    count: int
    total_volume: float  # m³
    total_area: float  # m²
    total_length: float  # m
    unit_price: float = 0.0  # 元/单位
    total_cost: float = 0.0  # 元
    
    def to_dict(self) -> Dict:
        return {
            'type': self.component_type.value,
            'count': self.count,
            'volume': f"{self.total_volume:.2f} m³",
            'area': f"{self.total_area:.2f} m²",
            'length': f"{self.total_length:.2f} m",
            'cost': f"¥{self.total_cost:.2f}"
        }


class QuantityCalculator:
    """工程量计算器"""
    
    # 单价表（示例）
    UNIT_PRICES = {
        ComponentType.BEAM: {'volume': 450.0},  # 元/m³
        ComponentType.COLUMN: {'volume': 480.0},
        ComponentType.WALL: {'area': 120.0},  # 元/m²
        ComponentType.SLAB: {'area': 350.0},
        ComponentType.DOOR: {'count': 800.0},  # 元/个
        ComponentType.WINDOW: {'count': 600.0},
    }
    
    def __init__(self):
        self.numba_enabled = NUMBA_AVAILABLE
        logger.info(f"工程量计算器初始化 (Numba: {'启用' if self.numba_enabled else '禁用'})")
    
    def calculate(self, components: List[Component]) -> Dict[ComponentType, QuantityResult]:
        """
        计算工程量
        
        Args:
            components: 构件列表
        
        Returns:
            Dict[ComponentType, QuantityResult]: 按类型分组的计算结果
        """
        results = {}
        
        # 按类型分组
        grouped = self._group_by_type(components)
        
        for comp_type, comp_list in grouped.items():
            result = self._calculate_type(comp_type, comp_list)
            results[comp_type] = result
        
        logger.info(f"工程量计算完成: {len(results)} 种构件类型")
        return results
    
    def _group_by_type(self, components: List[Component]) -> Dict[ComponentType, List[Component]]:
        """按类型分组"""
        grouped = {}
        for comp in components:
            if comp.type not in grouped:
                grouped[comp.type] = []
            grouped[comp.type].append(comp)
        return grouped
    
    def _calculate_type(self, comp_type: ComponentType, components: List[Component]) -> QuantityResult:
        """计算单个类型的工程量"""
        count = len(components)
        
        # 收集所有尺寸
        volumes = []
        areas = []
        lengths = []
        
        for comp in components:
            volumes.append(comp.calculate_volume())
            areas.append(comp.calculate_area())
            
            # 长度（取最大边）
            if comp.dimensions:
                length = max(comp.dimensions.values()) if comp.dimensions else 0
                lengths.append(length)
        
        # 使用Numba加速求和
        total_volume = self._sum_array(np.array(volumes)) / 1000000  # mm³ to m³
        total_area = self._sum_array(np.array(areas)) / 1000000  # mm² to m²
        total_length = self._sum_array(np.array(lengths)) / 1000  # mm to m
        
        # 计算成本
        unit_price_info = self.UNIT_PRICES.get(comp_type, {})
        total_cost = 0.0
        
        if 'volume' in unit_price_info and total_volume > 0:
            total_cost = total_volume * unit_price_info['volume']
        elif 'area' in unit_price_info and total_area > 0:
            total_cost = total_area * unit_price_info['area']
        elif 'count' in unit_price_info:
            total_cost = count * unit_price_info['count']
        
        return QuantityResult(
            component_type=comp_type,
            count=count,
            total_volume=total_volume,
            total_area=total_area,
            total_length=total_length,
            total_cost=total_cost
        )
    
    @staticmethod
    @jit(nopython=True)
    def _sum_array(arr):
        """Numba加速的数组求和"""
        return np.sum(arr)
    
    def generate_report(self, results: Dict[ComponentType, QuantityResult]) -> str:
        """生成工程量报表"""
        report = "=" * 60 + "\n"
        report += "工程量计算报表\n"
        report += "=" * 60 + "\n\n"
        
        total_cost = 0.0
        
        for comp_type, result in results.items():
            report += f"\n【{result.component_type.value}】\n"
            report += f"  数量: {result.count} 个\n"
            report += f"  体积: {result.total_volume:.2f} m³\n"
            report += f"  面积: {result.total_area:.2f} m²\n"
            report += f"  长度: {result.total_length:.2f} m\n"
            report += f"  费用: ¥{result.total_cost:.2f}\n"
            
            total_cost += result.total_cost
        
        report += "\n" + "=" * 60 + "\n"
        report += f"合计费用: ¥{total_cost:.2f}\n"
        report += "=" * 60 + "\n"
        
        return report
