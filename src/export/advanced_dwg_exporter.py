# -*- coding: utf-8 -*-
"""高级DWG导出器 - 商业级"""
import ezdxf
from ezdxf.enums import TextEntityAlignment
from ..dwg.entities import *
from ..utils.logger import logger
from ..utils.error_recovery import safe_execute, retry

class AdvancedDWGExporter:
    """高级DWG导出器"""
    
    def __init__(self):
        self.supported_versions = ['R2010', 'R2013', 'R2018', 'R2024']
        logger.info("高级DWG导出器初始化")
    
    @retry(max_attempts=3)
    @safe_execute(fallback_value=False)
    def export(self, document: DWGDocument, output_path: str, 
               version='R2018', include_layers=True) -> bool:
        """
        高级导出功能
        
        Args:
            document: DWG文档
            output_path: 输出路径
            version: DWG版本
            include_layers: 是否包含图层
        
        Returns:
            bool: 是否成功
        """
        logger.info(f"开始导出DWG: {output_path} (版本: {version})")
        
        # 创建新文档
        doc = ezdxf.new(version)
        msp = doc.modelspace()
        
        # 创建图层
        if include_layers:
            self._create_layers(doc, document)
        
        # 导出实体
        exported_count = 0
        for entity in document.entities:
            if self._export_entity(msp, entity, doc):
                exported_count += 1
        
        # 保存文件
        doc.saveas(output_path)
        
        logger.info(f"导出完成: {exported_count}/{len(document.entities)} 个实体")
        return True
    
    def _create_layers(self, doc, document: DWGDocument):
        """创建图层"""
        for layer in document.layers:
            if layer.name not in doc.layers:
                doc.layers.add(layer.name, color=layer.color)
    
    def _export_entity(self, msp, entity: Entity, doc) -> bool:
        """导出单个实体"""
        try:
            if isinstance(entity, LineEntity):
                msp.add_line(
                    entity.start[:2], 
                    entity.end[:2],
                    dxfattribs={
                        'layer': entity.layer,
                        'color': entity.color,
                        'lineweight': int(entity.lineweight * 100)
                    }
                )
                return True
            
            elif isinstance(entity, CircleEntity):
                msp.add_circle(
                    entity.center[:2],
                    entity.radius,
                    dxfattribs={
                        'layer': entity.layer,
                        'color': entity.color
                    }
                )
                return True
            
            elif isinstance(entity, TextEntity):
                # 使用翻译后的文本（如果有）
                text = entity.translated_text if entity.translated_text else entity.text
                if text:
                    msp.add_text(
                        text,
                        dxfattribs={
                            'layer': entity.layer,
                            'color': entity.color,
                            'height': entity.height,
                            'rotation': entity.rotation,
                            'insert': entity.position[:2]
                        }
                    )
                    return True
            
            elif isinstance(entity, PolylineEntity):
                if entity.points and len(entity.points) >= 2:
                    points = [p[:2] for p in entity.points]
                    msp.add_lwpolyline(
                        points,
                        close=entity.closed,
                        dxfattribs={
                            'layer': entity.layer,
                            'color': entity.color
                        }
                    )
                    return True
            
        except Exception as e:
            logger.warning(f"导出实体失败 ({entity.id}): {e}")
        
        return False
