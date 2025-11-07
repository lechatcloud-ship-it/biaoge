"""
日志查看器
"""
from PyQt6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QTextEdit,
    QPushButton, QComboBox, QLabel, QFileDialog
)
from PyQt6.QtCore import Qt, QTimer
from PyQt6.QtGui import QFont, QTextCursor
from pathlib import Path
import logging


class LogViewerDialog(QDialog):
    """日志查看器对话框"""

    def __init__(self, parent=None):
        super().__init__(parent)

        self.setWindowTitle("日志查看器")
        self.setMinimumSize(900, 600)

        self.log_file = Path("logs") / "app.log"

        self._init_ui()
        self._load_log()

        # 自动刷新定时器
        self.refresh_timer = QTimer()
        self.refresh_timer.timeout.connect(self._load_log)

    def _init_ui(self):
        """初始化UI"""
        layout = QVBoxLayout(self)

        # 工具栏
        toolbar = QHBoxLayout()

        # 日志级别过滤
        toolbar.addWidget(QLabel("级别:"))

        self.level_combo = QComboBox()
        self.level_combo.addItems(["全部", "DEBUG", "INFO", "WARNING", "ERROR"])
        self.level_combo.currentTextChanged.connect(self._on_level_changed)
        toolbar.addWidget(self.level_combo)

        toolbar.addStretch()

        # 自动刷新
        self.auto_refresh_combo = QComboBox()
        self.auto_refresh_combo.addItems(["不自动刷新", "每1秒", "每3秒", "每5秒"])
        self.auto_refresh_combo.currentIndexChanged.connect(self._on_refresh_interval_changed)
        toolbar.addWidget(self.auto_refresh_combo)

        # 刷新按钮
        refresh_btn = QPushButton("刷新")
        refresh_btn.clicked.connect(self._load_log)
        toolbar.addWidget(refresh_btn)

        # 清空按钮
        clear_btn = QPushButton("清空日志")
        clear_btn.clicked.connect(self._clear_log)
        toolbar.addWidget(clear_btn)

        # 导出按钮
        export_btn = QPushButton("导出...")
        export_btn.clicked.connect(self._export_log)
        toolbar.addWidget(export_btn)

        layout.addLayout(toolbar)

        # 日志文本
        self.log_text = QTextEdit()
        self.log_text.setReadOnly(True)
        self.log_text.setLineWrapMode(QTextEdit.LineWrapMode.NoWrap)

        # 等宽字体
        font = QFont("Consolas" if self._is_windows() else "Monaco")
        font.setPointSize(9)
        self.log_text.setFont(font)

        layout.addWidget(self.log_text)

        # 状态栏
        status_layout = QHBoxLayout()

        self.status_label = QLabel()
        status_layout.addWidget(self.status_label)

        status_layout.addStretch()

        # 关闭按钮
        close_btn = QPushButton("关闭")
        close_btn.clicked.connect(self.accept)
        status_layout.addWidget(close_btn)

        layout.addLayout(status_layout)

    def _is_windows(self):
        """检查是否Windows系统"""
        import sys
        return sys.platform == 'win32'

    def _load_log(self):
        """加载日志"""
        try:
            if not self.log_file.exists():
                self.log_text.setPlainText("日志文件不存在")
                self.status_label.setText("日志文件不存在")
                return

            with open(self.log_file, 'r', encoding='utf-8') as f:
                lines = f.readlines()

            # 过滤级别
            level_filter = self.level_combo.currentText()
            if level_filter != "全部":
                lines = [line for line in lines if level_filter in line]

            # 显示最后1000行
            lines = lines[-1000:]

            self.log_text.setPlainText(''.join(lines))

            # 滚动到底部
            self.log_text.moveCursor(QTextCursor.MoveOperation.End)

            # 更新状态
            file_size = self.log_file.stat().st_size / 1024  # KB
            self.status_label.setText(
                f"日志文件: {self.log_file} | "
                f"大小: {file_size:.2f} KB | "
                f"显示: {len(lines)} 行"
            )

        except Exception as e:
            self.log_text.setPlainText(f"加载日志失败: {e}")
            self.status_label.setText("加载失败")

    def _on_level_changed(self, level):
        """级别改变"""
        self._load_log()

    def _on_refresh_interval_changed(self, index):
        """刷新间隔改变"""
        self.refresh_timer.stop()

        intervals = [0, 1000, 3000, 5000]
        interval = intervals[index]

        if interval > 0:
            self.refresh_timer.start(interval)

    def _clear_log(self):
        """清空日志"""
        from PyQt6.QtWidgets import QMessageBox

        reply = QMessageBox.question(
            self,
            "确认",
            "确定要清空日志文件吗？",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No
        )

        if reply == QMessageBox.StandardButton.Yes:
            try:
                if self.log_file.exists():
                    self.log_file.write_text("")
                    self._load_log()
                    QMessageBox.information(self, "成功", "日志已清空")
            except Exception as e:
                QMessageBox.critical(self, "错误", f"清空日志失败:\n{e}")

    def _export_log(self):
        """导出日志"""
        file_path, _ = QFileDialog.getSaveFileName(
            self,
            "导出日志",
            "",
            "文本文件 (*.txt);;日志文件 (*.log);;所有文件 (*.*)"
        )

        if file_path:
            try:
                with open(file_path, 'w', encoding='utf-8') as f:
                    f.write(self.log_text.toPlainText())

                from PyQt6.QtWidgets import QMessageBox
                QMessageBox.information(self, "成功", f"日志已导出到:\n{file_path}")

            except Exception as e:
                from PyQt6.QtWidgets import QMessageBox
                QMessageBox.critical(self, "错误", f"导出失败:\n{e}")

    def closeEvent(self, event):
        """关闭事件"""
        self.refresh_timer.stop()
        super().closeEvent(event)
