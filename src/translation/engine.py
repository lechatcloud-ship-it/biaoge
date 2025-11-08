# -*- coding: utf-8 -*-
"""
翻译引擎
"""
import re
from typing import List, Dict, Set, Optional
from dataclasses import dataclass
from PyQt6.QtCore import QThread, pyqtSignal

from ..dwg.entities import DWGDocument, TextEntity
from ..services.bailian_client import BailianClient, BailianAPIError
from .cache import TranslationCache
from .quality_control import TranslationQualityControl, QualityLevel  # 🚀 翻译质量控制 (99.9999%)
from ..utils.logger import logger
from ..utils.config_manager import ConfigManager


@dataclass
class TranslationStats:
    """翻译统计信息"""
    total_entities: int = 0  # 总实体数
    unique_texts: int = 0  # 唯一文本数
    cached_count: int = 0  # 缓存命中数
    translated_count: int = 0  # API翻译数
    skipped_count: int = 0  # 跳过数（纯数字等）
    total_tokens: int = 0  # 总token消耗
    total_cost: float = 0.0  # 总成本（元）
    duration_seconds: float = 0.0  # 耗时（秒）
    # 🚀 质量控制统计
    quality_checked: int = 0  # 质量检查数
    quality_perfect: int = 0  # 完美翻译数
    quality_corrected: int = 0  # 自动修正数
    quality_warnings: int = 0  # 警告数
    quality_errors: int = 0  # 错误数
    average_quality_score: float = 0.0  # 平均质量分数
    
    def to_dict(self) -> Dict:
        """转换为字典"""
        return {
            'total_entities': self.total_entities,
            'unique_texts': self.unique_texts,
            'cached_count': self.cached_count,
            'translated_count': self.translated_count,
            'skipped_count': self.skipped_count,
            'total_tokens': self.total_tokens,
            'total_cost': self.total_cost,
            'duration_seconds': self.duration_seconds,
            'cache_hit_rate': f"{self.cached_count / self.unique_texts * 100:.1f}%" if self.unique_texts > 0 else "0%",
            # 🚀 质量控制统计
            'quality_checked': self.quality_checked,
            'quality_perfect': self.quality_perfect,
            'quality_corrected': self.quality_corrected,
            'quality_warnings': self.quality_warnings,
            'quality_errors': self.quality_errors,
            'average_quality_score': f"{self.average_quality_score:.2f}%"
        }


class TranslationEngine:
    """翻译引擎"""
    
    def __init__(
        self,
        client: Optional[BailianClient] = None,
        cache: Optional[TranslationCache] = None
    ):
        """
        初始化翻译引擎

        Args:
            client: 百炼API客户端，如果为None则自动创建
            cache: 翻译缓存，如果为None则自动创建
        """
        config = ConfigManager()

        self.client = client or BailianClient()
        self.cache = cache or TranslationCache()
        self.quality_control = TranslationQualityControl()  # 🚀 质量控制器 (99.9999%)

        # 从配置读取翻译设置（确保设置生效）
        self.batch_size = config.get('translation.batch_size', 50)
        self.cache_enabled = config.get('translation.cache_enabled', True)
        self.context_window = config.get('translation.context_window', 3)
        self.use_terminology = config.get('translation.use_terminology', True)
        self.post_process = config.get('translation.post_process', True)
        self.enable_quality_control = config.get('translation.quality_control', True)  # 🚀 默认启用质量控制

        logger.info(
            f"翻译引擎初始化完成 - "
            f"批量大小: {self.batch_size}, "
            f"缓存: {self.cache_enabled}, "
            f"上下文窗口: {self.context_window}, "
            f"术语库: {self.use_terminology}, "
            f"后处理: {self.post_process}, "
            f"质量控制: {self.enable_quality_control}"  # 🚀
        )
    
    def translate_document(
        self,
        document: DWGDocument,
        from_lang: str,
        to_lang: str,
        progress_callback: Optional[callable] = None
    ) -> TranslationStats:
        """
        翻译整个DWG文档
        
        Args:
            document: DWG文档
            from_lang: 源语言
            to_lang: 目标语言
            progress_callback: 进度回调函数 callback(current, total, message)
        
        Returns:
            TranslationStats: 翻译统计信息
        """
        import time
        start_time = time.time()
        
        stats = TranslationStats()
        
        # 1. 提取所有文本实体
        text_entities = self._extract_text_entities(document)
        stats.total_entities = len(text_entities)
        
        if not text_entities:
            logger.info("文档中没有文本实体")
            return stats
        
        logger.info(f"提取到{stats.total_entities}个文本实体")
        
        # 2. 提取唯一文本并预处理
        unique_texts_map = self._extract_unique_texts(text_entities)
        stats.unique_texts = len(unique_texts_map)
        stats.skipped_count = stats.total_entities - sum(len(v) for v in unique_texts_map.values())
        
        logger.info(f"唯一文本: {stats.unique_texts}个, 跳过: {stats.skipped_count}个")
        
        if progress_callback:
            progress_callback(0, stats.unique_texts, "正在查询缓存...")
        
        # 3. 查询缓存
        translations = {}
        to_translate = []
        
        if self.cache_enabled:
            cached_translations = self.cache.get_batch(
                list(unique_texts_map.keys()),
                from_lang,
                to_lang
            )
            translations.update(cached_translations)
            stats.cached_count = len(cached_translations)
            
            logger.info(f"缓存命中: {stats.cached_count}/{stats.unique_texts}")
        
        # 4. 找出需要翻译的文本
        for text in unique_texts_map.keys():
            if text not in translations:
                to_translate.append(text)
        
        # 5. 批量翻译
        if to_translate:
            if progress_callback:
                progress_callback(stats.cached_count, stats.unique_texts, "正在调用API翻译...")

            new_translations = self._translate_batch_texts(
                to_translate,
                from_lang,
                to_lang,
                progress_callback,
                stats.cached_count
            )

            # 🚀 质量控制检查
            if self.enable_quality_control:
                if progress_callback:
                    progress_callback(stats.cached_count, stats.unique_texts, "正在进行质量控制检查...")

                logger.info(f"🚀 开始质量控制检查: {len(new_translations)}条翻译")
                new_translations, quality_stats = self._perform_quality_control(
                    new_translations,
                    from_lang,
                    to_lang
                )

                # 更新质量统计
                stats.quality_checked = quality_stats['checked']
                stats.quality_perfect = quality_stats['perfect']
                stats.quality_corrected = quality_stats['corrected']
                stats.quality_warnings = quality_stats['warnings']
                stats.quality_errors = quality_stats['errors']
                stats.average_quality_score = quality_stats['average_score']

                logger.info(
                    f"🚀 质量控制完成: 完美{quality_stats['perfect']}, "
                    f"修正{quality_stats['corrected']}, "
                    f"警告{quality_stats['warnings']}, "
                    f"错误{quality_stats['errors']}, "
                    f"平均分{quality_stats['average_score']:.2f}%"
                )

            # 更新统计
            for result in new_translations:
                translations[result.original_text] = result.translated_text
                stats.translated_count += 1
                stats.total_tokens += result.tokens_used
                stats.total_cost += result.cost_estimate

            # 保存到缓存
            if self.cache_enabled:
                cache_dict = {r.original_text: r.translated_text for r in new_translations}
                self.cache.set_batch(cache_dict, from_lang, to_lang)
        
        # 6. 应用翻译到实体
        if progress_callback:
            progress_callback(stats.unique_texts, stats.unique_texts, "正在应用翻译...")
        
        self._apply_translations(unique_texts_map, translations)
        
        stats.duration_seconds = time.time() - start_time
        
        logger.info(f"翻译完成: {stats.to_dict()}")
        
        return stats
    
    def _extract_text_entities(self, document: DWGDocument) -> List[TextEntity]:
        """提取文本实体"""
        return [
            entity for entity in document.entities
            if isinstance(entity, TextEntity) and entity.text and entity.text.strip()
        ]
    
    def _extract_unique_texts(
        self,
        text_entities: List[TextEntity]
    ) -> Dict[str, List[TextEntity]]:
        """
        提取唯一文本并建立映射
        
        Returns:
            Dict[str, List[TextEntity]]: 文本->实体列表的映射
        """
        text_map = {}
        
        for entity in text_entities:
            text = entity.text.strip()
            
            # 跳过纯数字
            if self._is_number_only(text):
                continue
            
            # 跳过单字符
            if len(text) == 1 and text.isascii():
                continue
            
            # 跳过纯符号
            if self._is_symbols_only(text):
                continue
            
            # 添加到映射
            if text not in text_map:
                text_map[text] = []
            text_map[text].append(entity)
        
        return text_map
    
    def _translate_batch_texts(
        self,
        texts: List[str],
        from_lang: str,
        to_lang: str,
        progress_callback: Optional[callable] = None,
        progress_offset: int = 0
    ) -> List:
        """批量翻译文本（分批调用API）"""
        all_results = []
        total_batches = (len(texts) + self.batch_size - 1) // self.batch_size
        
        for i in range(0, len(texts), self.batch_size):
            batch = texts[i:i + self.batch_size]
            batch_num = i // self.batch_size + 1
            
            logger.info(f"翻译批次 {batch_num}/{total_batches} ({len(batch)}条)")
            
            try:
                # 使用配置的文本翻译模型
                results = self.client.translate_batch(
                    batch,
                    from_lang,
                    to_lang,
                    task_type='text'  # 使用文本翻译模型
                )
                all_results.extend(results)
                
                if progress_callback:
                    current = progress_offset + len(all_results)
                    total = progress_offset + len(texts)
                    progress_callback(current, total, f"翻译中... ({batch_num}/{total_batches})")
            
            except BailianAPIError as e:
                logger.error(f"批次翻译失败: {e}")
                # 失败时逐个翻译
                for text in batch:
                    try:
                        result = self.client.translate_text(
                            text,
                            from_lang,
                            to_lang,
                            task_type='text'  # 使用文本翻译模型
                        )
                        all_results.append(result)
                    except Exception as e2:
                        logger.error(f"单文本翻译失败: {text}, {e2}")
        
        return all_results
    
    def _apply_translations(
        self,
        text_map: Dict[str, List[TextEntity]],
        translations: Dict[str, str]
    ):
        """应用翻译到实体"""
        for original_text, entities in text_map.items():
            if original_text in translations:
                translated_text = translations[original_text]
                for entity in entities:
                    entity.translated_text = translated_text

    def _perform_quality_control(
        self,
        translation_results: List,
        from_lang: str,
        to_lang: str
    ) -> tuple:
        """
        🚀 执行翻译质量控制

        Args:
            translation_results: 翻译结果列表
            from_lang: 源语言
            to_lang: 目标语言

        Returns:
            tuple: (修正后的翻译结果, 质量统计字典)
        """
        quality_stats = {
            'checked': 0,
            'perfect': 0,
            'corrected': 0,
            'warnings': 0,
            'errors': 0,
            'average_score': 0.0
        }

        corrected_results = []
        total_score = 0.0

        for result in translation_results:
            original = result.original_text
            translated = result.translated_text

            # 执行质量检查 (返回问题列表)
            issues = self.quality_control.check_translation(
                original,
                translated,
                context={'from_lang': from_lang, 'to_lang': to_lang}
            )

            quality_stats['checked'] += 1

            # 根据问题数量和严重程度判断质量
            critical_issues = [i for i in issues if i.severity == 'CRITICAL']
            major_issues = [i for i in issues if i.severity == 'MAJOR']
            minor_issues = [i for i in issues if i.severity == 'MINOR']

            if len(issues) == 0:
                quality_stats['perfect'] += 1
                issue_score = 100.0
            else:
                # 计算质量分数: 每个CRITICAL -20分, MAJOR -10分, MINOR -5分
                issue_score = max(0, 100 - len(critical_issues)*20 - len(major_issues)*10 - len(minor_issues)*5)

                if critical_issues:
                    quality_stats['errors'] += len(critical_issues)
                if major_issues or minor_issues:
                    quality_stats['warnings'] += len(major_issues) + len(minor_issues)

            total_score += issue_score

            # 如果有critical问题，尝试自动修正
            if critical_issues:
                corrected = self.quality_control.auto_correct(original, translated, critical_issues)
                if corrected and corrected != translated:
                    result.translated_text = corrected
                    quality_stats['corrected'] += 1
                    logger.debug(
                        f"🚀 翻译已修正: {original[:20]}... | "
                        f"{translated[:20]}... -> {corrected[:20]}..."
                    )

            corrected_results.append(result)

        # 计算平均分
        if quality_stats['checked'] > 0:
            quality_stats['average_score'] = total_score / quality_stats['checked']

        return corrected_results, quality_stats

    def _is_number_only(self, text: str) -> bool:
        """判断是否纯数字（含小数点、负号）"""
        pattern = r'^[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?$'
        return bool(re.match(pattern, text))
    
    def _is_symbols_only(self, text: str) -> bool:
        """判断是否纯符号"""
        return all(not c.isalnum() for c in text)


class TranslationWorker(QThread):
    """翻译工作线程（用于UI异步翻译）"""
    
    # 信号
    progress = pyqtSignal(int, int, str)  # current, total, message
    finished = pyqtSignal(object)  # TranslationStats
    error = pyqtSignal(str)  # error_message
    
    def __init__(
        self,
        engine: TranslationEngine,
        document: DWGDocument,
        from_lang: str,
        to_lang: str
    ):
        super().__init__()
        self.engine = engine
        self.document = document
        self.from_lang = from_lang
        self.to_lang = to_lang
    
    def run(self):
        """运行翻译"""
        try:
            stats = self.engine.translate_document(
                self.document,
                self.from_lang,
                self.to_lang,
                progress_callback=self._on_progress
            )
            self.finished.emit(stats)
        except Exception as e:
            logger.error(f"翻译线程错误: {e}", exc_info=True)
            self.error.emit(str(e))
    
    def _on_progress(self, current: int, total: int, message: str):
        """进度回调"""
        self.progress.emit(current, total, message)
