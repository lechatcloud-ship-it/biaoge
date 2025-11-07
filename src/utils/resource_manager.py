"""资源管理器 - 内存和文件资源管理"""
import gc
import psutil
import os
from typing import Dict
from ..utils.logger import logger

class ResourceManager:
    """资源管理器"""
    
    def __init__(self):
        self.process = psutil.Process(os.getpid())
        logger.info("资源管理器初始化")
    
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
    
    def check_memory_threshold(self, threshold_mb=500):
        """检查内存阈值"""
        usage = self.get_memory_usage()
        if usage['rss_mb'] > threshold_mb:
            logger.warning(f"内存使用过高: {usage['rss_mb']:.2f} MB")
            self.optimize_memory()
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
