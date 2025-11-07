"""
DWG解析器（基于ezdxf）
"""
import ezdxf
from typing import Optional
from pathlib import Path

from .entities import (
    DWGDocument, Entity, Layer, TextStyle,
    LineEntity, CircleEntity, TextEntity, PolylineEntity,
    EntityType
)
from ..utils.logger import logger


class DWGParseError(Exception):
    """DWG解析错误"""
    pass


class DWGParser:
    """DWG解析器"""

    def parse(self, filepath: str) -> DWGDocument:
        """
        解析DWG文件

        Args:
            filepath: DWG文件路径

        Returns:
            解析后的DWG文档模型

        Raises:
            DWGParseError: 解析失败
        """
        filepath = Path(filepath)

        if not filepath.exists():
            raise DWGParseError(f"文件不存在: {filepath}")

        try:
            logger.info(f"开始解析DWG文件: {filepath}")
            doc = ezdxf.readfile(str(filepath))
        except IOError as e:
            raise DWGParseError(f"无法读取文件: {e}")
        except ezdxf.DXFStructureError as e:
            raise DWGParseError(f"DWG文件格式错误: {e}")
        except Exception as e:
            raise DWGParseError(f"解析失败: {e}")

        # 创建文档模型
        dwg_document = DWGDocument()
        dwg_document.version = doc.dxfversion
        dwg_document.metadata = {
            'filename': filepath.name,
            'filepath': str(filepath),
            'filesize': filepath.stat().st_size
        }

        # 解析图层
        logger.info("解析图层...")
        for layer in doc.layers:
            dwg_document.layers.append(self._parse_layer(layer))

        # 解析文本样式
        logger.info("解析文本样式...")
        for style in doc.styles:
            dwg_document.text_styles.append(self._parse_text_style(style))

        # 解析实体
        logger.info("解析实体...")
        modelspace = doc.modelspace()
        entity_count = 0

        for entity in modelspace:
            parsed_entity = self._parse_entity(entity)
            if parsed_entity:
                dwg_document.entities.append(parsed_entity)
                entity_count += 1

        logger.info(f"解析完成: {entity_count}个实体, {len(dwg_document.layers)}个图层")

        return dwg_document

    def _parse_layer(self, layer) -> Layer:
        """解析图层"""
        return Layer(
            name=layer.dxf.name,
            color=layer.dxf.color if hasattr(layer.dxf, 'color') else 7,
            linetype=layer.dxf.linetype if hasattr(layer.dxf, 'linetype') else 'Continuous',
            lineweight=layer.dxf.lineweight if hasattr(layer.dxf, 'lineweight') else 0,
            visible=not layer.is_off(),
            locked=layer.is_locked()
        )

    def _parse_text_style(self, style) -> TextStyle:
        """解析文本样式"""
        return TextStyle(
            name=style.dxf.name,
            font=style.dxf.font if hasattr(style.dxf, 'font') else 'arial.ttf',
            height=style.dxf.height if hasattr(style.dxf, 'height') else 0.0
        )

    def _parse_entity(self, entity) -> Optional[Entity]:
        """解析单个实体"""
        entity_type = entity.dxftype()

        try:
            if entity_type == 'LINE':
                return self._parse_line(entity)
            elif entity_type == 'CIRCLE':
                return self._parse_circle(entity)
            elif entity_type in ['TEXT', 'MTEXT']:
                return self._parse_text(entity)
            elif entity_type in ['POLYLINE', 'LWPOLYLINE']:
                return self._parse_polyline(entity)
            else:
                # 其他类型暂不支持
                return None
        except Exception as e:
            logger.warning(f"解析实体失败 ({entity_type}): {e}")
            return None

    def _parse_line(self, entity) -> LineEntity:
        """解析直线"""
        return LineEntity(
            id=str(entity.dxf.handle),
            entity_type=EntityType.LINE,
            layer=entity.dxf.layer,
            color=self._get_color(entity),
            start=tuple(entity.dxf.start),
            end=tuple(entity.dxf.end),
            lineweight=entity.dxf.lineweight / 100.0 if hasattr(entity.dxf, 'lineweight') else 0.0
        )

    def _parse_circle(self, entity) -> CircleEntity:
        """解析圆"""
        return CircleEntity(
            id=str(entity.dxf.handle),
            entity_type=EntityType.CIRCLE,
            layer=entity.dxf.layer,
            color=self._get_color(entity),
            center=tuple(entity.dxf.center),
            radius=entity.dxf.radius
        )

    def _parse_text(self, entity) -> TextEntity:
        """解析文本"""
        text_content = entity.dxf.text if hasattr(entity.dxf, 'text') else ""
        position = tuple(entity.dxf.insert if hasattr(entity.dxf, 'insert') else (0, 0, 0))

        return TextEntity(
            id=str(entity.dxf.handle),
            entity_type=EntityType.TEXT,
            layer=entity.dxf.layer,
            color=self._get_color(entity),
            text=text_content,
            position=position,
            height=entity.dxf.height if hasattr(entity.dxf, 'height') else 0.0,
            rotation=entity.dxf.rotation if hasattr(entity.dxf, 'rotation') else 0.0,
            style=entity.dxf.style if hasattr(entity.dxf, 'style') else 'Standard'
        )

    def _parse_polyline(self, entity) -> PolylineEntity:
        """解析多段线"""
        # 获取所有点
        points = []
        if hasattr(entity, 'get_points'):
            points = [tuple(p) + (0.0,) if len(p) == 2 else tuple(p) for p in entity.get_points()]
        elif hasattr(entity, 'points'):
            points = [tuple(p) for p in entity.points()]

        return PolylineEntity(
            id=str(entity.dxf.handle),
            entity_type=EntityType.POLYLINE,
            layer=entity.dxf.layer,
            color=self._get_color(entity),
            points=points,
            closed=entity.is_closed if hasattr(entity, 'is_closed') else False,
            lineweight=entity.dxf.lineweight / 100.0 if hasattr(entity.dxf, 'lineweight') else 0.0
        )

    def _get_color(self, entity) -> str:
        """获取实体颜色（ACI颜色索引转RGB）"""
        try:
            aci = entity.dxf.color if hasattr(entity.dxf, 'color') else 7

            # ACI颜色表（简化版）
            aci_colors = {
                1: "#FF0000",  # 红
                2: "#FFFF00",  # 黄
                3: "#00FF00",  # 绿
                4: "#00FFFF",  # 青
                5: "#0000FF",  # 蓝
                6: "#FF00FF",  # 洋红
                7: "#FFFFFF",  # 白/黑
                0: "#000000",  # ByBlock
                256: "#000000",  # ByLayer
            }

            return aci_colors.get(aci, "#FFFFFF")
        except:
            return "#FFFFFF"


# 便捷函数
def parse_dwg_file(filepath: str) -> DWGDocument:
    """
    解析DWG文件的便捷函数

    Args:
        filepath: DWG文件路径

    Returns:
        解析后的DWG文档
    """
    parser = DWGParser()
    return parser.parse(filepath)
