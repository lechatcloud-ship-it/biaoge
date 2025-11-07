"""高级构件识别 - 商业级算法"""
import re
from typing import List, Dict
from ..dwg.entities import DWGDocument, Entity, TextEntity
from .component_recognizer import Component, ComponentType
from ..services.bailian_client import BailianClient
from ..utils.logger import logger
from ..utils.error_recovery import safe_execute

class AdvancedComponentRecognizer:
    """高级构件识别器"""
    
    # 构件类型关键词字典（更全面）
    KEYWORDS = {
        ComponentType.BEAM: ['梁', 'BEAM', 'L-', 'KL', 'GL', 'JL', 'WKL'],
        ComponentType.COLUMN: ['柱', 'COLUMN', 'KZ', 'Z-', 'GZ', 'LZ'],
        ComponentType.WALL: ['墙', 'WALL', 'W-', 'Q-', 'NQ', 'WQ'],
        ComponentType.SLAB: ['板', 'SLAB', 'B-', 'LB', 'TB', '楼板', '屋面板'],
        ComponentType.DOOR: ['门', 'DOOR', 'M-', 'FM', 'MU'],
        ComponentType.WINDOW: ['窗', 'WINDOW', 'C-', 'MC'],
        ComponentType.STAIR: ['楼梯', 'STAIR', 'LT', 'STAIRS'],
    }
    
    # 材料关键词
    MATERIALS = {
        '混凝土': ['C20', 'C25', 'C30', 'C35', 'C40', '混凝土'],
        '钢材': ['Q235', 'Q345', 'HRB', 'HPB', '钢筋'],
        '砖': ['MU', '砌体', '砖墙'],
    }
    
    def __init__(self, use_ai=False):
        self.use_ai = use_ai
        self.client = BailianClient() if use_ai else None
        logger.info(f"高级构件识别器初始化 (AI: {use_ai})")
    
    @safe_execute(fallback_value=[])
    def recognize(self, document: DWGDocument) -> List[Component]:
        """高级识别"""
        components = []
        
        # 1. 基于文本的精确识别
        text_components = self._recognize_from_text_advanced(document)
        components.extend(text_components)
        
        # 2. 基于图形的几何识别
        shape_components = self._recognize_from_geometry(document)
        components.extend(shape_components)
        
        # 3. AI辅助识别（可选）
        if self.use_ai and len(components) < 10:
            ai_components = self._recognize_with_ai(document)
            components.extend(ai_components)
        
        # 4. 去重合并
        components = self._merge_duplicates(components)
        
        logger.info(f"高级识别完成: {len(components)} 个构件")
        return components
    
    def _recognize_from_text_advanced(self, document: DWGDocument) -> List[Component]:
        """高级文本识别"""
        components = []
        text_entities = [e for e in document.entities if isinstance(e, TextEntity)]
        
        for entity in text_entities:
            text = entity.text or ""
            text_upper = text.upper()
            
            # 精确匹配
            comp_type = self._classify_advanced(text_upper)
            if comp_type == ComponentType.UNKNOWN:
                continue
            
            # 提取详细信息
            dimensions = self._extract_dimensions_advanced(text)
            material = self._extract_material(text)
            spec = self._extract_specification(text)
            
            component = Component(
                id=entity.id,
                type=comp_type,
                name=text,
                entities=[entity],
                properties={
                    'text': text,
                    'material': material,
                    'specification': spec,
                },
                dimensions=dimensions,
                material=material
            )
            components.append(component)
        
        return components
    
    def _classify_advanced(self, text: str) -> ComponentType:
        """高级分类算法"""
        # 按关键词匹配，支持更多变体
        for comp_type, keywords in self.KEYWORDS.items():
            for keyword in keywords:
                if keyword in text:
                    return comp_type
        
        # 正则表达式匹配
        patterns = {
            ComponentType.BEAM: r'L[-\d]|KL[-\d]|[框连]梁',
            ComponentType.COLUMN: r'KZ[-\d]|Z[-\d]|框架柱',
            ComponentType.WALL: r'[内外剪力]墙|Q[-\d]',
        }
        
        for comp_type, pattern in patterns.items():
            if re.search(pattern, text):
                return comp_type
        
        return ComponentType.UNKNOWN
    
    def _extract_dimensions_advanced(self, text: str) -> Dict:
        """高级尺寸提取"""
        dimensions = {}
        
        # 匹配 300×600、300*600、300X600 等格式
        patterns = [
            r'(\d+)[×xX*](\d+)(?:[×xX*](\d+))?',
            r'b=(\d+)[,，\s]+h=(\d+)',
            r'宽(\d+)[,，\s]*高(\d+)',
        ]
        
        for pattern in patterns:
            match = re.search(pattern, text)
            if match:
                dimensions['width'] = float(match.group(1))
                dimensions['height'] = float(match.group(2))
                if match.lastindex >= 3 and match.group(3):
                    dimensions['length'] = float(match.group(3))
                break
        
        return dimensions
    
    def _extract_material(self, text: str) -> str:
        """提取材料信息"""
        for material, keywords in self.MATERIALS.items():
            for keyword in keywords:
                if keyword in text:
                    return material
        return "未知"
    
    def _extract_specification(self, text: str) -> str:
        """提取规格信息"""
        # 提取C30、Q345等规格
        spec_patterns = [
            r'(C\d{2,3})',
            r'(Q\d{3})',
            r'(HRB\d{3})',
            r'(MU\d{1,2})',
        ]
        
        for pattern in spec_patterns:
            match = re.search(pattern, text)
            if match:
                return match.group(1)
        return ""
    
    def _recognize_from_geometry(self, document: DWGDocument) -> List[Component]:
        """基于几何特征识别"""
        # 简化实现
        return []
    
    @safe_execute(fallback_value=[])
    def _recognize_with_ai(self, document: DWGDocument) -> List[Component]:
        """AI辅助识别"""
        if not self.client:
            return []
        
        # 构建prompt
        text_samples = []
        for e in document.entities:
            if isinstance(e, TextEntity) and e.text:
                text_samples.append(e.text)
                if len(text_samples) >= 50:
                    break
        
        if not text_samples:
            return []
        
        prompt = f"""你是专业的建筑图纸识别AI。请识别以下文本中的构件类型和规格：

{chr(10).join(text_samples[:20])}

返回JSON格式：
[{{"type": "梁/柱/墙", "name": "KL-1", "dimensions": {{"width": 300, "height": 600}}, "material": "C30混凝土"}}]

只返回JSON，不要其他内容。"""
        
        try:
            messages = [{'role': 'user', 'content': prompt}]
            response = self.client._call_api(messages)
            
            import json
            result = json.loads(response['translated_text'].strip())
            
            components = []
            for item in result:
                comp_type = self._parse_type(item.get('type', ''))
                if comp_type != ComponentType.UNKNOWN:
                    component = Component(
                        id=f"ai_{len(components)}",
                        type=comp_type,
                        name=item.get('name', ''),
                        entities=[],
                        properties=item,
                        dimensions=item.get('dimensions', {}),
                        material=item.get('material', '')
                    )
                    components.append(component)
            
            return components
        except Exception as e:
            logger.error(f"AI识别失败: {e}")
            return []
    
    def _parse_type(self, type_str: str) -> ComponentType:
        """解析类型字符串"""
        type_map = {
            '梁': ComponentType.BEAM,
            '柱': ComponentType.COLUMN,
            '墙': ComponentType.WALL,
            '板': ComponentType.SLAB,
        }
        return type_map.get(type_str, ComponentType.UNKNOWN)
    
    def _merge_duplicates(self, components: List[Component]) -> List[Component]:
        """合并重复构件"""
        seen = set()
        unique = []
        
        for comp in components:
            key = (comp.type, comp.name)
            if key not in seen:
                seen.add(key)
                unique.append(comp)
        
        return unique
