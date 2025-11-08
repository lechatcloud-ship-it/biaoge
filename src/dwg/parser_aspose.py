# -*- coding: utf-8 -*-
"""
DWG解析器 - 基于Aspose.CAD商业库
完美支持DWG R12-R2021所有版本
"""
import aspose.cad as cad
from aspose.cad import Image
from aspose.cad.fileformats.cad import CadImage
from aspose.cad.fileformats.cad.cadobjects import CadBaseEntity
from typing import Optional
from pathlib import Path

from .entities import (
    DWGDocument, Entity, Layer, TextStyle,
    LineEntity, CircleEntity, TextEntity, PolylineEntity,
    EntityType
)
from ..utils.logger import logger


class AsposeDWGParser:
    """
    基于Aspose.CAD的DWG解析器

    优势：
    - [是] 完整支持DWG R12-R2021
    - [是] 无需外部工具
    - [是] 性能优秀
    - [是] 商业级质量

    试用限制：
    - [警告] 输出有水印（读取无限制）
    """

    def parse(self, filepath: str) -> DWGDocument:
        """
        解析DWG文件

        Args:
            filepath: DWG文件路径

        Returns:
            解析后的DWG文档

        Raises:
            Exception: 解析失败
        """
        filepath = Path(filepath)

        if not filepath.exists():
            raise FileNotFoundError(f"文件不存在: {filepath}")

        try:
            logger.info(f"使用Aspose.CAD解析DWG: {filepath}")

            # 加载DWG文件
            cad_image = Image.load(str(filepath))

            if not isinstance(cad_image, CadImage):
                raise ValueError("不是有效的CAD文件")

            # 创建文档对象
            document = DWGDocument()

            # 获取版本信息
            document.version = cad_image.version if hasattr(cad_image, 'version') else "Unknown"

            # 解析图层
            self._parse_layers(cad_image, document)

            # 解析实体
            self._parse_entities(cad_image, document)

            # 元数据
            document.metadata = {
                "width": cad_image.width,
                "height": cad_image.height,
                "unit_type": str(cad_image.unit_type) if hasattr(cad_image, 'unit_type') else "Unknown",
            }

            logger.info(f"解析成功: {len(document.entities)} 个实体, {len(document.layers)} 个图层")

            return document

        except Exception as e:
            logger.error(f"Aspose.CAD解析失败: {e}", exc_info=True)
            raise

    def _parse_layers(self, cad_image: CadImage, document: DWGDocument):
        """解析图层"""
        try:
            if hasattr(cad_image, 'layers') and cad_image.layers:
                for layer in cad_image.layers:
                    layer_obj = Layer(
                        name=layer.name if hasattr(layer, 'name') else "0",
                        color=0,  # Aspose.CAD的颜色处理方式不同
                        linetype="Continuous",
                        lineweight=0,
                        visible=not layer.is_off if hasattr(layer, 'is_off') else True,
                        locked=layer.is_locked if hasattr(layer, 'is_locked') else False
                    )
                    document.layers.append(layer_obj)
        except Exception as e:
            logger.warning(f"图层解析失败: {e}")

    def _parse_entities(self, cad_image: CadImage, document: DWGDocument):
        """解析实体"""
        try:
            if not hasattr(cad_image, 'entities'):
                return

            for entity in cad_image.entities:
                parsed_entity = self._parse_entity(entity)
                if parsed_entity:
                    document.entities.append(parsed_entity)

        except Exception as e:
            logger.warning(f"实体解析失败: {e}")

    def _parse_entity(self, cad_entity: CadBaseEntity) -> Optional[Entity]:
        """
        解析单个实体

        Args:
            cad_entity: Aspose CAD实体对象

        Returns:
            解析后的实体对象
        """
        try:
            entity_type = cad_entity.type_name if hasattr(cad_entity, 'type_name') else "Unknown"

            # 根据实体类型解析
            if entity_type == "LWPOLYLINE" or entity_type == "POLYLINE":
                return self._parse_polyline(cad_entity)
            elif entity_type == "LINE":
                return self._parse_line(cad_entity)
            elif entity_type == "CIRCLE":
                return self._parse_circle(cad_entity)
            elif entity_type == "TEXT" or entity_type == "MTEXT":
                return self._parse_text(cad_entity)
            else:
                # 其他类型暂不处理
                return None

        except Exception as e:
            logger.debug(f"实体解析失败: {e}")
            return None

    def _parse_line(self, cad_entity) -> Optional[LineEntity]:
        """解析直线"""
        try:
            entity = LineEntity(EntityType.Line)

            if hasattr(cad_entity, 'first_point'):
                entity.start = (
                    cad_entity.first_point.x,
                    cad_entity.first_point.y,
                    cad_entity.first_point.z if hasattr(cad_entity.first_point, 'z') else 0
                )

            if hasattr(cad_entity, 'second_point'):
                entity.end = (
                    cad_entity.second_point.x,
                    cad_entity.second_point.y,
                    cad_entity.second_point.z if hasattr(cad_entity.second_point, 'z') else 0
                )

            entity.layer = cad_entity.layer_name if hasattr(cad_entity, 'layer_name') else "0"

            return entity
        except Exception as e:
            logger.debug(f"LINE解析失败: {e}")
            return None

    def _parse_circle(self, cad_entity) -> Optional[CircleEntity]:
        """解析圆"""
        try:
            entity = CircleEntity(EntityType.Circle)

            if hasattr(cad_entity, 'center_point'):
                entity.center = (
                    cad_entity.center_point.x,
                    cad_entity.center_point.y,
                    cad_entity.center_point.z if hasattr(cad_entity.center_point, 'z') else 0
                )

            if hasattr(cad_entity, 'radius'):
                entity.radius = cad_entity.radius

            entity.layer = cad_entity.layer_name if hasattr(cad_entity, 'layer_name') else "0"

            return entity
        except Exception as e:
            logger.debug(f"CIRCLE解析失败: {e}")
            return None

    def _parse_text(self, cad_entity) -> Optional[TextEntity]:
        """解析文本"""
        try:
            entity = TextEntity(EntityType.Text)

            # 获取文本内容
            if hasattr(cad_entity, 'default_value'):
                entity.text = cad_entity.default_value
            elif hasattr(cad_entity, 'text'):
                entity.text = cad_entity.text

            # 获取位置
            if hasattr(cad_entity, 'first_alignment_point'):
                entity.position = (
                    cad_entity.first_alignment_point.x,
                    cad_entity.first_alignment_point.y,
                    cad_entity.first_alignment_point.z if hasattr(cad_entity.first_alignment_point, 'z') else 0
                )
            elif hasattr(cad_entity, 'insertion_point'):
                entity.position = (
                    cad_entity.insertion_point.x,
                    cad_entity.insertion_point.y,
                    cad_entity.insertion_point.z if hasattr(cad_entity.insertion_point, 'z') else 0
                )

            # 获取高度
            if hasattr(cad_entity, 'height'):
                entity.height = cad_entity.height

            # 获取旋转角度
            if hasattr(cad_entity, 'rotation'):
                entity.rotation = cad_entity.rotation

            entity.layer = cad_entity.layer_name if hasattr(cad_entity, 'layer_name') else "0"

            return entity
        except Exception as e:
            logger.debug(f"TEXT解析失败: {e}")
            return None

    def _parse_polyline(self, cad_entity) -> Optional[PolylineEntity]:
        """解析多段线"""
        try:
            entity = PolylineEntity(EntityType.Polyline)

            # 获取顶点
            if hasattr(cad_entity, 'vertices'):
                for vertex in cad_entity.vertices:
                    if hasattr(vertex, 'location'):
                        point = (
                            vertex.location.x,
                            vertex.location.y,
                            vertex.location.z if hasattr(vertex.location, 'z') else 0
                        )
                        entity.points.append(point)

            # 是否闭合
            if hasattr(cad_entity, 'flag'):
                entity.closed = bool(cad_entity.flag & 1)

            entity.layer = cad_entity.layer_name if hasattr(cad_entity, 'layer_name') else "0"

            return entity
        except Exception as e:
            logger.debug(f"POLYLINE解析失败: {e}")
            return None
