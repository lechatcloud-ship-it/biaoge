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
    
    def __init__(self, client: Optional[BailianClient] = None):
        self.client = client or BailianClient()
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
                if dimensions['width'] > 1000:  # 大于1米认为是墙
                    comp_type = ComponentType.WALL
                elif dimensions['width'] < 600:  # 小于0.6米认为是柱
                    comp_type = ComponentType.COLUMN
                else:
                    comp_type = ComponentType.UNKNOWN
                
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
        """从文本提取尺寸信息"""
        import re
        
        dimensions = {}
        
        # 匹配 300×600 或 300*600 格式
        pattern = r'(\d+)[×x*](\d+)(?:[×x*](\d+))?'
        match = re.search(pattern, text)
        
        if match:
            dimensions['width'] = float(match.group(1))
            dimensions['height'] = float(match.group(2))
            if match.group(3):
                dimensions['length'] = float(match.group(3))
        
        return dimensions
    
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
        使用AI识别构件（高级版）
        
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
        
        # 构建AI prompt
        prompt = f"""你是一个专业的CAD图纸识别专家。
以下是图纸中的文本标注：

{chr(10).join(text_info[:50])}  # 最多50条

请识别这些文本中的建筑构件，输出JSON格式：
[
  {{"type": "梁/柱/墙/板", "name": "构件名称", "dimensions": {{"width": 300, "height": 600}}}}
]

{f'图纸类型：{context}' if context else ''}
"""
        
        try:
            messages = [
                {'role': 'user', 'content': prompt}
            ]
            response = self.client._call_api(messages)
            
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
