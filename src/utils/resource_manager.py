"""资源管理器 - 内存和文件资源管理"""
import gc
import psutil
import os
from typing import Dict
from ..utils.logger import logger
from .config_manager import ConfigManager

class ResourceManager:
    """资源管理器"""

    def __init__(self):
        self.process = psutil.Process(os.getpid())
        self.config = ConfigManager()
        # 从配置读取内存阈值（确保设置生效）
        self.memory_threshold_mb = self.config.get('performance.memory_threshold_mb', 500)
        self.auto_optimize = self.config.get('performance.auto_optimize', True)
        logger.info(
            f"资源管理器初始化 - "
            f"内存阈值: {self.memory_threshold_mb}MB, "
            f"自动优化: {self.auto_optimize}"
        )
    
    def get_memory_usage(self) -> Dict:
        """获取内存使用情况"""
        mem_info = self.process.memory_info()
        return {
            'rss_mb': mem_info.rss / 1024 / 1024,  # 物理内存 (MB)
            'vms_mb': mem_info.vms / 1024 / 1024,  # 虚拟内存 (MB)
            'percent': self.process.memory_percent()
        }
    
    def get_cpu_usage(self) -> float:
        """获取CPU使用率"""
        return self.process.cpu_percent(interval=0.1)
    
    def optimize_memory(self):
        """优化内存使用"""
        before = self.get_memory_usage()
        gc.collect()
        after = self.get_memory_usage()
        freed = before['rss_mb'] - after['rss_mb']
        logger.info(f"内存优化完成: 释放 {freed:.2f} MB")
        return freed
    
    def check_memory_threshold(self, threshold_mb=None):
        """检查内存阈值（使用配置的阈值）"""
        if threshold_mb is None:
            threshold_mb = self.memory_threshold_mb

        usage = self.get_memory_usage()
        if usage['rss_mb'] > threshold_mb:
            logger.warning(
                f"内存使用过高: {usage['rss_mb']:.2f} MB "
                f"(阈值: {threshold_mb} MB)"
            )
            if self.auto_optimize:
                self.optimize_memory()
                logger.info("已自动优化内存（配置启用）")
            return False
        return True
    
    def get_system_info(self) -> Dict:
        """获取系统信息"""
        return {
            'cpu_count': psutil.cpu_count(),
            'memory_total_gb': psutil.virtual_memory().total / 1024 / 1024 / 1024,
            'memory_available_gb': psutil.virtual_memory().available / 1024 / 1024 / 1024,
            'disk_usage_percent': psutil.disk_usage('/').percent
        }

resource_manager = ResourceManager()
