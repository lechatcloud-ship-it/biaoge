# -*- coding: utf-8 -*-
"""
空间索引（R-tree）- 用于快速视锥剔除
"""
from typing import List, Tuple
from ..dwg.entities import Entity, LineEntity, CircleEntity, TextEntity, PolylineEntity
from ..utils.logger import logger

try:
    from rtree import index
    RTREE_AVAILABLE = True
except ImportError:
    RTREE_AVAILABLE = False
    logger.warning("rtree未安装，使用简单空间索引")


class SpatialIndex:
    """空间索引管理器"""
    
    def __init__(self):
        self.use_rtree = RTREE_AVAILABLE
        if self.use_rtree:
            self.idx = index.Index()
        else:
            self.entities_bbox = []
        self.entity_map = {}
        logger.info(f"空间索引初始化 (R-tree: {self.use_rtree})")
    
    def build(self, entities: List[Entity]):
        """批量构建索引"""
        for entity in entities:
            self.insert(entity)
        logger.debug(f"空间索引构建完成: {len(entities)}个实体")

    def insert(self, entity: Entity):
        """插入实体"""
        bbox = self._get_bbox(entity)
        if not bbox:
            return

        entity_id = id(entity)
        self.entity_map[entity_id] = entity

        if self.use_rtree:
            self.idx.insert(entity_id, bbox)
        else:
            self.entities_bbox.append((entity_id, bbox))
    
    def query(self, bbox: Tuple[float, float, float, float]) -> List[Entity]:
        """查询与边界框相交的实体"""
        if self.use_rtree:
            entity_ids = list(self.idx.intersection(bbox))
        else:
            # 简单的边界框相交检测
            entity_ids = []
            for eid, ebbox in self.entities_bbox:
                if self._bbox_intersects(bbox, ebbox):
                    entity_ids.append(eid)
        
        return [self.entity_map[eid] for eid in entity_ids if eid in self.entity_map]
    
    def _get_bbox(self, entity: Entity) -> Tuple[float, float, float, float]:
        """获取实体边界框 (min_x, min_y, max_x, max_y)"""
        if isinstance(entity, LineEntity):
            xs = [entity.start[0], entity.end[0]]
            ys = [entity.start[1], entity.end[1]]
            return (min(xs), min(ys), max(xs), max(ys))
        
        elif isinstance(entity, CircleEntity):
            cx, cy = entity.center[0], entity.center[1]
            r = entity.radius
            return (cx - r, cy - r, cx + r, cy + r)
        
        elif isinstance(entity, TextEntity):
            px, py = entity.position[0], entity.position[1]
            h = entity.height if entity.height > 0 else 10
            w = h * len(entity.text) * 0.6 if entity.text else h
            return (px, py, px + w, py + h)
        
        elif isinstance(entity, PolylineEntity):
            if not entity.points:
                return None
            xs = [p[0] for p in entity.points]
            ys = [p[1] for p in entity.points]
            return (min(xs), min(ys), max(xs), max(ys))
        
        return None
    
    def _bbox_intersects(self, bbox1, bbox2) -> bool:
        """检测两个边界框是否相交"""
        return not (bbox1[2] < bbox2[0] or bbox1[0] > bbox2[2] or
                   bbox1[3] < bbox2[1] or bbox1[1] > bbox2[3])
    
    def clear(self):
        """清空索引"""
        if self.use_rtree:
            self.idx = index.Index()
        else:
            self.entities_bbox.clear()
        self.entity_map.clear()
