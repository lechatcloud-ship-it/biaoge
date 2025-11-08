# -*- coding: utf-8 -*-
"""
性能监控面板
"""
from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QLabel,
    QGroupBox, QTextEdit, QPushButton, QProgressBar
)
from PyQt6.QtCore import Qt, QTimer
from PyQt6.QtGui import QFont
import psutil
import os

from ..utils.performance import perf_monitor
from ..utils.resource_manager import resource_manager


class PerformancePanel(QWidget):
    """性能监控面板"""

    def __init__(self, parent=None):
        super().__init__(parent)

        self._init_ui()

        # 更新定时器
        self.update_timer = QTimer()
        self.update_timer.timeout.connect(self._update_metrics)
        self.update_timer.start(1000)  # 每秒更新

    def _init_ui(self):
        """初始化UI"""
        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 10, 10, 10)

        # 标题
        title = QLabel("⚡ 性能监控")
        title_font = QFont("Microsoft YaHei UI", 12, QFont.Weight.Bold)
        title.setFont(title_font)
        layout.addWidget(title)

        # CPU和内存组
        system_group = QGroupBox("系统资源")
        system_layout = QVBoxLayout()

        # CPU
        cpu_layout = QHBoxLayout()
        cpu_layout.addWidget(QLabel("CPU使用率:"))
        self.cpu_label = QLabel("0%")
        self.cpu_label.setStyleSheet("font-weight: bold; color: #2196F3;")
        cpu_layout.addWidget(self.cpu_label)
        cpu_layout.addStretch()
        system_layout.addLayout(cpu_layout)

        self.cpu_bar = QProgressBar()
        self.cpu_bar.setMaximum(100)
        system_layout.addWidget(self.cpu_bar)

        # 内存
        mem_layout = QHBoxLayout()
        mem_layout.addWidget(QLabel("内存使用:"))
        self.mem_label = QLabel("0 MB")
        self.mem_label.setStyleSheet("font-weight: bold; color: #4CAF50;")
        mem_layout.addWidget(self.mem_label)
        mem_layout.addStretch()
        system_layout.addLayout(mem_layout)

        self.mem_bar = QProgressBar()
        self.mem_bar.setMaximum(1000)  # 1GB
        system_layout.addWidget(self.mem_bar)

        system_group.setLayout(system_layout)
        layout.addWidget(system_group)

        # 性能统计组
        stats_group = QGroupBox("性能统计")
        stats_layout = QVBoxLayout()

        self.stats_text = QTextEdit()
        self.stats_text.setReadOnly(True)
        self.stats_text.setMaximumHeight(200)
        font = QFont("Consolas", 9)
        self.stats_text.setFont(font)

        stats_layout.addWidget(self.stats_text)

        stats_group.setLayout(stats_layout)
        layout.addWidget(stats_group)

        # 操作按钮
        button_layout = QHBoxLayout()

        self.optimize_btn = QPushButton("优化内存")
        self.optimize_btn.clicked.connect(self._optimize_memory)
        button_layout.addWidget(self.optimize_btn)

        self.clear_stats_btn = QPushButton("清除统计")
        self.clear_stats_btn.clicked.connect(self._clear_stats)
        button_layout.addWidget(self.clear_stats_btn)

        layout.addLayout(button_layout)

        layout.addStretch()

    def _update_metrics(self):
        """更新指标"""
        try:
            # CPU
            cpu_percent = psutil.cpu_percent(interval=None)
            self.cpu_label.setText(f"{cpu_percent:.1f}%")
            self.cpu_bar.setValue(int(cpu_percent))

            # 更新CPU标签颜色
            if cpu_percent > 80:
                self.cpu_label.setStyleSheet("font-weight: bold; color: #F44336;")
            elif cpu_percent > 50:
                self.cpu_label.setStyleSheet("font-weight: bold; color: #FF9800;")
            else:
                self.cpu_label.setStyleSheet("font-weight: bold; color: #2196F3;")

            # 内存
            usage = resource_manager.get_memory_usage()
            mem_mb = usage['rss_mb']
            self.mem_label.setText(f"{mem_mb:.1f} MB")
            self.mem_bar.setValue(int(mem_mb))

            # 更新内存标签颜色
            if mem_mb > 500:
                self.mem_label.setStyleSheet("font-weight: bold; color: #F44336;")
            elif mem_mb > 300:
                self.mem_label.setStyleSheet("font-weight: bold; color: #FF9800;")
            else:
                self.mem_label.setStyleSheet("font-weight: bold; color: #4CAF50;")

            # 性能统计
            self._update_stats()

        except Exception as e:
            pass  # 忽略更新错误

    def _update_stats(self):
        """更新性能统计"""
        try:
            stats_text = []

            # 主要性能指标
            metrics = [
                ('paint_event', '渲染帧时间'),
                ('set_document', '文档加载'),
                ('update_visible_entities', '实体更新'),
                ('component_recognition', '构件识别'),
                ('dwg_export', 'DWG导出')
            ]

            for metric_name, display_name in metrics:
                try:
                    stats = perf_monitor.get_stats(metric_name)
                    if stats and stats.get('count', 0) > 0:
                        avg = stats['avg']
                        min_val = stats['min']
                        max_val = stats['max']
                        count = stats['count']

                        stats_text.append(
                            f"{display_name:12s}: "
                            f"平均 {avg:6.2f}ms | "
                            f"最小 {min_val:6.2f}ms | "
                            f"最大 {max_val:6.2f}ms | "
                            f"次数 {count:4d}"
                        )
                except:
                    pass

            if stats_text:
                self.stats_text.setPlainText('\n'.join(stats_text))
            else:
                self.stats_text.setPlainText("暂无性能数据\n\n执行操作后会显示性能统计...")

        except Exception as e:
            pass

    def _optimize_memory(self):
        """优化内存"""
        try:
            freed = resource_manager.optimize_memory()

            from PyQt6.QtWidgets import QMessageBox
            QMessageBox.information(
                self,
                "优化完成",
                f"内存优化完成\n\n释放内存: {freed:.2f} MB"
            )

            self._update_metrics()

        except Exception as e:
            from PyQt6.QtWidgets import QMessageBox
            QMessageBox.critical(self, "错误", f"优化失败:\n{e}")

    def _clear_stats(self):
        """清除统计"""
        # 清空性能监控器
        perf_monitor.timers.clear()
        perf_monitor.counters.clear()

        self.stats_text.setPlainText("统计已清除")

        from PyQt6.QtWidgets import QMessageBox
        QMessageBox.information(self, "完成", "性能统计已清除")

    def showEvent(self, event):
        """显示事件"""
        super().showEvent(event)
        self.update_timer.start(1000)

    def hideEvent(self, event):
        """隐藏事件"""
        super().hideEvent(event)
        self.update_timer.stop()
