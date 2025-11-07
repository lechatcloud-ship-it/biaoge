"""
DWG翻译完整流程管道
将提取、分类、翻译、修改、验证集成为一体
"""
from dataclasses import dataclass
from typing import List, Optional, Dict
from pathlib import Path
from datetime import datetime

from .text_extractor import TextExtractor, ExtractedText
from .text_classifier import TextClassifier
from .smart_translator import SmartTranslator
from .precision_modifier import PrecisionDWGModifier, ModificationResult
from ..utils.logger import logger
from ..utils.config_manager import ConfigManager


@dataclass
class PipelineConfig:
    """流程配置"""
    # API配置
    api_key: str = ""

    # 翻译配置
    source_language: str = "auto"       # 源语言（自动检测）
    target_language: str = "Chinese"    # 目标语言（默认简体中文）
    use_terminology: bool = True
    use_memory: bool = True

    # 修改配置
    create_backup: bool = True
    validate_result: bool = False  # 是否验证结果（耗时）

    # 输出配置
    output_dir: Optional[str] = None  # 输出目录（None=与输入同目录）
    output_suffix: str = "_translated"  # 输出文件后缀


@dataclass
class PipelineResult:
    """流程结果"""
    success: bool                      # 是否成功
    input_path: str                    # 输入文件路径
    output_path: str                   # 输出文件路径
    backup_path: str                   # 备份文件路径

    # 统计信息
    total_texts: int = 0               # 总文本数
    translated_texts: int = 0          # 翻译的文本数
    skipped_texts: int = 0             # 跳过的文本数
    failed_texts: int = 0              # 失败的文本数

    # 分类统计
    classification_stats: Dict = None  # 文本分类统计

    # 质量信息
    average_confidence: float = 0.0    # 平均置信度
    needs_review_count: int = 0        # 需要审查的数量

    # 时间信息
    extraction_time: float = 0.0       # 提取耗时（秒）
    translation_time: float = 0.0      # 翻译耗时（秒）
    modification_time: float = 0.0     # 修改耗时（秒）
    total_time: float = 0.0            # 总耗时（秒）

    # 详细信息
    extracted_texts: List[ExtractedText] = None  # 提取的文本
    warnings: List[str] = None         # 警告信息
    errors: List[str] = None           # 错误信息

    def __post_init__(self):
        if self.classification_stats is None:
            self.classification_stats = {}
        if self.extracted_texts is None:
            self.extracted_texts = []
        if self.warnings is None:
            self.warnings = []
        if self.errors is None:
            self.errors = []


class TranslationPipeline:
    """
    DWG翻译流程管道

    完整流程：
    1. 提取文本 (TextExtractor)
    2. 分类文本 (TextClassifier)
    3. 智能翻译 (SmartTranslator)
    4. 精确修改 (PrecisionDWGModifier)
    5. 验证结果（可选）
    6. 生成报告
    """

    def __init__(self, config: Optional[PipelineConfig] = None):
        """
        初始化流程管道

        Args:
            config: 流程配置（None则从ConfigManager读取）
        """
        self.app_config = ConfigManager()

        # 使用传入的配置或从ConfigManager读取
        if config:
            self.config = config
        else:
            self.config = self._load_config_from_manager()

        # 初始化各个组件
        self.extractor = TextExtractor()
        self.classifier = TextClassifier()
        self.translator = SmartTranslator(self.config.api_key)
        self.modifier = PrecisionDWGModifier()

        logger.info("翻译流程管道初始化完成")

    def _load_config_from_manager(self) -> PipelineConfig:
        """从ConfigManager加载配置"""
        return PipelineConfig(
            api_key=self.app_config.get('api.api_key', ''),
            source_language=self.app_config.get('translation.default_source_lang', 'auto'),
            target_language=self.app_config.get('translation.default_target_lang', 'zh-CN'),
            use_terminology=self.app_config.get('translation.use_terminology', True),
            use_memory=self.app_config.get('translation.cache_enabled', True),
            create_backup=True,
            validate_result=False
        )

    def process_file(
        self,
        input_path: str,
        output_path: Optional[str] = None
    ) -> PipelineResult:
        """
        处理单个DWG文件

        Args:
            input_path: 输入DWG文件路径
            output_path: 输出DWG文件路径（None则自动生成）

        Returns:
            流程结果
        """
        input_path = Path(input_path)

        # 确定输出路径
        if output_path is None:
            if self.config.output_dir:
                output_dir = Path(self.config.output_dir)
                output_dir.mkdir(parents=True, exist_ok=True)
                output_path = output_dir / f"{input_path.stem}{self.config.output_suffix}{input_path.suffix}"
            else:
                output_path = input_path.parent / f"{input_path.stem}{self.config.output_suffix}{input_path.suffix}"
        else:
            output_path = Path(output_path)

        # 创建结果对象
        result = PipelineResult(
            success=False,
            input_path=str(input_path),
            output_path=str(output_path),
            backup_path=""
        )

        start_time = datetime.now()

        try:
            logger.info("="*60)
            logger.info(f"开始处理DWG文件: {input_path.name}")
            logger.info("="*60)

            # ========== 阶段1：提取文本 ==========
            logger.info("\n【阶段1/4】提取文本实体...")
            extract_start = datetime.now()

            extracted_texts = self.extractor.extract_from_file(str(input_path))
            result.extracted_texts = extracted_texts
            result.total_texts = len(extracted_texts)

            extract_time = (datetime.now() - extract_start).total_seconds()
            result.extraction_time = extract_time

            logger.info(f"✓ 提取完成: {len(extracted_texts)} 个文本实体 ({extract_time:.2f}秒)")

            # 获取提取统计
            extract_stats = self.extractor.get_statistics()
            logger.info(f"  按类型: {extract_stats['by_type']}")

            if len(extracted_texts) == 0:
                logger.warning("未提取到任何文本实体，跳过翻译")
                result.warnings.append("文件中没有可翻译的文本")
                return result

            # ========== 阶段2：分类文本 ==========
            logger.info("\n【阶段2/4】分类文本...")

            extracted_texts = self.classifier.classify_batch(extracted_texts)
            result.classification_stats = self.classifier.get_statistics()

            logger.info(f"✓ 分类完成: {result.classification_stats}")

            # ========== 阶段3：智能翻译 ==========
            logger.info("\n【阶段3/4】智能翻译...")
            translate_start = datetime.now()

            extracted_texts = self.translator.translate_texts(
                extracted_texts,
                use_terminology=self.config.use_terminology,
                use_memory=self.config.use_memory
            )

            translate_time = (datetime.now() - translate_start).total_seconds()
            result.translation_time = translate_time

            # 统计翻译结果
            translated_count = sum(1 for t in extracted_texts if t.translated_text and t.translated_text != t.original_text)
            skipped_count = sum(1 for t in extracted_texts if not t.translated_text or t.translated_text == t.original_text)
            needs_review_count = sum(1 for t in extracted_texts if t.needs_review)

            result.translated_texts = translated_count
            result.skipped_texts = skipped_count
            result.needs_review_count = needs_review_count

            # 计算平均置信度
            confidences = [t.confidence for t in extracted_texts if t.confidence > 0]
            if confidences:
                result.average_confidence = sum(confidences) / len(confidences)

            logger.info(f"✓ 翻译完成: {translated_count} 个文本 ({translate_time:.2f}秒)")
            logger.info(f"  跳过: {skipped_count}, 需要审查: {needs_review_count}")
            logger.info(f"  平均置信度: {result.average_confidence:.2%}")

            # 收集警告
            for text in extracted_texts:
                if text.warning_message:
                    result.warnings.append(f"[{text.entity_id}] {text.warning_message}")

            # ========== 阶段4：精确修改 ==========
            logger.info("\n【阶段4/4】修改DWG文件...")
            modify_start = datetime.now()

            mod_result = self.modifier.modify_file(
                str(input_path),
                extracted_texts,
                str(output_path),
                create_backup=self.config.create_backup
            )

            modify_time = (datetime.now() - modify_start).total_seconds()
            result.modification_time = modify_time

            result.backup_path = mod_result.backup_path
            result.failed_texts = mod_result.stats.error_count

            logger.info(f"✓ 修改完成 ({modify_time:.2f}秒)")
            logger.info(f"  成功: {mod_result.stats.success_count}, 失败: {mod_result.stats.error_count}")

            if mod_result.backup_path:
                logger.info(f"  备份: {mod_result.backup_path}")

            # 收集错误
            for error in mod_result.stats.errors:
                result.errors.append(
                    f"[{error['entity_id']}] {error['original_text']} → {error['error']}"
                )

            # ========== 总结 ==========
            end_time = datetime.now()
            result.total_time = (end_time - start_time).total_seconds()
            result.success = mod_result.success

            logger.info("\n" + "="*60)
            logger.info("处理完成")
            logger.info("="*60)
            logger.info(f"输入文件: {input_path}")
            logger.info(f"输出文件: {output_path}")
            logger.info(f"总计: {result.total_texts} 个文本")
            logger.info(f"翻译: {result.translated_texts} 个")
            logger.info(f"跳过: {result.skipped_texts} 个")
            logger.info(f"失败: {result.failed_texts} 个")
            logger.info(f"需审查: {result.needs_review_count} 个")
            logger.info(f"总耗时: {result.total_time:.2f} 秒")
            logger.info(f"  提取: {result.extraction_time:.2f}s")
            logger.info(f"  翻译: {result.translation_time:.2f}s")
            logger.info(f"  修改: {result.modification_time:.2f}s")

            if result.warnings:
                logger.warning(f"警告数: {len(result.warnings)}")
            if result.errors:
                logger.error(f"错误数: {len(result.errors)}")

            logger.info("="*60)

            return result

        except Exception as e:
            logger.error(f"处理失败: {e}", exc_info=True)
            result.success = False
            result.errors.append(str(e))
            result.total_time = (datetime.now() - start_time).total_seconds()
            return result

    def process_batch(
        self,
        input_paths: List[str],
        output_paths: Optional[List[str]] = None
    ) -> List[PipelineResult]:
        """
        批量处理多个DWG文件

        Args:
            input_paths: 输入文件路径列表
            output_paths: 输出文件路径列表（None则自动生成）

        Returns:
            流程结果列表
        """
        results = []

        for i, input_path in enumerate(input_paths):
            output_path = output_paths[i] if output_paths and i < len(output_paths) else None

            result = self.process_file(input_path, output_path)
            results.append(result)

        # 输出批量处理摘要
        self._print_batch_summary(results)

        return results

    def _print_batch_summary(self, results: List[PipelineResult]):
        """打印批量处理摘要"""
        logger.info("\n" + "="*60)
        logger.info("批量处理摘要")
        logger.info("="*60)

        total_files = len(results)
        success_files = sum(1 for r in results if r.success)
        failed_files = total_files - success_files

        total_texts = sum(r.total_texts for r in results)
        translated_texts = sum(r.translated_texts for r in results)
        failed_texts = sum(r.failed_texts for r in results)
        needs_review = sum(r.needs_review_count for r in results)

        total_time = sum(r.total_time for r in results)

        logger.info(f"文件总数: {total_files}")
        logger.info(f"  成功: {success_files}")
        logger.info(f"  失败: {failed_files}")
        logger.info(f"文本总数: {total_texts}")
        logger.info(f"  翻译: {translated_texts}")
        logger.info(f"  失败: {failed_texts}")
        logger.info(f"  需审查: {needs_review}")
        logger.info(f"总耗时: {total_time:.2f} 秒")
        logger.info(f"平均每个文件: {total_time/max(total_files,1):.2f} 秒")
        logger.info("="*60)


# 便捷函数
def translate_dwg_file(
    input_path: str,
    output_path: Optional[str] = None,
    api_key: Optional[str] = None,
    source_lang: str = "Chinese",
    target_lang: str = "English"
) -> PipelineResult:
    """
    翻译DWG文件的便捷函数

    Args:
        input_path: 输入DWG文件路径
        output_path: 输出DWG文件路径（None则自动生成）
        api_key: API密钥（None则从配置读取）
        source_lang: 源语言
        target_lang: 目标语言

    Returns:
        流程结果
    """
    config = PipelineConfig(
        api_key=api_key or "",
        source_language=source_lang,
        target_language=target_lang
    )

    pipeline = TranslationPipeline(config)
    return pipeline.process_file(input_path, output_path)


def translate_dwg_files(
    input_paths: List[str],
    output_paths: Optional[List[str]] = None,
    api_key: Optional[str] = None
) -> List[PipelineResult]:
    """
    批量翻译DWG文件的便捷函数

    Args:
        input_paths: 输入文件路径列表
        output_paths: 输出文件路径列表（None则自动生成）
        api_key: API密钥

    Returns:
        流程结果列表
    """
    config = PipelineConfig(api_key=api_key or "")

    pipeline = TranslationPipeline(config)
    return pipeline.process_batch(input_paths, output_paths)
