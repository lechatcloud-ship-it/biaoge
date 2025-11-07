"""
完整的设置对话框 - 商业级配置界面
"""
from PyQt6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QFormLayout,
    QTabWidget, QWidget, QPushButton, QLineEdit,
    QComboBox, QSpinBox, QCheckBox, QLabel,
    QGroupBox, QMessageBox, QFileDialog
)
from PyQt6.QtCore import Qt
from pathlib import Path
import os

from ..utils.config_manager import ConfigManager
from ..utils.logger import logger


class SettingsDialog(QDialog):
    """完整设置对话框"""

    def __init__(self, parent=None):
        super().__init__(parent)

        self.config = ConfigManager()
        self.setWindowTitle("设置")
        self.setMinimumSize(700, 600)
        self.setModal(True)

        self._init_ui()
        self._load_settings()

    def _init_ui(self):
        """初始化UI"""
        layout = QVBoxLayout(self)

        # 创建选项卡
        tab_widget = QTabWidget()

        # 1. 阿里云百炼设置
        tab_widget.addTab(self._create_bailian_tab(), "阿里云百炼")

        # 2. 性能设置
        tab_widget.addTab(self._create_performance_tab(), "性能优化")

        # 3. 界面设置
        tab_widget.addTab(self._create_ui_tab(), "界面设置")

        # 4. 高级设置
        tab_widget.addTab(self._create_advanced_tab(), "高级")

        layout.addWidget(tab_widget)

        # 按钮
        button_layout = QHBoxLayout()
        button_layout.addStretch()

        self.ok_button = QPushButton("确定")
        self.ok_button.clicked.connect(self._on_ok)

        self.cancel_button = QPushButton("取消")
        self.cancel_button.clicked.connect(self.reject)

        self.apply_button = QPushButton("应用")
        self.apply_button.clicked.connect(self._on_apply)

        button_layout.addWidget(self.ok_button)
        button_layout.addWidget(self.cancel_button)
        button_layout.addWidget(self.apply_button)

        layout.addLayout(button_layout)

    def _create_bailian_tab(self):
        """创建阿里云百炼设置选项卡"""
        widget = QWidget()
        layout = QVBoxLayout(widget)

        # API配置组
        api_group = QGroupBox("API配置")
        api_layout = QFormLayout()

        # API密钥
        self.api_key_edit = QLineEdit()
        self.api_key_edit.setEchoMode(QLineEdit.EchoMode.Password)
        self.api_key_edit.setPlaceholderText("请输入阿里云DashScope API Key")
        self.api_key_edit.setMinimumWidth(400)

        show_key_btn = QPushButton("👁")
        show_key_btn.setFixedWidth(30)
        show_key_btn.setCheckable(True)
        show_key_btn.toggled.connect(
            lambda checked: self.api_key_edit.setEchoMode(
                QLineEdit.EchoMode.Normal if checked else QLineEdit.EchoMode.Password
            )
        )

        key_layout = QHBoxLayout()
        key_layout.addWidget(self.api_key_edit)
        key_layout.addWidget(show_key_btn)

        api_layout.addRow("API密钥:", key_layout)

        # API密钥说明
        key_help = QLabel(
            '<a href="https://dashscope.console.aliyun.com/apiKey">点击获取API密钥</a> | '
            '密钥将安全保存在本地配置文件中'
        )
        key_help.setOpenExternalLinks(True)
        key_help.setStyleSheet("color: #666; font-size: 11px;")
        api_layout.addRow("", key_help)

        # 模型选择
        self.model_combo = QComboBox()
        self.model_combo.addItems([
            "qwen-plus (推荐) - ¥0.004/1K tokens",
            "qwen-turbo (快速) - ¥0.002/1K tokens",
            "qwen-max (最强) - ¥0.040/1K tokens"
        ])
        api_layout.addRow("模型:", self.model_combo)

        # API端点
        self.endpoint_edit = QLineEdit()
        self.endpoint_edit.setPlaceholderText("https://dashscope.aliyuncs.com")
        api_layout.addRow("API端点:", self.endpoint_edit)

        # 超时设置
        self.timeout_spin = QSpinBox()
        self.timeout_spin.setRange(10, 300)
        self.timeout_spin.setSuffix(" 秒")
        api_layout.addRow("请求超时:", self.timeout_spin)

        # 重试次数
        self.retry_spin = QSpinBox()
        self.retry_spin.setRange(1, 10)
        self.retry_spin.setSuffix(" 次")
        api_layout.addRow("重试次数:", self.retry_spin)

        api_group.setLayout(api_layout)
        layout.addWidget(api_group)

        # 翻译设置组
        trans_group = QGroupBox("翻译设置")
        trans_layout = QFormLayout()

        # 批量大小
        self.batch_size_spin = QSpinBox()
        self.batch_size_spin.setRange(10, 100)
        self.batch_size_spin.setSuffix(" 条/批")
        trans_layout.addRow("批量翻译大小:", self.batch_size_spin)

        # 缓存启用
        self.cache_enabled_check = QCheckBox("启用翻译缓存（提升90%+速度）")
        trans_layout.addRow("", self.cache_enabled_check)

        # 缓存TTL
        self.cache_ttl_spin = QSpinBox()
        self.cache_ttl_spin.setRange(1, 365)
        self.cache_ttl_spin.setSuffix(" 天")
        trans_layout.addRow("缓存有效期:", self.cache_ttl_spin)

        trans_group.setLayout(trans_layout)
        layout.addWidget(trans_group)

        # 测试按钮
        test_layout = QHBoxLayout()
        test_layout.addStretch()

        test_btn = QPushButton("测试连接")
        test_btn.clicked.connect(self._test_api_connection)
        test_layout.addWidget(test_btn)

        layout.addLayout(test_layout)

        layout.addStretch()

        return widget

    def _create_performance_tab(self):
        """创建性能设置选项卡"""
        widget = QWidget()
        layout = QVBoxLayout(widget)

        # 渲染性能组
        render_group = QGroupBox("渲染性能")
        render_layout = QFormLayout()

        # 空间索引
        self.spatial_index_check = QCheckBox("启用空间索引（大幅提升大型图纸性能）")
        render_layout.addRow("", self.spatial_index_check)

        # 抗锯齿
        self.antialiasing_check = QCheckBox("启用抗锯齿（更清晰，但略慢）")
        render_layout.addRow("", self.antialiasing_check)

        # 实体阈值
        self.entity_threshold_spin = QSpinBox()
        self.entity_threshold_spin.setRange(100, 100000)
        self.entity_threshold_spin.setSingleStep(1000)
        self.entity_threshold_spin.setSuffix(" 个")
        render_layout.addRow("空间索引阈值:", self.entity_threshold_spin)

        render_group.setLayout(render_layout)
        layout.addWidget(render_group)

        # 内存管理组
        memory_group = QGroupBox("内存管理")
        memory_layout = QFormLayout()

        # 内存阈值
        self.memory_threshold_spin = QSpinBox()
        self.memory_threshold_spin.setRange(100, 2000)
        self.memory_threshold_spin.setSingleStep(50)
        self.memory_threshold_spin.setSuffix(" MB")
        memory_layout.addRow("内存警告阈值:", self.memory_threshold_spin)

        # 自动优化
        self.auto_optimize_check = QCheckBox("内存超限自动优化")
        memory_layout.addRow("", self.auto_optimize_check)

        memory_group.setLayout(memory_layout)
        layout.addWidget(memory_group)

        # 性能监控组
        monitor_group = QGroupBox("性能监控")
        monitor_layout = QFormLayout()

        # 启用监控
        self.perf_monitor_check = QCheckBox("启用性能监控（开发模式）")
        monitor_layout.addRow("", self.perf_monitor_check)

        # 监控历史
        self.perf_history_spin = QSpinBox()
        self.perf_history_spin.setRange(10, 1000)
        self.perf_history_spin.setSuffix(" 条")
        monitor_layout.addRow("保留历史记录:", self.perf_history_spin)

        monitor_group.setLayout(monitor_layout)
        layout.addWidget(monitor_group)

        layout.addStretch()

        return widget

    def _create_ui_tab(self):
        """创建界面设置选项卡"""
        widget = QWidget()
        layout = QVBoxLayout(widget)

        # 外观组
        appearance_group = QGroupBox("外观")
        appearance_layout = QFormLayout()

        # 主题
        self.theme_combo = QComboBox()
        self.theme_combo.addItems(["亮色主题", "暗色主题", "跟随系统"])
        appearance_layout.addRow("主题:", self.theme_combo)

        # 字体大小
        self.font_size_spin = QSpinBox()
        self.font_size_spin.setRange(8, 16)
        self.font_size_spin.setSuffix(" pt")
        appearance_layout.addRow("字体大小:", self.font_size_spin)

        appearance_group.setLayout(appearance_layout)
        layout.addWidget(appearance_group)

        # 窗口组
        window_group = QGroupBox("窗口")
        window_layout = QFormLayout()

        # 启动时最大化
        self.start_maximized_check = QCheckBox("启动时窗口最大化")
        window_layout.addRow("", self.start_maximized_check)

        # 记住窗口位置
        self.remember_position_check = QCheckBox("记住窗口位置和大小")
        window_layout.addRow("", self.remember_position_check)

        # 显示状态栏
        self.show_statusbar_check = QCheckBox("显示状态栏")
        window_layout.addRow("", self.show_statusbar_check)

        window_group.setLayout(window_layout)
        layout.addWidget(window_group)

        # 交互组
        interaction_group = QGroupBox("交互")
        interaction_layout = QFormLayout()

        # 确认退出
        self.confirm_exit_check = QCheckBox("退出时显示确认对话框")
        interaction_layout.addRow("", self.confirm_exit_check)

        # 拖放支持
        self.drag_drop_check = QCheckBox("启用文件拖放")
        interaction_layout.addRow("", self.drag_drop_check)

        # 最近文件数
        self.recent_files_spin = QSpinBox()
        self.recent_files_spin.setRange(5, 20)
        self.recent_files_spin.setSuffix(" 个")
        interaction_layout.addRow("最近文件数:", self.recent_files_spin)

        interaction_group.setLayout(interaction_layout)
        layout.addWidget(interaction_group)

        layout.addStretch()

        return widget

    def _create_advanced_tab(self):
        """创建高级设置选项卡"""
        widget = QWidget()
        layout = QVBoxLayout(widget)

        # 日志组
        log_group = QGroupBox("日志")
        log_layout = QFormLayout()

        # 日志级别
        self.log_level_combo = QComboBox()
        self.log_level_combo.addItems(["DEBUG", "INFO", "WARNING", "ERROR"])
        log_layout.addRow("日志级别:", self.log_level_combo)

        # 日志文件
        log_file_layout = QHBoxLayout()
        self.log_file_edit = QLineEdit()
        self.log_file_edit.setReadOnly(True)
        browse_log_btn = QPushButton("浏览...")
        browse_log_btn.clicked.connect(self._browse_log_file)

        log_file_layout.addWidget(self.log_file_edit)
        log_file_layout.addWidget(browse_log_btn)

        log_layout.addRow("日志文件:", log_file_layout)

        log_group.setLayout(log_layout)
        layout.addWidget(log_group)

        # 数据组
        data_group = QGroupBox("数据管理")
        data_layout = QVBoxLayout()

        # 清除缓存按钮
        clear_cache_btn = QPushButton("清除翻译缓存")
        clear_cache_btn.clicked.connect(self._clear_cache)
        data_layout.addWidget(clear_cache_btn)

        # 重置设置按钮
        reset_settings_btn = QPushButton("恢复默认设置")
        reset_settings_btn.clicked.connect(self._reset_settings)
        data_layout.addWidget(reset_settings_btn)

        data_group.setLayout(data_layout)
        layout.addWidget(data_group)

        # 环境变量组
        env_group = QGroupBox("环境变量")
        env_layout = QFormLayout()

        # DASHSCOPE_API_KEY
        env_key = os.getenv('DASHSCOPE_API_KEY', '(未设置)')
        env_label = QLabel(env_key[:20] + '...' if len(env_key) > 20 else env_key)
        env_label.setStyleSheet("font-family: monospace;")
        env_layout.addRow("DASHSCOPE_API_KEY:", env_label)

        env_group.setLayout(env_layout)
        layout.addWidget(env_group)

        layout.addStretch()

        return widget

    def _load_settings(self):
        """加载设置"""
        # 阿里云百炼设置
        self.api_key_edit.setText(
            os.getenv('DASHSCOPE_API_KEY', self.config.get('api.api_key', ''))
        )

        model = self.config.get('api.model', 'qwen-plus')
        model_index = {'qwen-plus': 0, 'qwen-turbo': 1, 'qwen-max': 2}.get(model, 0)
        self.model_combo.setCurrentIndex(model_index)

        self.endpoint_edit.setText(
            self.config.get('api.endpoint', 'https://dashscope.aliyuncs.com')
        )
        self.timeout_spin.setValue(self.config.get('api.timeout', 60))
        self.retry_spin.setValue(self.config.get('api.max_retries', 3))

        # 翻译设置
        self.batch_size_spin.setValue(self.config.get('translation.batch_size', 50))
        self.cache_enabled_check.setChecked(self.config.get('translation.cache_enabled', True))
        self.cache_ttl_spin.setValue(self.config.get('translation.cache_ttl_days', 7))

        # 性能设置
        self.spatial_index_check.setChecked(self.config.get('performance.spatial_index', True))
        self.antialiasing_check.setChecked(self.config.get('performance.antialiasing', True))
        self.entity_threshold_spin.setValue(self.config.get('performance.entity_threshold', 100))
        self.memory_threshold_spin.setValue(self.config.get('performance.memory_threshold_mb', 500))
        self.auto_optimize_check.setChecked(self.config.get('performance.auto_optimize', True))
        self.perf_monitor_check.setChecked(self.config.get('performance.monitor_enabled', False))
        self.perf_history_spin.setValue(self.config.get('performance.monitor_history', 100))

        # UI设置
        theme_index = self.config.get('ui.theme', 0)
        self.theme_combo.setCurrentIndex(theme_index)
        self.font_size_spin.setValue(self.config.get('ui.font_size', 9))
        self.start_maximized_check.setChecked(self.config.get('ui.start_maximized', False))
        self.remember_position_check.setChecked(self.config.get('ui.remember_position', True))
        self.show_statusbar_check.setChecked(self.config.get('ui.show_statusbar', True))
        self.confirm_exit_check.setChecked(self.config.get('ui.confirm_exit', True))
        self.drag_drop_check.setChecked(self.config.get('ui.drag_drop', True))
        self.recent_files_spin.setValue(self.config.get('ui.recent_files_count', 10))

        # 高级设置
        log_level = self.config.get('logging.level', 'INFO')
        level_index = {'DEBUG': 0, 'INFO': 1, 'WARNING': 2, 'ERROR': 3}.get(log_level, 1)
        self.log_level_combo.setCurrentIndex(level_index)
        self.log_file_edit.setText(self.config.get('logging.file', 'logs/app.log'))

    def _save_settings(self):
        """保存设置"""
        # 阿里云百炼设置
        api_key = self.api_key_edit.text().strip()
        if api_key:
            os.environ['DASHSCOPE_API_KEY'] = api_key
            self.config.set('api.api_key', api_key)

        model_names = ['qwen-plus', 'qwen-turbo', 'qwen-max']
        self.config.set('api.model', model_names[self.model_combo.currentIndex()])
        self.config.set('api.endpoint', self.endpoint_edit.text())
        self.config.set('api.timeout', self.timeout_spin.value())
        self.config.set('api.max_retries', self.retry_spin.value())

        # 翻译设置
        self.config.set('translation.batch_size', self.batch_size_spin.value())
        self.config.set('translation.cache_enabled', self.cache_enabled_check.isChecked())
        self.config.set('translation.cache_ttl_days', self.cache_ttl_spin.value())

        # 性能设置
        self.config.set('performance.spatial_index', self.spatial_index_check.isChecked())
        self.config.set('performance.antialiasing', self.antialiasing_check.isChecked())
        self.config.set('performance.entity_threshold', self.entity_threshold_spin.value())
        self.config.set('performance.memory_threshold_mb', self.memory_threshold_spin.value())
        self.config.set('performance.auto_optimize', self.auto_optimize_check.isChecked())
        self.config.set('performance.monitor_enabled', self.perf_monitor_check.isChecked())
        self.config.set('performance.monitor_history', self.perf_history_spin.value())

        # UI设置
        self.config.set('ui.theme', self.theme_combo.currentIndex())
        self.config.set('ui.font_size', self.font_size_spin.value())
        self.config.set('ui.start_maximized', self.start_maximized_check.isChecked())
        self.config.set('ui.remember_position', self.remember_position_check.isChecked())
        self.config.set('ui.show_statusbar', self.show_statusbar_check.isChecked())
        self.config.set('ui.confirm_exit', self.confirm_exit_check.isChecked())
        self.config.set('ui.drag_drop', self.drag_drop_check.isChecked())
        self.config.set('ui.recent_files_count', self.recent_files_spin.value())

        # 高级设置
        log_levels = ['DEBUG', 'INFO', 'WARNING', 'ERROR']
        self.config.set('logging.level', log_levels[self.log_level_combo.currentIndex()])
        self.config.set('logging.file', self.log_file_edit.text())

        # 保存配置
        self.config.save()

        logger.info("设置已保存")

    def _test_api_connection(self):
        """测试API连接"""
        try:
            from ..services.bailian_client import BailianClient

            api_key = self.api_key_edit.text().strip()
            if not api_key:
                QMessageBox.warning(self, "警告", "请先输入API密钥")
                return

            model_names = ['qwen-plus', 'qwen-turbo', 'qwen-max']
            model = model_names[self.model_combo.currentIndex()]

            # 临时设置API密钥
            os.environ['DASHSCOPE_API_KEY'] = api_key

            client = BailianClient(api_key=api_key, model=model)

            if client.test_connection():
                QMessageBox.information(
                    self,
                    "测试成功",
                    f"API连接测试成功！\n\n"
                    f"模型: {model}\n"
                    f"端点: {self.endpoint_edit.text()}"
                )
            else:
                QMessageBox.warning(self, "测试失败", "API连接测试失败，请检查配置")

        except Exception as e:
            QMessageBox.critical(self, "错误", f"测试失败:\n{str(e)}")

    def _browse_log_file(self):
        """浏览日志文件"""
        file_path, _ = QFileDialog.getSaveFileName(
            self,
            "选择日志文件",
            "",
            "日志文件 (*.log);;所有文件 (*.*)"
        )

        if file_path:
            self.log_file_edit.setText(file_path)

    def _clear_cache(self):
        """清除缓存"""
        reply = QMessageBox.question(
            self,
            "确认",
            "确定要清除所有翻译缓存吗？\n这将删除所有已缓存的翻译结果。",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No
        )

        if reply == QMessageBox.StandardButton.Yes:
            try:
                from ..translation.cache import TranslationCache
                cache = TranslationCache()
                cache.clear()
                QMessageBox.information(self, "成功", "缓存已清除")
                logger.info("翻译缓存已清除")
            except Exception as e:
                QMessageBox.critical(self, "错误", f"清除缓存失败:\n{str(e)}")

    def _reset_settings(self):
        """重置设置"""
        reply = QMessageBox.question(
            self,
            "确认",
            "确定要恢复所有默认设置吗？\n这将重置所有配置（不包括API密钥）。",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No
        )

        if reply == QMessageBox.StandardButton.Yes:
            # 保存API密钥
            api_key = self.api_key_edit.text()

            # 重置配置
            self.config.config = {}

            # 恢复API密钥
            if api_key:
                self.config.set('api.api_key', api_key)

            self.config.save()

            # 重新加载
            self._load_settings()

            QMessageBox.information(self, "成功", "已恢复默认设置")
            logger.info("设置已重置为默认值")

    def _on_ok(self):
        """确定按钮"""
        self._save_settings()
        self.accept()

    def _on_apply(self):
        """应用按钮"""
        self._save_settings()
        QMessageBox.information(self, "提示", "设置已应用，部分设置需要重启应用后生效")
