# -*- coding: utf-8 -*-
"""
性能监控工具
"""
import time
from typing import Dict
from collections import defaultdict
from ..utils.logger import logger


class PerformanceMonitor:
    """性能监控器"""
    
    def __init__(self):
        self.timers = defaultdict(list)
        self.counters = defaultdict(int)
        self.enabled = True
    
    def start_timer(self, name: str):
        """开始计时"""
        if not self.enabled:
            return None
        return time.perf_counter()
    
    def end_timer(self, name: str, start_time: float):
        """结束计时"""
        if not self.enabled or start_time is None:
            return
        elapsed = (time.perf_counter() - start_time) * 1000  # ms
        self.timers[name].append(elapsed)
        
        # 只保留最近100次记录
        if len(self.timers[name]) > 100:
            self.timers[name] = self.timers[name][-100:]
    
    def increment(self, name: str, value: int = 1):
        """增加计数器"""
        if self.enabled:
            self.counters[name] += value
    
    def get_stats(self, name: str) -> Dict:
        """获取统计信息"""
        if name not in self.timers or not self.timers[name]:
            return {}
        
        times = self.timers[name]
        return {
            'avg': sum(times) / len(times),
            'min': min(times),
            'max': max(times),
            'count': len(times),
            'total': sum(times)
        }
    
    def get_all_stats(self) -> Dict:
        """获取所有统计"""
        stats = {}
        for name in self.timers:
            stats[name] = self.get_stats(name)
        stats['counters'] = dict(self.counters)
        return stats
    
    def reset(self):
        """重置所有统计"""
        self.timers.clear()
        self.counters.clear()
    
    def print_stats(self):
        """打印统计信息"""
        logger.info("=== 性能统计 ===")
        for name, stat in self.get_all_stats().items():
            if name == 'counters':
                continue
            logger.info(f"{name}: avg={stat['avg']:.2f}ms, min={stat['min']:.2f}ms, max={stat['max']:.2f}ms")
        
        if self.counters:
            logger.info("=== 计数器 ===")
            for name, count in self.counters.items():
                logger.info(f"{name}: {count}")


# 全局性能监控器
perf_monitor = PerformanceMonitor()
