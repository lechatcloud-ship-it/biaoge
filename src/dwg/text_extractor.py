"""
DWG智能文本提取器
提取所有类型的文本实体，记录完整上下文信息
"""
from dataclasses import dataclass, field
from typing import List, Tuple, Optional, Any, Dict
from enum import Enum
import ezdxf
from ezdxf.document import Drawing
from ezdxf.entities import DXFEntity

from ..utils.logger import logger


class TextEntityType(Enum):
    """文本实体类型"""
    TEXT = "TEXT"              # 单行文本
    MTEXT = "MTEXT"            # 多行文本
    DIMENSION = "DIMENSION"    # 尺寸标注
    LEADER = "LEADER"          # 引线标注
    MULTILEADER = "MULTILEADER"  # 多重引线
    ATTRIB = "ATTRIB"          # 块属性
    ATTDEF = "ATTDEF"          # 块属性定义
    TABLE = "TABLE"            # 表格


class TextCategory(Enum):
    """文本内容分类"""
    PURE_NUMBER = "pure_number"          # 纯数字
    UNIT = "unit"                        # 单位符号
    PURE_TEXT = "pure_text"              # 纯文本
    MIXED = "mixed"                      # 混合文本
    SPECIAL_SYMBOL = "special_symbol"    # 特殊符号
    FORMULA = "formula"                  # 公式
    EMPTY = "empty"                      # 空文本


@dataclass
class ExtractedText:
    """
    提取的文本实体
    包含完整的上下文信息和原始实体引用
    """
    # ==== 唯一标识 ====
    entity_id: str                    # 实体句柄（唯一标识符）
    entity_type: TextEntityType       # 实体类型

    # ==== 原始实体引用 ====
    entity_ref: DXFEntity            # ezdxf实体对象引用（用于后续修改）

    # ==== 文本内容 ====
    original_text: str               # 原始文本内容
    translated_text: str = ""        # 翻译后的文本（初始为空）

    # ==== 核心属性（必须保持不变） ====
    position: Tuple[float, float, float] = (0.0, 0.0, 0.0)  # 位置
    height: float = 0.0              # 文字高度
    rotation: float = 0.0            # 旋转角度（度）
    width_factor: float = 1.0        # 宽度因子
    oblique: float = 0.0             # 倾斜角度

    # ==== 样式属性 ====
    style: str = "Standard"          # 文本样式名
    layer: str = "0"                 # 图层名
    color: int = 256                 # 颜色（256=ByLayer）
    linetype: str = "ByLayer"        # 线型
    lineweight: int = -1             # 线宽

    # ==== 对齐属性 ====
    halign: int = 0                  # 水平对齐
    valign: int = 0                  # 垂直对齐

    # ==== MTEXT特有属性 ====
    column_type: int = 0             # 列类型
    column_count: int = 1            # 列数
    char_height: float = 0.0         # 字符高度
    line_spacing_style: int = 1      # 行间距样式
    line_spacing_factor: float = 1.0 # 行间距因子

    # ==== DIMENSION特有属性 ====
    dim_type: int = 0                # 尺寸类型
    measurement: float = 0.0         # 测量值
    text_override: str = ""          # 文本覆盖

    # ==== 上下文信息（用于智能翻译） ====
    nearby_entities: List[str] = field(default_factory=list)  # 附近实体类型
    nearby_texts: List[str] = field(default_factory=list)     # 附近文本内容
    text_category: TextCategory = TextCategory.PURE_TEXT      # 文本分类

    # ==== 质量控制 ====
    confidence: float = 0.0          # 翻译置信度（0-1）
    quality_score: float = 0.0       # 翻译质量评分（0-100）
    needs_review: bool = False       # 是否需要人工审查
    warning_message: str = ""        # 警告信息
    review_notes: str = ""           # 审查备注

    # ==== 元数据 ====
    extracted_at: str = ""           # 提取时间
    translated_at: str = ""          # 翻译时间
    reviewed_at: str = ""            # 审查时间
    reviewed_by: str = ""            # 审查人


class TextExtractor:
    """
    智能文本提取器

    功能：
    1. 提取所有类型的文本实体（TEXT, MTEXT, DIMENSION, LEADER等）
    2. 记录完整的实体属性
    3. 收集上下文信息
    4. 分类文本内容
    """

    def __init__(self):
        self.extracted_texts: List[ExtractedText] = []

    def extract_from_file(self, dwg_path: str) -> List[ExtractedText]:
        """
        从DWG文件提取所有文本实体

        Args:
            dwg_path: DWG文件路径

        Returns:
            提取的文本实体列表
        """
        try:
            doc = ezdxf.readfile(dwg_path)
            return self.extract_from_document(doc)
        except Exception as e:
            logger.error(f"读取DWG文件失败: {e}")
            raise

    def extract_from_document(self, doc: Drawing) -> List[ExtractedText]:
        """
        从ezdxf文档对象提取所有文本实体

        Args:
            doc: ezdxf文档对象

        Returns:
            提取的文本实体列表
        """
        self.extracted_texts = []
        modelspace = doc.modelspace()

        logger.info("开始提取文本实体...")

        # 1. 提取TEXT（单行文本）
        text_entities = list(modelspace.query('TEXT'))
        logger.info(f"发现 {len(text_entities)} 个TEXT实体")
        for entity in text_entities:
            extracted = self._extract_text(entity)
            if extracted:
                self.extracted_texts.append(extracted)

        # 2. 提取MTEXT（多行文本）
        mtext_entities = list(modelspace.query('MTEXT'))
        logger.info(f"发现 {len(mtext_entities)} 个MTEXT实体")
        for entity in mtext_entities:
            extracted = self._extract_mtext(entity)
            if extracted:
                self.extracted_texts.append(extracted)

        # 3. 提取DIMENSION（尺寸标注）
        dim_entities = list(modelspace.query('DIMENSION'))
        logger.info(f"发现 {len(dim_entities)} 个DIMENSION实体")
        for entity in dim_entities:
            extracted = self._extract_dimension(entity)
            if extracted:
                self.extracted_texts.append(extracted)

        # 4. 提取LEADER（引线标注）
        leader_entities = list(modelspace.query('LEADER'))
        logger.info(f"发现 {len(leader_entities)} 个LEADER实体")
        for entity in leader_entities:
            extracted = self._extract_leader(entity)
            if extracted:
                self.extracted_texts.append(extracted)

        # 5. 提取MULTILEADER（多重引线）
        try:
            mleader_entities = list(modelspace.query('MULTILEADER'))
            logger.info(f"发现 {len(mleader_entities)} 个MULTILEADER实体")
            for entity in mleader_entities:
                extracted = self._extract_multileader(entity)
                if extracted:
                    self.extracted_texts.append(extracted)
        except Exception as e:
            logger.warning(f"提取MULTILEADER失败（可能版本不支持）: {e}")

        # 6. 提取ATTRIB/ATTDEF（块属性）
        attrib_count = self._extract_block_attributes(doc)
        logger.info(f"发现 {attrib_count} 个块属性")

        # 7. 提取TABLE（表格）
        try:
            table_entities = list(modelspace.query('TABLE'))
            logger.info(f"发现 {len(table_entities)} 个TABLE实体")
            for entity in table_entities:
                extracted_list = self._extract_table(entity)
                self.extracted_texts.extend(extracted_list)
        except Exception as e:
            logger.warning(f"提取TABLE失败: {e}")

        # 收集上下文信息
        self._collect_context_info(modelspace)

        logger.info(f"文本提取完成: 共 {len(self.extracted_texts)} 个文本实体")

        return self.extracted_texts

    def _extract_text(self, entity) -> Optional[ExtractedText]:
        """提取TEXT实体"""
        try:
            text_content = entity.dxf.text if hasattr(entity.dxf, 'text') else ""

            if not text_content.strip():
                return None  # 跳过空文本

            return ExtractedText(
                entity_id=str(entity.dxf.handle),
                entity_type=TextEntityType.TEXT,
                entity_ref=entity,
                original_text=text_content,

                # 位置和尺寸
                position=tuple(entity.dxf.insert) if hasattr(entity.dxf, 'insert') else (0, 0, 0),
                height=entity.dxf.height if hasattr(entity.dxf, 'height') else 0.0,
                rotation=entity.dxf.rotation if hasattr(entity.dxf, 'rotation') else 0.0,
                width_factor=entity.dxf.width if hasattr(entity.dxf, 'width') else 1.0,
                oblique=entity.dxf.oblique if hasattr(entity.dxf, 'oblique') else 0.0,

                # 样式
                style=entity.dxf.style if hasattr(entity.dxf, 'style') else 'Standard',
                layer=entity.dxf.layer if hasattr(entity.dxf, 'layer') else '0',
                color=entity.dxf.color if hasattr(entity.dxf, 'color') else 256,
                linetype=entity.dxf.linetype if hasattr(entity.dxf, 'linetype') else 'ByLayer',
                lineweight=entity.dxf.lineweight if hasattr(entity.dxf, 'lineweight') else -1,

                # 对齐
                halign=entity.dxf.halign if hasattr(entity.dxf, 'halign') else 0,
                valign=entity.dxf.valign if hasattr(entity.dxf, 'valign') else 0,
            )
        except Exception as e:
            logger.warning(f"提取TEXT实体失败: {e}")
            return None

    def _extract_mtext(self, entity) -> Optional[ExtractedText]:
        """提取MTEXT实体（保留所有格式信息）"""
        try:
            # MTEXT的text可能包含格式代码
            text_content = entity.text if hasattr(entity, 'text') else ""

            if not text_content.strip():
                return None

            return ExtractedText(
                entity_id=str(entity.dxf.handle),
                entity_type=TextEntityType.MTEXT,
                entity_ref=entity,
                original_text=text_content,

                # 位置和尺寸
                position=tuple(entity.dxf.insert) if hasattr(entity.dxf, 'insert') else (0, 0, 0),
                height=entity.dxf.char_height if hasattr(entity.dxf, 'char_height') else 0.0,
                rotation=entity.dxf.rotation if hasattr(entity.dxf, 'rotation') else 0.0,
                width_factor=entity.dxf.width if hasattr(entity.dxf, 'width') else 1.0,

                # 样式
                style=entity.dxf.style if hasattr(entity.dxf, 'style') else 'Standard',
                layer=entity.dxf.layer if hasattr(entity.dxf, 'layer') else '0',
                color=entity.dxf.color if hasattr(entity.dxf, 'color') else 256,

                # MTEXT特有属性
                char_height=entity.dxf.char_height if hasattr(entity.dxf, 'char_height') else 0.0,
                line_spacing_style=entity.dxf.line_spacing_style if hasattr(entity.dxf, 'line_spacing_style') else 1,
                line_spacing_factor=entity.dxf.line_spacing_factor if hasattr(entity.dxf, 'line_spacing_factor') else 1.0,
            )
        except Exception as e:
            logger.warning(f"提取MTEXT实体失败: {e}")
            return None

    def _extract_dimension(self, entity) -> Optional[ExtractedText]:
        """
        提取DIMENSION实体

        警告：尺寸标注的文本通常是自动计算的数值，修改需要非常小心！
        """
        try:
            # 尺寸标注的文本可能是覆盖文本或自动计算的
            text_content = ""

            if hasattr(entity.dxf, 'text'):
                text_content = entity.dxf.text
            elif hasattr(entity, 'get_text'):
                text_content = entity.get_text()

            if not text_content:
                return None  # 如果是自动计算的数值，可能不需要翻译

            return ExtractedText(
                entity_id=str(entity.dxf.handle),
                entity_type=TextEntityType.DIMENSION,
                entity_ref=entity,
                original_text=text_content,

                # 基本属性
                layer=entity.dxf.layer if hasattr(entity.dxf, 'layer') else '0',
                color=entity.dxf.color if hasattr(entity.dxf, 'color') else 256,

                # DIMENSION特有
                dim_type=entity.dxf.dimtype if hasattr(entity.dxf, 'dimtype') else 0,
                measurement=entity.get_measurement() if hasattr(entity, 'get_measurement') else 0.0,

                # 标记为高风险，需要审查
                needs_review=True,
                warning_message="尺寸标注文本，修改需谨慎！"
            )
        except Exception as e:
            logger.warning(f"提取DIMENSION实体失败: {e}")
            return None

    def _extract_leader(self, entity) -> Optional[ExtractedText]:
        """提取LEADER实体"""
        try:
            # LEADER可能关联MTEXT或没有文本
            # 这里需要进一步处理
            logger.debug(f"LEADER实体 {entity.dxf.handle} 提取（待完善）")
            return None  # TODO: 完善LEADER提取
        except Exception as e:
            logger.warning(f"提取LEADER实体失败: {e}")
            return None

    def _extract_multileader(self, entity) -> Optional[ExtractedText]:
        """提取MULTILEADER实体"""
        try:
            # MULTILEADER是较新的实体类型
            # 需要根据ezdxf版本进行处理
            logger.debug(f"MULTILEADER实体 {entity.dxf.handle} 提取（待完善）")
            return None  # TODO: 完善MULTILEADER提取
        except Exception as e:
            logger.warning(f"提取MULTILEADER实体失败: {e}")
            return None

    def _extract_block_attributes(self, doc: Drawing) -> int:
        """提取块定义中的属性"""
        count = 0
        try:
            for block in doc.blocks:
                for entity in block:
                    if entity.dxftype() in ['ATTRIB', 'ATTDEF']:
                        extracted = self._extract_attrib(entity)
                        if extracted:
                            self.extracted_texts.append(extracted)
                            count += 1
        except Exception as e:
            logger.warning(f"提取块属性失败: {e}")

        return count

    def _extract_attrib(self, entity) -> Optional[ExtractedText]:
        """提取ATTRIB/ATTDEF实体"""
        try:
            text_content = entity.dxf.text if hasattr(entity.dxf, 'text') else ""

            if not text_content.strip():
                return None

            return ExtractedText(
                entity_id=str(entity.dxf.handle),
                entity_type=TextEntityType.ATTRIB if entity.dxftype() == 'ATTRIB' else TextEntityType.ATTDEF,
                entity_ref=entity,
                original_text=text_content,

                # 基本属性
                position=tuple(entity.dxf.insert) if hasattr(entity.dxf, 'insert') else (0, 0, 0),
                height=entity.dxf.height if hasattr(entity.dxf, 'height') else 0.0,
                layer=entity.dxf.layer if hasattr(entity.dxf, 'layer') else '0',

                # 标记为需要审查（块属性修改可能影响多个引用）
                needs_review=True,
                warning_message="块属性文本，修改会影响所有块引用"
            )
        except Exception as e:
            logger.warning(f"提取ATTRIB实体失败: {e}")
            return None

    def _extract_table(self, entity) -> List[ExtractedText]:
        """提取TABLE实体（表格中的所有单元格）"""
        extracted_list = []
        try:
            # TABLE包含多个单元格，每个单元格可能有文本
            # 这里需要遍历所有单元格
            logger.debug(f"TABLE实体 {entity.dxf.handle} 提取（待完善）")
            # TODO: 完善TABLE提取
        except Exception as e:
            logger.warning(f"提取TABLE实体失败: {e}")

        return extracted_list

    def _collect_context_info(self, modelspace):
        """
        收集上下文信息

        为每个文本实体收集：
        1. 附近的其他实体类型
        2. 附近的其他文本内容
        """
        # TODO: 实现空间邻近查询
        # 可以使用空间索引来快速查找附近的实体
        pass

    def get_statistics(self) -> Dict[str, int]:
        """获取提取统计信息"""
        stats = {
            'total': len(self.extracted_texts),
            'by_type': {},
            'by_category': {},
            'needs_review': 0,
            'empty': 0
        }

        for text in self.extracted_texts:
            # 按类型统计
            type_name = text.entity_type.value
            stats['by_type'][type_name] = stats['by_type'].get(type_name, 0) + 1

            # 按分类统计
            category_name = text.text_category.value
            stats['by_category'][category_name] = stats['by_category'].get(category_name, 0) + 1

            # 需要审查的数量
            if text.needs_review:
                stats['needs_review'] += 1

            # 空文本数量
            if not text.original_text.strip():
                stats['empty'] += 1

        return stats


# 便捷函数
def extract_texts_from_file(dwg_path: str) -> List[ExtractedText]:
    """
    从DWG文件提取所有文本实体的便捷函数

    Args:
        dwg_path: DWG文件路径

    Returns:
        提取的文本实体列表
    """
    extractor = TextExtractor()
    return extractor.extract_from_file(dwg_path)
