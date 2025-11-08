# -*- coding: utf-8 -*-
"""
DWG实体数据模型
"""
from dataclasses import dataclass, field
from typing import List, Dict, Any, Tuple, Optional
from enum import Enum


class EntityType(Enum):
    """实体类型"""
    LINE = "LINE"
    CIRCLE = "CIRCLE"
    ARC = "ARC"
    POLYLINE = "POLYLINE"
    TEXT = "TEXT"
    MTEXT = "MTEXT"
    DIMENSION = "DIMENSION"
    INSERT = "INSERT"
    HATCH = "HATCH"
    SPLINE = "SPLINE"
    ELLIPSE = "ELLIPSE"
    UNKNOWN = "UNKNOWN"


@dataclass
class Layer:
    """图层"""
    name: str
    color: int
    linetype: str
    lineweight: int
    visible: bool = True
    locked: bool = False


@dataclass
class TextStyle:
    """文本样式"""
    name: str
    font: str
    height: float


@dataclass
class Entity:
    """实体基类"""
    id: str
    entity_type: EntityType
    layer: str
    color: str
    properties: Dict[str, Any] = field(default_factory=dict)


@dataclass
class LineEntity(Entity):
    """直线实体"""
    start: Tuple[float, float, float] = (0.0, 0.0, 0.0)
    end: Tuple[float, float, float] = (0.0, 0.0, 0.0)
    lineweight: float = 0.0

    def __post_init__(self):
        if not hasattr(self, 'entity_type') or self.entity_type is None:
            self.entity_type = EntityType.LINE


@dataclass
class CircleEntity(Entity):
    """圆实体"""
    center: Tuple[float, float, float] = (0.0, 0.0, 0.0)
    radius: float = 0.0

    def __post_init__(self):
        if not hasattr(self, 'entity_type') or self.entity_type is None:
            self.entity_type = EntityType.CIRCLE


@dataclass
class TextEntity(Entity):
    """文本实体"""
    text: str = ""
    position: Tuple[float, float, float] = (0.0, 0.0, 0.0)
    height: float = 0.0
    rotation: float = 0.0
    style: str = "Standard"
    translated_text: Optional[str] = None  # 翻译后的文本

    def __post_init__(self):
        if not hasattr(self, 'entity_type') or self.entity_type is None:
            self.entity_type = EntityType.TEXT


@dataclass
class PolylineEntity(Entity):
    """多段线实体"""
    points: List[Tuple[float, float, float]] = field(default_factory=list)
    closed: bool = False
    lineweight: float = 0.0

    def __post_init__(self):
        if not hasattr(self, 'entity_type') or self.entity_type is None:
            self.entity_type = EntityType.POLYLINE


@dataclass
class DWGDocument:
    """DWG文档模型"""
    version: str = ""
    layers: List[Layer] = field(default_factory=list)
    entities: List[Entity] = field(default_factory=list)
    text_styles: List[TextStyle] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)

    def get_layer(self, name: str) -> Optional[Layer]:
        """获取图层"""
        for layer in self.layers:
            if layer.name == name:
                return layer
        return None

    def get_entities_by_layer(self, layer_name: str) -> List[Entity]:
        """按图层获取实体"""
        return [e for e in self.entities if e.layer == layer_name]

    def get_text_entities(self) -> List[TextEntity]:
        """获取所有文本实体"""
        return [e for e in self.entities if isinstance(e, TextEntity)]

    @property
    def entity_count(self) -> int:
        """实体总数"""
        return len(self.entities)

    @property
    def layer_count(self) -> int:
        """图层总数"""
        return len(self.layers)
