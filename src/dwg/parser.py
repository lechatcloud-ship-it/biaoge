# -*- coding: utf-8 -*-
"""
DWG解析器（基于ezdxf）
"""
import ezdxf
from typing import Optional, Callable
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


class DWGPasswordError(DWGParseError):
    """DWG文件需要密码或密码错误"""
    pass


class DWGParser:
    """DWG解析器（支持密码保护）"""

    def __init__(self, password_callback: Optional[Callable[[str], tuple[str | None, bool]]] = None):
        """
        初始化解析器

        Args:
            password_callback: 密码输入回调函数，参数为文件名，返回(密码, 是否记住)
        """
        self.password_callback = password_callback

    def parse(self, filepath: str, password: Optional[str] = None) -> DWGDocument:
        """
        解析DWG文件（支持密码保护）

        Args:
            filepath: DWG文件路径
            password: 文件密码（可选）

        Returns:
            解析后的DWG文档模型

        Raises:
            DWGParseError: 解析失败
            DWGPasswordError: 需要密码或密码错误
        """
        filepath = Path(filepath)

        if not filepath.exists():
            raise DWGParseError(
                f"文件不存在\n\n"
                f"文件路径：{filepath}\n\n"
                "可能的原因：\n"
                "1. 文件已被移动或删除\n"
                "2. 文件路径输入错误\n"
                "3. 没有访问权限\n\n"
                "建议：请确认文件路径是否正确"
            )

        try:
            logger.info(f"开始解析DWG文件: {filepath}")

            # 注意：ezdxf本身不支持密码保护的DWG文件
            # 如果文件有密码保护，需要先用AutoCAD等软件解密
            doc = ezdxf.readfile(str(filepath))

        except IOError as e:
            error_msg = str(e).lower()

            # 检测是否为加密文件
            if any(keyword in error_msg for keyword in ['encrypt', 'password', 'protected', 'locked']):
                raise DWGPasswordError(
                    f"文件已加密，需要密码\n\n"
                    f"文件：{filepath.name}\n\n"
                    "[提示] 解决方案：\n\n"
                    "方法1（推荐）：使用AutoCAD解密\n"
                    "1. 用AutoCAD打开此文件\n"
                    "2. 输入密码解密\n"
                    "3. 另存为新文件（无密码）\n"
                    "4. 在本软件中打开新文件\n\n"
                    "方法2：使用DWG TrueView\n"
                    "• 下载免费的Autodesk DWG TrueView\n"
                    "• 打开文件并导出为DXF格式\n"
                    "• 在本软件中打开DXF文件\n\n"
                    "方法3：联系图纸提供方\n"
                    "• 请求提供无密码版本\n"
                    "• 或获取密码后自行解密\n\n"
                    "[警告] 注意：\n"
                    "由于技术限制，本软件无法直接打开加密的DWG文件。\n"
                    "这是为了保护知识产权和数据安全。"
                )

            raise DWGParseError(
                f"文件读取失败\n\n"
                f"文件：{filepath.name}\n"
                f"错误：{str(e)}\n\n"
                "可能的原因：\n"
                "1. 文件正被其他程序占用\n"
                "2. 文件权限不足\n"
                "3. 文件已加密（需要密码）\n"
                "4. 磁盘读取错误\n\n"
                "建议：\n"
                "• 关闭其他可能打开该文件的程序\n"
                "• 检查文件访问权限\n"
                "• 如果文件有密码，请先用AutoCAD解密\n"
                "• 尝试复制文件到其他位置后重试"
            )
        except ezdxf.DXFStructureError as e:
            error_msg = str(e).lower()

            # 检测是否为加密导致的结构错误
            if any(keyword in error_msg for keyword in ['encrypt', 'decode', 'invalid']):
                raise DWGPasswordError(
                    f"文件可能已加密或损坏\n\n"
                    f"文件：{filepath.name}\n"
                    f"错误：{str(e)}\n\n"
                    "如果文件已加密：\n"
                    "• 请使用AutoCAD打开并解密\n"
                    "• 另存为无密码版本后重试\n\n"
                    "如果文件未加密：\n"
                    "• 文件可能已损坏\n"
                    "• 尝试使用CAD软件修复"
                )

            raise DWGParseError(
                f"DWG文件格式错误\n\n"
                f"文件：{filepath.name}\n"
                f"错误：{str(e)}\n\n"
                "可能的原因：\n"
                "1. 文件已损坏\n"
                "2. 文件版本不受支持\n"
                "3. 文件不是有效的DWG/DXF格式\n"
                "4. 文件已加密（需要解密）\n\n"
                "建议：\n"
                "• 使用CAD软件打开并另存为DXF格式\n"
                "• 确认文件扩展名正确（.dwg或.dxf）\n"
                "• 如果文件有密码，请先解密\n"
                "• 尝试使用CAD软件修复文件"
            )
        except ezdxf.DXFVersionError as e:
            raise DWGParseError(
                f"DWG文件版本不支持\n\n"
                f"文件：{filepath.name}\n"
                f"错误：{str(e)}\n\n"
                "当前支持的版本：\n"
                "• R12 - R2024\n\n"
                "建议：\n"
                "• 使用AutoCAD等软件将文件另存为R2018或更早版本\n"
                "• 确认文件是否为有效的DWG格式"
            )
        except Exception as e:
            error_msg = str(e).lower()

            # 最后检查是否可能是加密问题
            if any(keyword in error_msg for keyword in ['encrypt', 'password', 'protected']):
                raise DWGPasswordError(
                    f"文件可能已加密\n\n"
                    f"文件：{filepath.name}\n"
                    f"错误：{str(e)}\n\n"
                    "请使用AutoCAD打开并解密后重试。"
                )

            raise DWGParseError(
                f"解析DWG文件时发生未知错误\n\n"
                f"文件：{filepath.name}\n"
                f"错误类型：{type(e).__name__}\n"
                f"错误信息：{str(e)[:200]}\n\n"
                "建议：\n"
                "• 检查文件是否完整\n"
                "• 确认文件未加密\n"
                "• 尝试用CAD软件打开文件验证其有效性\n"
                "• 如问题持续，请联系技术支持并提供错误信息"
            )

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
