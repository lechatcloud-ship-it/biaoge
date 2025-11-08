# -*- coding: utf-8 -*-
"""配置持久化 - 商业级"""
import json
from pathlib import Path
from typing import Any, Dict
from ..utils.logger import logger

class ConfigPersistence:
    """配置持久化管理"""
    
    def __init__(self):
        self.config_dir = Path.home() / '.biaoge'
        self.config_dir.mkdir(parents=True, exist_ok=True)
        self.state_file = self.config_dir / 'app_state.json'
        self.recent_files_file = self.config_dir / 'recent_files.json'
        
    def save_state(self, state: Dict[str, Any]):
        """保存应用状态"""
        try:
            with open(self.state_file, 'w', encoding='utf-8') as f:
                json.dump(state, f, indent=2, ensure_ascii=False)
            logger.debug("应用状态已保存")
        except Exception as e:
            logger.error(f"保存状态失败: {e}")
    
    def load_state(self) -> Dict[str, Any]:
        """加载应用状态"""
        try:
            if self.state_file.exists():
                with open(self.state_file, 'r', encoding='utf-8') as f:
                    return json.load(f)
        except Exception as e:
            logger.error(f"加载状态失败: {e}")
        return {}
    
    def save_recent_files(self, files: list):
        """保存最近打开的文件"""
        try:
            # 只保留最近10个
            files = files[-10:] if len(files) > 10 else files
            with open(self.recent_files_file, 'w', encoding='utf-8') as f:
                json.dump(files, f, indent=2, ensure_ascii=False)
        except Exception as e:
            logger.error(f"保存最近文件失败: {e}")
    
    def load_recent_files(self) -> list:
        """加载最近打开的文件"""
        try:
            if self.recent_files_file.exists():
                with open(self.recent_files_file, 'r', encoding='utf-8') as f:
                    return json.load(f)
        except Exception as e:
            logger.error(f"加载最近文件失败: {e}")
        return []

persistence = ConfigPersistence()
