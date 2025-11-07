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
            'cache_hit_rate': f"{self.cached_count / self.unique_texts * 100:.1f}%" if self.unique_texts > 0 else "0%"
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
        
        self.batch_size = config.get('translation.batch_size', 50)
        self.cache_enabled = config.get('translation.cache_enabled', True)
        
        logger.info("翻译引擎初始化完成")
    
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
                results = self.client.translate_batch(batch, from_lang, to_lang)
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
                        result = self.client.translate_text(text, from_lang, to_lang)
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
