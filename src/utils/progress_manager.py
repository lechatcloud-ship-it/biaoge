# -*- coding: utf-8 -*-
"""进度管理器 - 商业级"""
import json
from pathlib import Path
from datetime import datetime
from typing import Dict, Any
from ..utils.logger import logger

class ProgressManager:
    """进度管理器 - 支持保存和恢复"""
    
    def __init__(self):
        self.progress_dir = Path.home() / '.biaoge' / 'progress'
        self.progress_dir.mkdir(parents=True, exist_ok=True)
        logger.info("进度管理器初始化")
    
    def save_progress(self, task_id: str, progress_data: Dict[str, Any]):
        """保存进度"""
        try:
            progress_file = self.progress_dir / f"{task_id}.json"
            data = {
                'task_id': task_id,
                'timestamp': datetime.now().isoformat(),
                'progress': progress_data
            }
            
            with open(progress_file, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2, ensure_ascii=False)
            
            logger.debug(f"进度已保存: {task_id}")
        except Exception as e:
            logger.error(f"保存进度失败: {e}")
    
    def load_progress(self, task_id: str) -> Dict[str, Any]:
        """加载进度"""
        try:
            progress_file = self.progress_dir / f"{task_id}.json"
            if progress_file.exists():
                with open(progress_file, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                logger.info(f"进度已加载: {task_id}")
                return data.get('progress', {})
        except Exception as e:
            logger.error(f"加载进度失败: {e}")
        return {}
    
    def clear_progress(self, task_id: str):
        """清除进度"""
        try:
            progress_file = self.progress_dir / f"{task_id}.json"
            if progress_file.exists():
                progress_file.unlink()
                logger.debug(f"进度已清除: {task_id}")
        except Exception as e:
            logger.error(f"清除进度失败: {e}")
    
    def list_tasks(self) -> list:
        """列出所有任务"""
        try:
            return [f.stem for f in self.progress_dir.glob('*.json')]
        except Exception as e:
            logger.error(f"列出任务失败: {e}")
            return []

progress_manager = ProgressManager()
