"""
精确DWG修改器
保证非破坏性修改，只改文本内容，其他属性完全不变
"""
from dataclasses import dataclass
from typing import List, Dict, Optional
from pathlib import Path
import ezdxf
from ezdxf.document import Drawing

from .text_extractor import ExtractedText, TextEntityType
from ..utils.logger import logger


@dataclass
class ModificationStats:
    """修改统计"""
    total_count: int = 0          # 总数
    success_count: int = 0        # 成功数
    skip_count: int = 0           # 跳过数
    error_count: int = 0          # 错误数
    errors: List[Dict] = None     # 错误详情

    def __post_init__(self):
        if self.errors is None:
            self.errors = []


@dataclass
class ModificationResult:
    """修改结果"""
    success: bool                 # 是否成功
    stats: ModificationStats      # 统计信息
    output_path: str              # 输出文件路径
    backup_path: str = ""         # 备份文件路径
    validation_report: dict = None  # 验证报告

    def __post_init__(self):
        if self.validation_report is None:
            self.validation_report = {}


class PrecisionDWGModifier:
    """
    精确DWG修改器

    核心原则：
    1. ✅ 只修改 entity.dxf.text 属性（或equivalent）
    2. ❌ 不创建新实体
    3. ❌ 不删除实体
    4. ❌ 不改变任何其他属性
    5. ❌ 不改变实体顺序
    6. ✅ 保持文件结构完整性
    """

    def __init__(self):
        self.stats = ModificationStats()

    def modify_file(
        self,
        input_path: str,
        translations: List[ExtractedText],
        output_path: str,
        create_backup: bool = True
    ) -> ModificationResult:
        """
        修改DWG文件

        Args:
            input_path: 输入DWG文件路径
            translations: 翻译结果列表
            output_path: 输出DWG文件路径
            create_backup: 是否创建备份

        Returns:
            修改结果
        """
        input_path = Path(input_path)
        output_path = Path(output_path)

        # 1. 创建备份
        backup_path = ""
        if create_backup:
            backup_path = self._create_backup(input_path)
            logger.info(f"已创建备份: {backup_path}")

        try:
            # 2. 读取DWG文件
            logger.info(f"读取DWG文件: {input_path}")
            doc = ezdxf.readfile(str(input_path))

            # 3. 执行修改
            self._modify_document(doc, translations)

            # 4. 保存修改后的文件
            logger.info(f"保存修改后的文件: {output_path}")
            doc.saveas(str(output_path))

            # 5. 验证修改结果（可选，耗时）
            # validation_report = self._validate_modification(input_path, output_path)

            # 6. 返回结果
            return ModificationResult(
                success=True,
                stats=self.stats,
                output_path=str(output_path),
                backup_path=backup_path
            )

        except Exception as e:
            logger.error(f"DWG修改失败: {e}", exc_info=True)
            return ModificationResult(
                success=False,
                stats=self.stats,
                output_path=str(output_path),
                backup_path=backup_path
            )

    def _modify_document(self, doc: Drawing, translations: List[ExtractedText]):
        """
        修改ezdxf文档对象

        Args:
            doc: ezdxf文档对象
            translations: 翻译结果列表
        """
        self.stats = ModificationStats()
        self.stats.total_count = len(translations)

        logger.info(f"开始修改 {len(translations)} 个文本实体...")

        for trans in translations:
            # 跳过没有翻译的
            if not trans.translated_text:
                self.stats.skip_count += 1
                continue

            # 跳过原文和译文相同的
            if trans.original_text == trans.translated_text:
                self.stats.skip_count += 1
                continue

            try:
                # 根据实体类型调用不同的修改方法
                if trans.entity_type == TextEntityType.TEXT:
                    self._modify_text(trans)

                elif trans.entity_type == TextEntityType.MTEXT:
                    self._modify_mtext(trans)

                elif trans.entity_type == TextEntityType.DIMENSION:
                    self._modify_dimension(trans)

                elif trans.entity_type == TextEntityType.LEADER:
                    self._modify_leader(trans)

                elif trans.entity_type == TextEntityType.MULTILEADER:
                    self._modify_multileader(trans)

                elif trans.entity_type in [TextEntityType.ATTRIB, TextEntityType.ATTDEF]:
                    self._modify_attrib(trans)

                elif trans.entity_type == TextEntityType.TABLE:
                    self._modify_table(trans)

                else:
                    logger.warning(f"不支持的实体类型: {trans.entity_type}")
                    self.stats.skip_count += 1
                    continue

                self.stats.success_count += 1

            except Exception as e:
                self.stats.error_count += 1
                self.stats.errors.append({
                    'entity_id': trans.entity_id,
                    'entity_type': trans.entity_type.value,
                    'original_text': trans.original_text,
                    'translated_text': trans.translated_text,
                    'error': str(e)
                })
                logger.error(f"修改实体失败 [{trans.entity_id}]: {e}")

        logger.info(
            f"修改完成: 成功={self.stats.success_count}, "
            f"跳过={self.stats.skip_count}, "
            f"失败={self.stats.error_count}"
        )

    def _modify_text(self, trans: ExtractedText):
        """
        修改TEXT实体

        这是最简单和最安全的修改
        """
        entity = trans.entity_ref

        # ✅ 只修改这一行！
        entity.dxf.text = trans.translated_text

        logger.debug(
            f"TEXT修改: '{trans.original_text}' → '{trans.translated_text}'"
        )

    def _modify_mtext(self, trans: ExtractedText):
        """
        修改MTEXT实体

        关键：必须保持所有格式标记（\\f, \\P, \\C等）
        """
        entity = trans.entity_ref

        # ✅ 只修改text属性
        # 注意：MTEXT使用 .text 属性，不是 .dxf.text
        if hasattr(entity, 'text'):
            entity.text = trans.translated_text
        elif hasattr(entity.dxf, 'text'):
            entity.dxf.text = trans.translated_text
        else:
            raise AttributeError("MTEXT实体没有text属性")

        logger.debug(
            f"MTEXT修改: '{trans.original_text[:50]}...' → "
            f"'{trans.translated_text[:50]}...'"
        )

    def _modify_dimension(self, trans: ExtractedText):
        """
        修改DIMENSION实体

        ⚠️ 警告：这是高风险操作！
        尺寸标注的文本通常是自动计算的数值，不应该修改。
        只有当用户明确设置了覆盖文本时才应该修改。
        """
        entity = trans.entity_ref

        # 检查是否有覆盖文本
        if hasattr(entity.dxf, 'text'):
            # ✅ 只修改覆盖文本
            entity.dxf.text = trans.translated_text
            logger.warning(
                f"DIMENSION覆盖文本修改: '{trans.original_text}' → "
                f"'{trans.translated_text}' (请人工审查！)"
            )
        else:
            # ❌ 不应该修改自动计算的尺寸数值
            logger.error(
                f"DIMENSION实体 {trans.entity_id} 没有覆盖文本，"
                f"不应修改自动计算的数值！"
            )
            raise ValueError("不能修改DIMENSION的自动计算数值")

    def _modify_leader(self, trans: ExtractedText):
        """
        修改LEADER实体

        LEADER通常关联MTEXT，需要找到关联的文本实体
        """
        entity = trans.entity_ref

        # TODO: 完善LEADER修改逻辑
        logger.warning(f"LEADER修改功能待完善: {trans.entity_id}")

    def _modify_multileader(self, trans: ExtractedText):
        """
        修改MULTILEADER实体

        较新的实体类型，处理方式可能因ezdxf版本而异
        """
        entity = trans.entity_ref

        # TODO: 完善MULTILEADER修改逻辑
        logger.warning(f"MULTILEADER修改功能待完善: {trans.entity_id}")

    def _modify_attrib(self, trans: ExtractedText):
        """
        修改ATTRIB/ATTDEF实体

        ⚠️ 警告：块属性的修改会影响所有使用该块的实例！
        """
        entity = trans.entity_ref

        # ✅ 修改text属性
        entity.dxf.text = trans.translated_text

        logger.warning(
            f"块属性修改: '{trans.original_text}' → '{trans.translated_text}' "
            f"(会影响所有块引用，请人工审查！)"
        )

    def _modify_table(self, trans: ExtractedText):
        """
        修改TABLE实体中的单元格

        TABLE是复杂的实体，需要定位到具体的单元格
        """
        entity = trans.entity_ref

        # TODO: 完善TABLE修改逻辑
        logger.warning(f"TABLE修改功能待完善: {trans.entity_id}")

    def _create_backup(self, file_path: Path) -> str:
        """
        创建备份文件

        Args:
            file_path: 原文件路径

        Returns:
            备份文件路径
        """
        import shutil
        from datetime import datetime

        # 生成备份文件名：原文件名.backup.yyyymmdd_hhmmss.dwg
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        backup_path = file_path.parent / f"{file_path.stem}.backup.{timestamp}{file_path.suffix}"

        shutil.copy2(file_path, backup_path)

        return str(backup_path)

    def _validate_modification(
        self,
        original_path: Path,
        modified_path: Path
    ) -> dict:
        """
        验证修改结果（可选，耗时）

        检查：
        1. 文件可以正常打开
        2. 实体数量一致
        3. 非文本实体未被修改
        """
        report = {
            'valid': True,
            'errors': [],
            'warnings': []
        }

        try:
            # 读取两个文件
            original_doc = ezdxf.readfile(str(original_path))
            modified_doc = ezdxf.readfile(str(modified_path))

            # 检查实体数量
            original_count = len(list(original_doc.modelspace()))
            modified_count = len(list(modified_doc.modelspace()))

            if original_count != modified_count:
                report['valid'] = False
                report['errors'].append(
                    f"实体数量不一致: {original_count} vs {modified_count}"
                )

            # 更多验证...
            # TODO: 实现更详细的验证

        except Exception as e:
            report['valid'] = False
            report['errors'].append(f"验证失败: {str(e)}")

        return report


class BatchModifier:
    """
    批量修改器

    支持批量处理多个DWG文件
    """

    def __init__(self):
        self.modifier = PrecisionDWGModifier()
        self.results: List[ModificationResult] = []

    def modify_batch(
        self,
        file_translations: List[Tuple[str, List[ExtractedText], str]],
        create_backup: bool = True
    ) -> List[ModificationResult]:
        """
        批量修改DWG文件

        Args:
            file_translations: [(输入路径, 翻译列表, 输出路径), ...]
            create_backup: 是否创建备份

        Returns:
            修改结果列表
        """
        self.results = []

        for input_path, translations, output_path in file_translations:
            logger.info(f"处理文件: {input_path}")

            result = self.modifier.modify_file(
                input_path,
                translations,
                output_path,
                create_backup
            )

            self.results.append(result)

        return self.results

    def get_summary(self) -> dict:
        """获取批量处理摘要"""
        total_files = len(self.results)
        success_files = sum(1 for r in self.results if r.success)
        failed_files = total_files - success_files

        total_texts = sum(r.stats.total_count for r in self.results)
        success_texts = sum(r.stats.success_count for r in self.results)
        error_texts = sum(r.stats.error_count for r in self.results)

        return {
            'total_files': total_files,
            'success_files': success_files,
            'failed_files': failed_files,
            'total_texts': total_texts,
            'success_texts': success_texts,
            'error_texts': error_texts,
            'success_rate': f"{success_texts*100/max(total_texts,1):.1f}%"
        }


# 便捷函数
def modify_dwg_file(
    input_path: str,
    translations: List[ExtractedText],
    output_path: str,
    create_backup: bool = True
) -> ModificationResult:
    """
    修改DWG文件的便捷函数

    Args:
        input_path: 输入DWG文件路径
        translations: 翻译结果列表
        output_path: 输出DWG文件路径
        create_backup: 是否创建备份

    Returns:
        修改结果
    """
    modifier = PrecisionDWGModifier()
    return modifier.modify_file(input_path, translations, output_path, create_backup)
