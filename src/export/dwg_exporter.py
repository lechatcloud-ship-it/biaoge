# -*- coding: utf-8 -*-
"""DWG/DXF导出器"""
import ezdxf
from ..dwg.entities import DWGDocument, LineEntity, CircleEntity, TextEntity
from ..utils.logger import logger

class DWGExporter:
    def export(self, document: DWGDocument, output_path: str):
        """导出为DWG/DXF"""
        doc = ezdxf.new('R2010')
        msp = doc.modelspace()
        
        for entity in document.entities:
            if isinstance(entity, LineEntity):
                msp.add_line(entity.start[:2], entity.end[:2])
            elif isinstance(entity, CircleEntity):
                msp.add_circle(entity.center[:2], entity.radius)
            elif isinstance(entity, TextEntity):
                text = entity.translated_text or entity.text
                msp.add_text(text, dxfattribs={'insert': entity.position[:2], 'height': entity.height})
        
        doc.saveas(output_path)
        logger.info(f"导出DWG: {output_path}")
