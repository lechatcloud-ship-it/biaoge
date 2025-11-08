# -*- coding: utf-8 -*-
"""
批量处理界面组件
"""
from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout,
    QHeaderView, QFileDialog
)
from PyQt6.QtCore import Qt, QThread, pyqtSignal
from PyQt6.QtGui import QColor, QBrush
from pathlib import Path
from typing import List

from qfluentwidgets import (
    PushButton, TableWidget, BodyLabel, TitleLabel,
    ProgressBar, CheckBox, CardWidget, MessageBox,
    FluentIcon
)

from ..batch.processor import BatchProcessor, BatchTask, TaskStatus
from ..utils.logger import logger


class BatchProcessThread(QThread):
    """批处理工作线程"""

    task_started = pyqtSignal(int)  # task_index
    task_progress = pyqtSignal(int, float)  # task_index, progress
    task_completed = pyqtSignal(int)  # task_index
    task_failed = pyqtSignal(int, str)  # task_index, error_message
    all_completed = pyqtSignal()

    def __init__(self, processor: BatchProcessor):
        super().__init__()
        self.processor = processor
        self.translate = True
        self.export = False
        self.export_dir = None

    def run(self):
        """运行批处理"""
        # 设置回调
        self.processor.on_task_start = lambda task: self.task_started.emit(
            self.processor.tasks.index(task)
        )

        self.processor.on_task_progress = lambda task, progress: self.task_progress.emit(
            self.processor.tasks.index(task),
            progress
        )

        self.processor.on_task_complete = lambda task: self.task_completed.emit(
            self.processor.tasks.index(task)
        )

        self.processor.on_task_failed = lambda task, error: self.task_failed.emit(
            self.processor.tasks.index(task),
            error
        )

        self.processor.on_all_complete = lambda: self.all_completed.emit()

        # 开始处理
        self.processor.process_all(
            translate=self.translate,
            export=self.export,
            export_dir=self.export_dir
        )


class BatchWidget(QWidget):
    """批量处理界面组件"""

    def __init__(self):
        super().__init__()
        self.processor = BatchProcessor()
        self.process_thread = None

        self._init_ui()
        self._connect_signals()

    def _init_ui(self):
        """初始化UI"""
        layout = QVBoxLayout(self)
        layout.setContentsMargins(10, 10, 10, 10)

        # 标题
        title = TitleLabel("批量文件处理")
        layout.addWidget(title)

        # 文件列表组
        file_group = CardWidget()
        file_layout = QVBoxLayout(file_group)

        # 文件操作按钮
        btn_layout = QHBoxLayout()

        self.add_files_btn = PushButton(FluentIcon.ADD, "添加文件")
        self.add_files_btn.setToolTip("添加DWG/DXF文件到批处理列表")
        btn_layout.addWidget(self.add_files_btn)

        self.remove_selected_btn = PushButton(FluentIcon.REMOVE, "移除选中")
        self.remove_selected_btn.setToolTip("移除选中的文件")
        self.remove_selected_btn.setEnabled(False)
        btn_layout.addWidget(self.remove_selected_btn)

        self.clear_all_btn = PushButton(FluentIcon.DELETE, "清空列表")
        self.clear_all_btn.setToolTip("清空所有文件")
        btn_layout.addWidget(self.clear_all_btn)

        btn_layout.addStretch()
        file_layout.addLayout(btn_layout)

        # 文件表格
        self.file_table = TableWidget()
        self.file_table.setColumnCount(4)
        self.file_table.setHorizontalHeaderLabels(["文件名", "状态", "进度", "错误信息"])
        self.file_table.horizontalHeader().setSectionResizeMode(0, QHeaderView.ResizeMode.Stretch)
        self.file_table.horizontalHeader().setSectionResizeMode(1, QHeaderView.ResizeMode.Fixed)
        self.file_table.horizontalHeader().setSectionResizeMode(2, QHeaderView.ResizeMode.Fixed)
        self.file_table.horizontalHeader().setSectionResizeMode(3, QHeaderView.ResizeMode.Stretch)
        self.file_table.setColumnWidth(1, 80)
        self.file_table.setColumnWidth(2, 100)
        self.file_table.setSelectionBehavior(TableWidget.SelectionBehavior.SelectRows)
        file_layout.addWidget(self.file_table)

        layout.addWidget(file_group)

        # 处理选项组
        options_group = CardWidget()
        options_layout = QVBoxLayout(options_group)

        options_title = BodyLabel("处理选项")
        options_title.setStyleSheet("font-weight: bold;")
        options_layout.addWidget(options_title)

        self.translate_checkbox = CheckBox("翻译文本")
        self.translate_checkbox.setChecked(True)
        self.translate_checkbox.setToolTip("自动翻译DWG文件中的文本")
        options_layout.addWidget(self.translate_checkbox)

        # TODO: 导出选项暂时禁用，等导出功能完善后启用
        self.export_checkbox = CheckBox("导出处理后的文件")
        self.export_checkbox.setEnabled(False)
        self.export_checkbox.setToolTip("将处理后的文件导出到指定目录（暂未实现）")
        options_layout.addWidget(self.export_checkbox)

        layout.addWidget(options_group)

        # 统计信息
        stats_group = CardWidget()
        stats_layout = QVBoxLayout(stats_group)

        stats_title = BodyLabel("统计信息")
        stats_title.setStyleSheet("font-weight: bold;")
        stats_layout.addWidget(stats_title)

        self.stats_label = BodyLabel("总计: 0 | 完成: 0 | 失败: 0 | 成功率: 0%")
        stats_layout.addWidget(self.stats_label)

        self.overall_progress = ProgressBar()
        self.overall_progress.setTextVisible(True)
        self.overall_progress.setFormat("总进度: %p%")
        stats_layout.addWidget(self.overall_progress)

        layout.addWidget(stats_group)

        # 控制按钮
        control_layout = QHBoxLayout()
        control_layout.addStretch()

        self.start_btn = PushButton(FluentIcon.PLAY, "开始处理")
        self.start_btn.setEnabled(False)
        control_layout.addWidget(self.start_btn)

        self.cancel_btn = PushButton(FluentIcon.CANCEL, "取消")
        self.cancel_btn.setEnabled(False)
        control_layout.addWidget(self.cancel_btn)

        layout.addLayout(control_layout)

    def _connect_signals(self):
        """连接信号"""
        self.add_files_btn.clicked.connect(self.on_add_files)
        self.remove_selected_btn.clicked.connect(self.on_remove_selected)
        self.clear_all_btn.clicked.connect(self.on_clear_all)
        self.start_btn.clicked.connect(self.on_start_processing)
        self.cancel_btn.clicked.connect(self.on_cancel_processing)
        self.file_table.itemSelectionChanged.connect(self.on_selection_changed)

    def on_add_files(self):
        """添加文件"""
        file_paths, _ = QFileDialog.getOpenFileNames(
            self,
            "选择DWG/DXF文件",
            "",
            "DWG文件 (*.dwg *.dxf);;所有文件 (*.*)"
        )

        if file_paths:
            self.processor.add_files(file_paths)
            self.refresh_file_table()
            self.update_ui_state()

    def on_remove_selected(self):
        """移除选中的文件"""
        selected_rows = sorted(
            set(item.row() for item in self.file_table.selectedItems()),
            reverse=True
        )

        for row in selected_rows:
            self.processor.remove_task(row)

        self.refresh_file_table()
        self.update_ui_state()

    def on_clear_all(self):
        """清空所有文件"""
        if self.processor.tasks:
            w = MessageBox(
                "确认清空",
                f"确定要清空所有 {len(self.processor.tasks)} 个文件吗？",
                self
            )
            if w.exec():
                self.processor.clear_tasks()
                self.refresh_file_table()
                self.update_ui_state()

    def on_selection_changed(self):
        """选择变化"""
        has_selection = len(self.file_table.selectedItems()) > 0
        self.remove_selected_btn.setEnabled(has_selection and not self.processor.is_processing)

    def on_start_processing(self):
        """开始处理"""
        if not self.processor.tasks:
            MessageBox("无文件", "请先添加文件到批处理列表", self).exec()
            return

        # 创建处理线程
        self.process_thread = BatchProcessThread(self.processor)
        self.process_thread.translate = self.translate_checkbox.isChecked()
        self.process_thread.export = self.export_checkbox.isChecked()

        # 连接信号
        self.process_thread.task_started.connect(self.on_task_started)
        self.process_thread.task_progress.connect(self.on_task_progress)
        self.process_thread.task_completed.connect(self.on_task_completed)
        self.process_thread.task_failed.connect(self.on_task_failed)
        self.process_thread.all_completed.connect(self.on_all_completed)

        # 更新UI状态
        self.start_btn.setEnabled(False)
        self.cancel_btn.setEnabled(True)
        self.add_files_btn.setEnabled(False)
        self.remove_selected_btn.setEnabled(False)
        self.clear_all_btn.setEnabled(False)

        # 启动线程
        self.process_thread.start()
        logger.info("批处理线程已启动")

    def on_cancel_processing(self):
        """取消处理"""
        w = MessageBox(
            "确认取消",
            "确定要取消批处理吗？",
            self
        )
        if w.exec():
            self.processor.cancel()
            self.cancel_btn.setEnabled(False)
            logger.info("用户请求取消批处理")

    def on_task_started(self, task_index: int):
        """任务开始"""
        task = self.processor.tasks[task_index]
        self.update_task_row(task_index, task)
        logger.debug(f"任务开始: {task.filename}")

    def on_task_progress(self, task_index: int, progress: float):
        """任务进度更新"""
        task = self.processor.tasks[task_index]
        self.update_task_row(task_index, task)
        self.update_overall_progress()

    def on_task_completed(self, task_index: int):
        """任务完成"""
        task = self.processor.tasks[task_index]
        self.update_task_row(task_index, task)
        self.update_overall_progress()
        self.update_statistics()
        logger.debug(f"任务完成: {task.filename}")

    def on_task_failed(self, task_index: int, error_message: str):
        """任务失败"""
        task = self.processor.tasks[task_index]
        self.update_task_row(task_index, task)
        self.update_overall_progress()
        self.update_statistics()
        logger.debug(f"任务失败: {task.filename} - {error_message}")

    def on_all_completed(self):
        """全部完成"""
        stats = self.processor.get_statistics()

        MessageBox(
            "批处理完成",
            f"批处理已完成！\n\n"
            f"总计: {stats['total']}\n"
            f"成功: {stats['completed']}\n"
            f"失败: {stats['failed']}\n"
            f"跳过: {stats['skipped']}\n"
            f"成功率: {stats['success_rate']:.1f}%\n"
            f"总耗时: {stats['total_duration']:.2f}秒",
            self
        ).exec()

        # 恢复UI状态
        self.start_btn.setEnabled(True)
        self.cancel_btn.setEnabled(False)
        self.add_files_btn.setEnabled(True)
        self.clear_all_btn.setEnabled(True)

        logger.info("批处理全部完成")

    def refresh_file_table(self):
        """刷新文件表格"""
        from PyQt6.QtWidgets import QTableWidgetItem
        self.file_table.setRowCount(len(self.processor.tasks))

        for index, task in enumerate(self.processor.tasks):
            self.update_task_row(index, task)

    def update_task_row(self, row: int, task: BatchTask):
        """更新任务行"""
        from PyQt6.QtWidgets import QTableWidgetItem

        # 文件名
        filename_item = QTableWidgetItem(task.filename)
        filename_item.setToolTip(str(task.file_path))
        self.file_table.setItem(row, 0, filename_item)

        # 状态
        status_item = QTableWidgetItem(task.status_text)
        status_item.setTextAlignment(Qt.AlignmentFlag.AlignCenter)

        # 根据状态设置颜色
        if task.status == TaskStatus.COMPLETED:
            status_item.setForeground(QBrush(QColor("#28A745")))
        elif task.status == TaskStatus.FAILED:
            status_item.setForeground(QBrush(QColor("#DC3545")))
        elif task.status == TaskStatus.PROCESSING:
            status_item.setForeground(QBrush(QColor("#0078D4")))
        elif task.status == TaskStatus.SKIPPED:
            status_item.setForeground(QBrush(QColor("#FFC107")))

        self.file_table.setItem(row, 1, status_item)

        # 进度
        progress_widget = ProgressBar()
        progress_widget.setMaximum(100)
        progress_widget.setValue(int(task.progress * 100))
        progress_widget.setTextVisible(True)
        progress_widget.setFormat("%p%")
        self.file_table.setCellWidget(row, 2, progress_widget)

        # 错误信息
        error_item = QTableWidgetItem(task.error_message or "")
        error_item.setToolTip(task.error_message or "")
        self.file_table.setItem(row, 3, error_item)

    def update_overall_progress(self):
        """更新总进度"""
        if not self.processor.tasks:
            self.overall_progress.setValue(0)
            return

        total_progress = sum(task.progress for task in self.processor.tasks)
        overall = int((total_progress / len(self.processor.tasks)) * 100)
        self.overall_progress.setValue(overall)

    def update_statistics(self):
        """更新统计信息"""
        stats = self.processor.get_statistics()
        self.stats_label.setText(
            f"总计: {stats['total']} | "
            f"完成: {stats['completed']} | "
            f"失败: {stats['failed']} | "
            f"跳过: {stats['skipped']} | "
            f"成功率: {stats['success_rate']:.1f}%"
        )

    def update_ui_state(self):
        """更新UI状态"""
        has_tasks = len(self.processor.tasks) > 0
        is_processing = self.processor.is_processing

        self.start_btn.setEnabled(has_tasks and not is_processing)
        self.clear_all_btn.setEnabled(has_tasks and not is_processing)

        self.update_statistics()
        self.update_overall_progress()
