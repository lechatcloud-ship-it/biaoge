"""
翻译缓存（SQLite）
"""
import sqlite3
import hashlib
from datetime import datetime, timedelta
from typing import Optional, Dict, Tuple
from pathlib import Path

from ..utils.logger import logger
from ..utils.config_manager import ConfigManager


class TranslationCache:
    """翻译缓存管理器"""
    
    def __init__(self, db_path: Optional[str] = None):
        """
        初始化缓存
        
        Args:
            db_path: 数据库文件路径，默认使用用户目录
        """
        config = ConfigManager()
        
        if db_path is None:
            cache_dir = Path.home() / '.biaoge' / 'cache'
            cache_dir.mkdir(parents=True, exist_ok=True)
            db_path = str(cache_dir / 'translation.db')
        
        self.db_path = db_path
        self.ttl_days = config.get('translation.cache_ttl_days', 7)
        
        # 统计信息
        self.hits = 0
        self.misses = 0
        
        self._init_database()
        logger.info(f"翻译缓存初始化: {self.db_path}, TTL={self.ttl_days}天")
    
    def _init_database(self):
        """初始化数据库表"""
        with sqlite3.connect(self.db_path) as conn:
            conn.execute('''
                CREATE TABLE IF NOT EXISTS translations (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    text_hash TEXT NOT NULL,
                    original_text TEXT NOT NULL,
                    translated_text TEXT NOT NULL,
                    from_lang TEXT NOT NULL,
                    to_lang TEXT NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    accessed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    access_count INTEGER DEFAULT 1,
                    UNIQUE(text_hash, from_lang, to_lang)
                )
            ''')
            
            # 创建索引以提高查询性能
            conn.execute('''
                CREATE INDEX IF NOT EXISTS idx_text_hash 
                ON translations(text_hash, from_lang, to_lang)
            ''')
            
            conn.execute('''
                CREATE INDEX IF NOT EXISTS idx_created_at 
                ON translations(created_at)
            ''')
            
            conn.commit()
    
    def get(self, text: str, from_lang: str, to_lang: str) -> Optional[str]:
        """
        获取缓存的翻译
        
        Args:
            text: 原文
            from_lang: 源语言
            to_lang: 目标语言
        
        Returns:
            Optional[str]: 翻译结果，如果未找到则返回None
        """
        text_hash = self._hash_text(text)
        expiry_date = datetime.now() - timedelta(days=self.ttl_days)
        
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.execute('''
                SELECT translated_text FROM translations
                WHERE text_hash = ? AND from_lang = ? AND to_lang = ?
                AND created_at > ?
            ''', (text_hash, from_lang, to_lang, expiry_date))
            
            result = cursor.fetchone()
            
            if result:
                self.hits += 1
                
                # 更新访问时间和计数
                conn.execute('''
                    UPDATE translations
                    SET accessed_at = CURRENT_TIMESTAMP,
                        access_count = access_count + 1
                    WHERE text_hash = ? AND from_lang = ? AND to_lang = ?
                ''', (text_hash, from_lang, to_lang))
                conn.commit()
                
                logger.debug(f"缓存命中: {text[:20]}... -> {result[0][:20]}...")
                return result[0]
            else:
                self.misses += 1
                return None
    
    def set(
        self,
        text: str,
        translated_text: str,
        from_lang: str,
        to_lang: str
    ):
        """
        保存翻译到缓存
        
        Args:
            text: 原文
            translated_text: 译文
            from_lang: 源语言
            to_lang: 目标语言
        """
        text_hash = self._hash_text(text)
        
        with sqlite3.connect(self.db_path) as conn:
            try:
                conn.execute('''
                    INSERT INTO translations 
                    (text_hash, original_text, translated_text, from_lang, to_lang)
                    VALUES (?, ?, ?, ?, ?)
                ''', (text_hash, text, translated_text, from_lang, to_lang))
                conn.commit()
                logger.debug(f"缓存保存: {text[:20]}... -> {translated_text[:20]}...")
            except sqlite3.IntegrityError:
                # 已存在，更新
                conn.execute('''
                    UPDATE translations
                    SET translated_text = ?,
                        accessed_at = CURRENT_TIMESTAMP,
                        access_count = access_count + 1
                    WHERE text_hash = ? AND from_lang = ? AND to_lang = ?
                ''', (translated_text, text_hash, from_lang, to_lang))
                conn.commit()
                logger.debug(f"缓存更新: {text[:20]}...")
    
    def get_batch(
        self,
        texts: list[str],
        from_lang: str,
        to_lang: str
    ) -> Dict[str, str]:
        """
        批量获取缓存
        
        Args:
            texts: 原文列表
            from_lang: 源语言
            to_lang: 目标语言
        
        Returns:
            Dict[str, str]: 原文->译文的字典（只包含缓存命中的）
        """
        results = {}
        
        for text in texts:
            cached = self.get(text, from_lang, to_lang)
            if cached:
                results[text] = cached
        
        return results
    
    def set_batch(
        self,
        translations: Dict[str, str],
        from_lang: str,
        to_lang: str
    ):
        """
        批量保存翻译
        
        Args:
            translations: 原文->译文的字典
            from_lang: 源语言
            to_lang: 目标语言
        """
        for text, translated_text in translations.items():
            self.set(text, translated_text, from_lang, to_lang)
    
    def clear_expired(self) -> int:
        """
        清理过期缓存
        
        Returns:
            int: 清理的记录数
        """
        expiry_date = datetime.now() - timedelta(days=self.ttl_days)
        
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.execute('''
                DELETE FROM translations WHERE created_at < ?
            ''', (expiry_date,))
            deleted_count = cursor.rowcount
            conn.commit()
        
        if deleted_count > 0:
            logger.info(f"清理过期缓存: {deleted_count}条记录")
        
        return deleted_count
    
    def get_statistics(self) -> Dict:
        """
        获取缓存统计信息
        
        Returns:
            Dict: 统计信息
        """
        with sqlite3.connect(self.db_path) as conn:
            cursor = conn.execute('SELECT COUNT(*) FROM translations')
            total_count = cursor.fetchone()[0]
            
            cursor = conn.execute('''
                SELECT 
                    from_lang || '->' || to_lang as lang_pair,
                    COUNT(*) as count
                FROM translations
                GROUP BY lang_pair
            ''')
            lang_pairs = dict(cursor.fetchall())
            
            cursor = conn.execute('''
                SELECT SUM(access_count) FROM translations
            ''')
            total_accesses = cursor.fetchone()[0] or 0
        
        hit_rate = self.hits / (self.hits + self.misses) if (self.hits + self.misses) > 0 else 0
        
        return {
            'total_entries': total_count,
            'language_pairs': lang_pairs,
            'total_accesses': total_accesses,
            'hit_rate': hit_rate,
            'hits': self.hits,
            'misses': self.misses
        }
    
    def _hash_text(self, text: str) -> str:
        """计算文本哈希值"""
        return hashlib.md5(text.encode('utf-8')).hexdigest()
    
    def close(self):
        """关闭缓存（当前实现中无需显式关闭）"""
        pass
