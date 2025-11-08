# -*- coding: utf-8 -*-
"""
构件识别引擎（基于AI）
"""
from typing import List, Dict, Optional
from dataclasses import dataclass
from enum import Enum

from ..dwg.entities import DWGDocument, Entity, LineEntity, TextEntity, CircleEntity, PolylineEntity
from ..services.bailian_client import BailianClient
from ..utils.logger import logger


class ComponentType(Enum):
    """构件类型"""
    BEAM = "梁"
    COLUMN = "柱"
    WALL = "墙"
    SLAB = "板"
    DOOR = "门"
    WINDOW = "窗"
    STAIR = "楼梯"
    UNKNOWN = "未知"


@dataclass
class Component:
    """构件"""
    id: str
    type: ComponentType
    name: str
    entities: List[Entity]
    properties: Dict
    dimensions: Dict  # 尺寸信息（长宽高）
    material: Optional[str] = None
    quantity: float = 1.0
    
    def calculate_volume(self) -> float:
        """计算体积"""
        if 'length' in self.dimensions and 'width' in self.dimensions and 'height' in self.dimensions:
            return self.dimensions['length'] * self.dimensions['width'] * self.dimensions['height']
        return 0.0
    
    def calculate_area(self) -> float:
        """计算面积"""
        if 'length' in self.dimensions and 'width' in self.dimensions:
            return self.dimensions['length'] * self.dimensions['width']
        return 0.0


class ComponentRecognizer:
    """构件识别器"""
    
    def __init__(self, client: Optional[BailianClient] = None, init_client: bool = True):
        """
        初始化构件识别器

        Args:
            client: BailianClient实例，None表示不使用AI
            init_client: 是否自动初始化client（测试时设为False）
        """
        if client is not None:
            self.client = client
        elif init_client:
            self.client = BailianClient()
        else:
            self.client = None
        logger.info("构件识别器初始化完成")
    
    def recognize_components(self, document: DWGDocument) -> List[Component]:
        """
        识别文档中的构件
        
        Args:
            document: DWG文档
        
        Returns:
            List[Component]: 识别出的构件列表
        """
        components = []
        
        # 1. 基于文本识别构件
        text_components = self._recognize_from_text(document)
        components.extend(text_components)
        
        # 2. 基于图形识别构件
        shape_components = self._recognize_from_shapes(document)
        components.extend(shape_components)
        
        logger.info(f"识别出 {len(components)} 个构件")
        return components
    
    def _recognize_from_text(self, document: DWGDocument) -> List[Component]:
        """从文本标注识别构件"""
        components = []

        text_entities = [e for e in document.entities if isinstance(e, TextEntity)]

        for entity in text_entities:
            text = entity.text or ""

            # 简单规则匹配
            component_type = self._classify_by_text(text)

            if component_type != ComponentType.UNKNOWN:
                # 提取尺寸信息
                dimensions = self._extract_dimensions(text)

                # 🆕 补充缺失的尺寸维度（基于建筑规范和标准做法）
                dimensions = self._supplement_missing_dimensions(
                    dimensions, component_type, text, document, entity
                )

                component = Component(
                    id=entity.id,
                    type=component_type,
                    name=text,
                    entities=[entity],
                    properties={'text': text},
                    dimensions=dimensions
                )
                components.append(component)

        return components
    
    def _recognize_from_shapes(self, document: DWGDocument) -> List[Component]:
        """从图形识别构件（简化版）"""
        components = []

        # 识别矩形（可能是柱子、墙等）
        polyline_entities = [e for e in document.entities if isinstance(e, PolylineEntity)]

        for entity in polyline_entities:
            if entity.closed and len(entity.points) == 4:
                # 可能是矩形构件
                dimensions = self._calculate_polyline_dimensions(entity)

                # 根据尺寸判断类型
                width = dimensions.get('width', 0)
                if width > 1000:  # 大于1米认为是墙
                    comp_type = ComponentType.WALL
                elif width < 600:  # 小于0.6米认为是柱
                    comp_type = ComponentType.COLUMN
                else:
                    comp_type = ComponentType.UNKNOWN

                # 🆕 补充缺失的尺寸维度
                dimensions = self._supplement_missing_dimensions(
                    dimensions, comp_type, "", document, entity
                )

                component = Component(
                    id=entity.id,
                    type=comp_type,
                    name=f"{comp_type.value}_{entity.id[:8]}",
                    entities=[entity],
                    properties={},
                    dimensions=dimensions
                )
                components.append(component)

        return components
    
    def _classify_by_text(self, text: str) -> ComponentType:
        """根据文本分类构件类型"""
        text = text.upper()
        
        if any(keyword in text for keyword in ['梁', 'BEAM', 'L-']):
            return ComponentType.BEAM
        elif any(keyword in text for keyword in ['柱', 'COLUMN', 'C-', 'KZ']):
            return ComponentType.COLUMN
        elif any(keyword in text for keyword in ['墙', 'WALL', 'W-']):
            return ComponentType.WALL
        elif any(keyword in text for keyword in ['板', 'SLAB', 'B-']):
            return ComponentType.SLAB
        elif any(keyword in text for keyword in ['门', 'DOOR', 'M-']):
            return ComponentType.DOOR
        elif any(keyword in text for keyword in ['窗', 'WINDOW', 'C-']):
            return ComponentType.WINDOW
        elif any(keyword in text for keyword in ['楼梯', 'STAIR', 'LT']):
            return ComponentType.STAIR
        
        return ComponentType.UNKNOWN
    
    def _extract_dimensions(self, text: str) -> Dict:
        """
        从文本提取尺寸信息（支持10+种CAD标注格式）

        支持的格式（参考AutoCAD和国标GB/T 50001-2017）：
        1. 300×600, 300*600, 300x600 (乘号/星号/x)
        2. 300X600, 300×600×900 (大写X，三维)
        3. φ300, Φ300, ø300 (直径标注)
        4. 300, 600, 900 (用逗号分隔的多个尺寸)
        5. b×h=300×600 (带标签)
        6. 300/600 (斜杠分隔)
        7. 300-600 (短横线分隔，非负数)
        8. C300×600 (带前缀编号)
        9. 300(600) (括号标注第二尺寸)
        10. 3000mm, 3m, 300cm (带单位，自动转换为mm)
        """
        import re

        dimensions = {}
        original_text = text

        # 预处理：统一单位到mm
        text = self._normalize_units(text)

        # 1. 直径标注（φ300, Φ300, ø300）
        diameter_pattern = r'[φΦø∅][\s]*(\d+(?:\.\d+)?)'
        diameter_match = re.search(diameter_pattern, text)
        if diameter_match:
            diameter = float(diameter_match.group(1))
            dimensions['diameter'] = diameter
            dimensions['width'] = diameter
            dimensions['height'] = diameter
            logger.debug(f"提取直径标注: φ{diameter}mm - {original_text}")
            return dimensions

        # 2. 三维尺寸标注（300×600×900）- 优先匹配
        triple_pattern = r'(\d+(?:\.\d+)?)\s*[×xX*]\s*(\d+(?:\.\d+)?)\s*[×xX*]\s*(\d+(?:\.\d+)?)'
        triple_match = re.search(triple_pattern, text)
        if triple_match:
            dimensions['width'] = float(triple_match.group(1))
            dimensions['height'] = float(triple_match.group(2))
            dimensions['length'] = float(triple_match.group(3))
            logger.debug(f"提取三维尺寸: {dimensions} - {original_text}")
            return dimensions

        # 3. 二维尺寸标注（300×600, 300*600, 300x600, 300X600, 300/600）
        double_pattern = r'(\d+(?:\.\d+)?)\s*[×xX*/]\s*(\d+(?:\.\d+)?)'
        double_match = re.search(double_pattern, text)
        if double_match:
            dimensions['width'] = float(double_match.group(1))
            dimensions['height'] = float(double_match.group(2))
            logger.debug(f"提取二维尺寸: {dimensions} - {original_text}")
            return dimensions

        # 4. 带标签的尺寸（b×h=300×600, B×H=300×600）
        labeled_pattern = r'[bBhHlL]\s*[×xX*]\s*[bBhHlL]\s*=\s*(\d+(?:\.\d+)?)\s*[×xX*]\s*(\d+(?:\.\d+)?)'
        labeled_match = re.search(labeled_pattern, text)
        if labeled_match:
            dimensions['width'] = float(labeled_match.group(1))
            dimensions['height'] = float(labeled_match.group(2))
            logger.debug(f"提取带标签尺寸: {dimensions} - {original_text}")
            return dimensions

        # 5. 括号标注（300(600)）
        paren_pattern = r'(\d+(?:\.\d+)?)\s*\(\s*(\d+(?:\.\d+)?)\s*\)'
        paren_match = re.search(paren_pattern, text)
        if paren_match:
            dimensions['width'] = float(paren_match.group(1))
            dimensions['height'] = float(paren_match.group(2))
            logger.debug(f"提取括号标注: {dimensions} - {original_text}")
            return dimensions

        # 6. 逗号分隔的多个尺寸（300, 600, 900 或 300,600,900）
        comma_pattern = r'(\d+(?:\.\d+)?)\s*[,，]\s*(\d+(?:\.\d+)?)\s*(?:[,，]\s*(\d+(?:\.\d+)?))?\s*'
        comma_match = re.search(comma_pattern, text)
        if comma_match:
            dimensions['width'] = float(comma_match.group(1))
            dimensions['height'] = float(comma_match.group(2))
            if comma_match.group(3):
                dimensions['length'] = float(comma_match.group(3))
            logger.debug(f"提取逗号分隔尺寸: {dimensions} - {original_text}")
            return dimensions

        # 7. 短横线分隔（仅当不是负数时）（300-600）
        dash_pattern = r'(?<!\d)(\d+(?:\.\d+)?)\s*[-]\s*(\d+(?:\.\d+)?)\s*(?:[-]\s*(\d+(?:\.\d+)?))?\s*(?!\d)'
        dash_match = re.search(dash_pattern, text)
        if dash_match:
            # 验证不是范围表示（如"2-5层"）
            if not re.search(r'[层楼F]', text):
                dimensions['width'] = float(dash_match.group(1))
                dimensions['height'] = float(dash_match.group(2))
                if dash_match.group(3):
                    dimensions['length'] = float(dash_match.group(3))
                logger.debug(f"提取短横线分隔尺寸: {dimensions} - {original_text}")
                return dimensions

        # 8. 单个数值（可能是长度、宽度、直径等）
        single_pattern = r'(\d+(?:\.\d+)?)'
        single_match = re.search(single_pattern, text)
        if single_match:
            value = float(single_match.group(1))
            # 根据上下文判断（简化版）
            if any(keyword in text.upper() for keyword in ['φ', 'Φ', 'ø', '∅', 'DIA', 'DIAMETER', '直径']):
                dimensions['diameter'] = value
                dimensions['width'] = value
                dimensions['height'] = value
            else:
                dimensions['width'] = value
            logger.debug(f"提取单个数值: {dimensions} - {original_text}")
            return dimensions

        # 未能提取到尺寸
        logger.warning(f"无法提取尺寸信息: {original_text}")
        return dimensions

    def _normalize_units(self, text: str) -> str:
        """
        统一单位到mm

        支持: m, cm, mm, ", ' (英寸、英尺)
        """
        import re

        # 米 -> mm (3m -> 3000)
        text = re.sub(r'(\d+(?:\.\d+)?)\s*m(?![a-z])', lambda m: str(float(m.group(1)) * 1000), text)

        # 厘米 -> mm (300cm -> 3000)
        text = re.sub(r'(\d+(?:\.\d+)?)\s*cm', lambda m: str(float(m.group(1)) * 10), text)

        # 英寸 -> mm (12" -> 304.8)
        text = re.sub(r'(\d+(?:\.\d+)?)\s*"', lambda m: str(float(m.group(1)) * 25.4), text)

        # 英尺 -> mm (10' -> 3048)
        text = re.sub(r"(\d+(?:\.\d+)?)\s*'", lambda m: str(float(m.group(1)) * 304.8), text)

        # 移除mm单位标识（保留数字）
        text = re.sub(r'\s*mm\b', '', text)

        return text

    def _supplement_missing_dimensions(
        self,
        dimensions: Dict,
        component_type: ComponentType,
        text: str,
        document: DWGDocument,
        entity: Entity
    ) -> Dict:
        """
        补充缺失的尺寸维度

        策略：
        1. 基于建筑规范的标准尺寸（GB 50011-2010, GB 50009-2012等）
        2. 搜索附近文本标注
        3. 根据构件类型的典型做法

        Args:
            dimensions: 已提取的尺寸
            component_type: 构件类型
            text: 原始文本
            document: DWG文档
            entity: 实体对象

        Returns:
            补充后的尺寸字典
        """
        if not dimensions:
            dimensions = {}

        # 检查缺失的维度
        has_width = 'width' in dimensions or 'diameter' in dimensions
        has_height = 'height' in dimensions
        has_length = 'length' in dimensions

        # 如果已经有完整的三维尺寸，直接返回
        if has_width and has_height and has_length:
            logger.debug(f"尺寸完整: {dimensions}")
            return dimensions

        # === 策略1: 基于建筑规范的标准尺寸 ===
        standard_dims = self._get_standard_dimensions(component_type, dimensions, text)

        # === 策略2: 从附近文本标注中查找缺失维度 ===
        nearby_dims = self._search_nearby_dimensions(entity, document)

        # === 策略3: 合并尺寸信息 ===
        # 优先级: 已提取 > 附近标注 > 标准尺寸
        final_dimensions = {**standard_dims, **nearby_dims, **dimensions}

        # 记录补充信息
        if final_dimensions != dimensions:
            added_keys = set(final_dimensions.keys()) - set(dimensions.keys())
            logger.info(f"补充尺寸 [{component_type.value}] {text}: 新增 {added_keys} -> {final_dimensions}")

        return final_dimensions

    def _get_standard_dimensions(
        self,
        component_type: ComponentType,
        current_dims: Dict,
        text: str
    ) -> Dict:
        """
        获取标准尺寸（基于建筑规范）

        参考规范：
        - GB 50011-2010 建筑抗震设计规范
        - GB 50009-2012 建筑结构荷载规范
        - 16G101-1 混凝土结构施工图平面整体表示方法制图规则和构造详图
        """
        standard = {}

        if component_type == ComponentType.BEAM:
            # 梁：通常标注为 宽×高，长度需补充
            if 'width' in current_dims and 'height' in current_dims and 'length' not in current_dims:
                # 尝试从文本中提取跨度信息
                span = self._extract_span_from_text(text)
                if span:
                    standard['length'] = span
                else:
                    # 默认跨度 6000mm (6米，常见住宅跨度)
                    standard['length'] = 6000.0
                    logger.debug(f"梁：使用默认跨度6000mm")

        elif component_type == ComponentType.COLUMN:
            # 柱：通常标注为 宽×高（截面），层高需补充
            if 'width' in current_dims and 'height' in current_dims and 'length' not in current_dims:
                # 默认层高 3000mm (3米)
                standard['length'] = 3000.0
                logger.debug(f"柱：使用默认层高3000mm")
            elif 'diameter' in current_dims and 'length' not in current_dims:
                # 圆柱
                standard['length'] = 3000.0
                logger.debug(f"圆柱：使用默认层高3000mm")

        elif component_type == ComponentType.WALL:
            # 墙：通常标注厚度和长度，高度需补充
            if 'width' in current_dims and 'length' not in current_dims:
                # width是厚度，需要补充长度和高度
                # 默认层高
                standard['height'] = 3000.0
                # 默认墙长（根据厚度推断）
                thickness = current_dims.get('width', 0)
                if thickness < 150:  # 轻质墙
                    standard['length'] = 3000.0
                elif thickness < 300:  # 承重墙
                    standard['length'] = 6000.0
                else:  # 剪力墙
                    standard['length'] = 6000.0
                logger.debug(f"墙：补充高度{standard.get('height')}mm，长度{standard.get('length')}mm")

            elif 'width' in current_dims and 'height' in current_dims and 'length' not in current_dims:
                # 有厚度和高度，补充长度
                standard['length'] = 6000.0

        elif component_type == ComponentType.SLAB:
            # 板：通常只标注厚度，需要补充长度和宽度
            if 'width' in current_dims and 'length' not in current_dims and 'height' not in current_dims:
                # width是厚度
                thickness = current_dims.get('width', 0)
                # 常见楼板厚度: 100mm, 120mm, 150mm
                if thickness < 200:  # 确认是厚度
                    # 重新分配: width是厚度 -> height
                    standard['height'] = current_dims['width']
                    # 补充楼板的平面尺寸（默认一个开间）
                    standard['width'] = 3000.0  # 3米
                    standard['length'] = 6000.0  # 6米
                    logger.debug(f"板：厚度{thickness}mm，补充平面尺寸3000×6000mm")

        elif component_type == ComponentType.DOOR:
            # 门：通常标注宽×高，厚度可选
            if 'width' in current_dims and 'height' in current_dims and 'length' not in current_dims:
                # 门厚度（标准门扇厚度）
                standard['length'] = 40.0  # 40mm
                logger.debug(f"门：使用标准厚度40mm")

        elif component_type == ComponentType.WINDOW:
            # 窗：通常标注宽×高，厚度可选
            if 'width' in current_dims and 'height' in current_dims and 'length' not in current_dims:
                # 窗厚度（标准窗框厚度）
                standard['length'] = 50.0  # 50mm
                logger.debug(f"窗：使用标准厚度50mm")

        elif component_type == ComponentType.STAIR:
            # 楼梯：复杂构件，需要多个尺寸
            if 'width' in current_dims and 'length' not in current_dims and 'height' not in current_dims:
                # 楼梯宽度，补充踏步长度和层高
                standard['length'] = 3000.0  # 楼梯跑长度
                standard['height'] = 3000.0  # 层高
                logger.debug(f"楼梯：补充跑长3000mm，层高3000mm")

        return standard

    def _extract_span_from_text(self, text: str) -> Optional[float]:
        """从文本中提取跨度信息（如：L=6000, 跨度6m）"""
        import re

        # 匹配 L=6000, L=6m, 跨度6000, 跨度6m
        patterns = [
            r'L\s*=\s*(\d+(?:\.\d+)?)\s*m(?![a-z])',  # L=6m
            r'L\s*=\s*(\d+(?:\.\d+)?)',  # L=6000
            r'跨度\s*[:：]?\s*(\d+(?:\.\d+)?)\s*m(?![a-z])',  # 跨度:6m
            r'跨度\s*[:：]?\s*(\d+(?:\.\d+)?)',  # 跨度:6000
        ]

        for pattern in patterns:
            match = re.search(pattern, text)
            if match:
                value = float(match.group(1))
                # 判断单位
                if 'm(?![a-z])' in pattern:
                    value = value * 1000  # 米转毫米
                logger.debug(f"提取跨度: {value}mm from {text}")
                return value

        return None

    def _search_nearby_dimensions(
        self,
        entity: Entity,
        document: DWGDocument,
        search_radius: float = 500.0  # 搜索半径 500mm
    ) -> Dict:
        """
        搜索附近文本标注中的尺寸信息

        Args:
            entity: 当前实体
            document: DWG文档
            search_radius: 搜索半径(mm)

        Returns:
            找到的尺寸信息
        """
        nearby_dims = {}

        # 获取当前实体的位置
        if not hasattr(entity, 'position') or not entity.position:
            return nearby_dims

        current_pos = entity.position
        cx, cy = current_pos[0], current_pos[1]

        # 搜索附近的文本实体
        text_entities = [e for e in document.entities if isinstance(e, TextEntity)]

        for text_entity in text_entities:
            if text_entity.id == entity.id:
                continue  # 跳过自己

            if not hasattr(text_entity, 'position') or not text_entity.position:
                continue

            # 计算距离
            tx, ty = text_entity.position[0], text_entity.position[1]
            distance = ((cx - tx) ** 2 + (cy - ty) ** 2) ** 0.5

            if distance <= search_radius:
                # 在搜索半径内，提取尺寸
                text = text_entity.text or ""
                dims = self._extract_dimensions(text)

                if dims:
                    logger.debug(f"找到附近标注 (距离{distance:.0f}mm): {text} -> {dims}")
                    # 合并维度（不覆盖已有的）
                    for key, value in dims.items():
                        if key not in nearby_dims:
                            nearby_dims[key] = value

        return nearby_dims

    def _calculate_polyline_dimensions(self, polyline: PolylineEntity) -> Dict:
        """计算多段线的尺寸"""
        if not polyline.points or len(polyline.points) < 2:
            return {}
        
        # 计算包围盒
        xs = [p[0] for p in polyline.points]
        ys = [p[1] for p in polyline.points]
        
        width = max(xs) - min(xs)
        height = max(ys) - min(ys)
        
        return {
            'width': width,
            'height': height
        }
    
    def recognize_with_ai(self, document: DWGDocument, context: str = "") -> List[Component]:
        """
        使用AI识别构件（高级版 + Few-Shot Learning）

        Args:
            document: DWG文档
            context: 上下文信息（如：建筑类型）

        Returns:
            List[Component]: 识别出的构件
        """
        # 收集图纸信息
        text_info = []
        for entity in document.entities:
            if isinstance(entity, TextEntity) and entity.text:
                text_info.append(entity.text)

        if not text_info:
            return []

        # 🆕 构建Few-Shot Learning Prompt（专业工程知识）
        prompt = f"""你是一个精通建筑工程的CAD图纸识别专家，掌握：
- 建筑结构施工图识别
- 16G101-1图集标准标注
- GB 50011-2010抗震设计规范
- 工程量计算清单规范

【任务】识别以下CAD图纸文本标注中的建筑构件，提取构件类型和尺寸。

【Few-Shot示例】学习以下标注识别模式：

示例1（梁）：
输入: "KL1 300×600"
输出: {{"type": "梁", "name": "KL1", "dimensions": {{"width": 300, "height": 600, "length": 6000}}}}

示例2（柱）：
输入: "KZ1 600×600"
输出: {{"type": "柱", "name": "KZ1", "dimensions": {{"width": 600, "height": 600, "length": 3000}}}}

示例3（柱-圆形）：
输入: "φ500"
输出: {{"type": "柱", "name": "圆柱φ500", "dimensions": {{"diameter": 500, "width": 500, "height": 500, "length": 3000}}}}

示例4（墙）：
输入: "剪力墙 200厚"
输出: {{"type": "墙", "name": "剪力墙", "dimensions": {{"width": 200, "height": 3000, "length": 6000}}}}

示例5（板）：
输入: "楼板120厚"
输出: {{"type": "板", "name": "楼板", "dimensions": {{"width": 3000, "height": 120, "length": 6000}}}}

示例6（梁-带跨度）：
输入: "L1 250×500 L=7200"
输出: {{"type": "梁", "name": "L1", "dimensions": {{"width": 250, "height": 500, "length": 7200}}}}

【关键识别规则】
1. 梁（L/KL/B）：标注为"宽×高"（截面），长度=跨度（默认6000mm）
2. 柱（Z/KZ/C）：标注为"宽×高"（截面），长度=层高（默认3000mm）
3. 墙（Q/W）：标注为"厚度"，需补充高度（默认3000mm）和长度（默认6000mm）
4. 板（B）：标注为"厚度"，需补充平面尺寸（默认3000×6000mm）
5. 直径标注（φ/Φ/ø）：圆形构件，width=height=diameter
6. 单位统一为mm

【待识别文本】
{chr(10).join(text_info[:50])}

{f'【图纸类型】{context}' if context else ''}

【输出格式】严格JSON数组，每个构件必须包含：
[
  {{
    "type": "梁/柱/墙/板/门/窗/楼梯",
    "name": "构件名称或编号",
    "dimensions": {{
      "width": 数值,
      "height": 数值,
      "length": 数值
    }}
  }}
]

【注意】
- 如果标注缺失维度，请基于建筑规范补充默认值
- 所有尺寸必须为数值(mm)，不要包含单位字符串
- 不确定的标注可以跳过，不要猜测
- 返回有效的JSON格式，不要包含注释或markdown标记
"""
        
        try:
            messages = [
                {'role': 'user', 'content': prompt}
            ]
            # 🆕 使用calculation任务类型，调用qwen-max模型（强推理能力）
            model = self.client.get_model_for_task('calculation')
            response = self.client._call_api(messages, model)
            
            # 解析AI返回的JSON
            import json
            components_data = json.loads(response['translated_text'])
            
            components = []
            for data in components_data:
                comp_type = self._parse_component_type(data.get('type', ''))
                component = Component(
                    id=f"ai_{len(components)}",
                    type=comp_type,
                    name=data.get('name', ''),
                    entities=[],
                    properties=data,
                    dimensions=data.get('dimensions', {})
                )
                components.append(component)
            
            logger.info(f"AI识别出 {len(components)} 个构件")
            return components
        
        except Exception as e:
            logger.error(f"AI识别失败: {e}")
            return []
    
    def _parse_component_type(self, type_str: str) -> ComponentType:
        """解析构件类型字符串"""
        type_map = {
            '梁': ComponentType.BEAM,
            '柱': ComponentType.COLUMN,
            '墙': ComponentType.WALL,
            '板': ComponentType.SLAB,
            '门': ComponentType.DOOR,
            '窗': ComponentType.WINDOW,
            '楼梯': ComponentType.STAIR,
        }
        return type_map.get(type_str, ComponentType.UNKNOWN)
