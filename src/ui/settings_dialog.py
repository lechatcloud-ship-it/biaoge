"""
完整的设置对话框 - 符合国内商业软件标准
"""
from PyQt6.QtWidgets import (
    QDialog, QVBoxLayout, QHBoxLayout, QFormLayout,
    QTabWidget, QWidget, QPushButton, QLineEdit,
    QComboBox, QSpinBox, QCheckBox, QLabel,
    QGroupBox, QMessageBox, QFileDialog, QTextEdit,
    QRadioButton, QButtonGroup
)
from PyQt6.QtCore import Qt
from pathlib import Path
import os

from ..utils.config_manager import ConfigManager
from ..utils.logger import logger


class SettingsDialog(QDialog):
    """完整设置对话框 - 国内商业软件标准"""

    def __init__(self, parent=None):
        super().__init__(parent)

        self.config = ConfigManager()
        self.setWindowTitle("设置")
        self.setMinimumSize(800, 700)
        self.setModal(True)

        self._init_ui()
        self._load_settings()

    def _init_ui(self):
        """初始化UI"""
        layout = QVBoxLayout(self)

        # 创建选项卡
        tab_widget = QTabWidget()

        # 1. 阿里云百炼设置
        tab_widget.addTab(self._create_bailian_tab(), "🤖 阿里云百炼")

        # 2. 翻译设置
        tab_widget.addTab(self._create_translation_tab(), "🌐 翻译设置")

        # 3. 性能设置
        tab_widget.addTab(self._create_performance_tab(), "⚡ 性能优化")

        # 4. 界面设置
        tab_widget.addTab(self._create_ui_tab(), "🎨 界面设置")

        # 5. 数据管理
        tab_widget.addTab(self._create_data_tab(), "💾 数据管理")

        # 6. 高级设置
        tab_widget.addTab(self._create_advanced_tab(), "🔧 高级")

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

        # 模型配置组
        model_group = QGroupBox("模型配置")
        model_layout = QVBoxLayout()

        # 多模态模型
        multimodal_layout = QFormLayout()
        self.multimodal_combo = QComboBox()
        self.multimodal_combo.addItems([
            "qwen-vl-max (多模态-最强) - ¥0.020/1K tokens",
            "qwen-vl-plus (多模态-推荐) - ¥0.008/1K tokens",
            "qwen-max (通用最强) - ¥0.040/1K tokens"
        ])
        multimodal_layout.addRow("多模态模型:", self.multimodal_combo)
        model_layout.addLayout(multimodal_layout)

        # 图片翻译模型
        image_layout = QFormLayout()
        self.image_model_combo = QComboBox()
        self.image_model_combo.addItems([
            "qwen-vl-max (图片识别-最强) - ¥0.020/1K tokens",
            "qwen-vl-plus (图片识别-推荐) - ¥0.008/1K tokens",
            "qwen-mt-image (专用图片翻译) - ¥0.012/1K tokens"
        ])
        image_layout.addRow("图片翻译:", self.image_model_combo)
        model_layout.addLayout(image_layout)

        # 文本翻译模型
        text_layout = QFormLayout()
        self.text_model_combo = QComboBox()
        self.text_model_combo.addItems([
            "qwen-mt-plus (翻译专用-推荐) - ¥0.006/1K tokens",
            "qwen-mt-turbo (翻译专用-快速) - ¥0.003/1K tokens",
            "qwen-plus (通用-推荐) - ¥0.004/1K tokens",
            "qwen-turbo (通用-快速) - ¥0.002/1K tokens",
            "qwen-max (通用-最强) - ¥0.040/1K tokens"
        ])
        text_layout.addRow("文本翻译:", self.text_model_combo)
        model_layout.addLayout(text_layout)

        # 自定义模型
        custom_layout = QFormLayout()

        self.use_custom_model = QCheckBox("使用自定义模型")
        self.use_custom_model.toggled.connect(self._on_custom_model_toggled)
        custom_layout.addRow("", self.use_custom_model)

        self.custom_model_edit = QLineEdit()
        self.custom_model_edit.setPlaceholderText("输入自定义模型名称，如: qwen-max-0428")
        self.custom_model_edit.setEnabled(False)
        custom_layout.addRow("自定义模型:", self.custom_model_edit)

        custom_help = QLabel(
            "支持所有DashScope兼容的模型名称 | "
            '<a href="https://help.aliyun.com/zh/dashscope/developer-reference/model-square">查看模型列表</a>'
        )
        custom_help.setOpenExternalLinks(True)
        custom_help.setStyleSheet("color: #666; font-size: 11px;")
        custom_layout.addRow("", custom_help)

        model_layout.addLayout(custom_layout)

        model_group.setLayout(model_layout)
        layout.addWidget(model_group)

        # 测试按钮
        test_layout = QHBoxLayout()
        test_layout.addStretch()

        test_btn = QPushButton("测试连接")
        test_btn.clicked.connect(self._test_api_connection)
        test_layout.addWidget(test_btn)

        layout.addLayout(test_layout)

        layout.addStretch()

        return widget

    def _create_translation_tab(self):
        """创建翻译设置选项卡"""
        widget = QWidget()
        layout = QVBoxLayout(widget)

        # 翻译引擎组
        engine_group = QGroupBox("翻译引擎配置")
        engine_layout = QFormLayout()

        # 批量大小
        self.batch_size_spin = QSpinBox()
        self.batch_size_spin.setRange(10, 200)
        self.batch_size_spin.setSuffix(" 条/批")
        engine_layout.addRow("批量翻译大小:", self.batch_size_spin)

        # 并发数
        self.concurrent_spin = QSpinBox()
        self.concurrent_spin.setRange(1, 10)
        self.concurrent_spin.setSuffix(" 个线程")
        engine_layout.addRow("并发翻译线程:", self.concurrent_spin)

        engine_group.setLayout(engine_layout)
        layout.addWidget(engine_group)

        # 缓存设置组
        cache_group = QGroupBox("智能缓存")
        cache_layout = QFormLayout()

        # 缓存启用
        self.cache_enabled_check = QCheckBox("启用翻译缓存（可节省90%+成本）")
        self.cache_enabled_check.setChecked(True)
        cache_layout.addRow("", self.cache_enabled_check)

        # 缓存TTL
        self.cache_ttl_spin = QSpinBox()
        self.cache_ttl_spin.setRange(1, 365)
        self.cache_ttl_spin.setSuffix(" 天")
        cache_layout.addRow("缓存有效期:", self.cache_ttl_spin)

        # 自动清理
        self.auto_cleanup_check = QCheckBox("自动清理过期缓存")
        self.auto_cleanup_check.setChecked(True)
        cache_layout.addRow("", self.auto_cleanup_check)

        cache_group.setLayout(cache_layout)
        layout.addWidget(cache_group)

        # 质量设置组
        quality_group = QGroupBox("翻译质量")
        quality_layout = QFormLayout()

        # 上下文窗口
        self.context_window_spin = QSpinBox()
        self.context_window_spin.setRange(0, 10)
        self.context_window_spin.setSuffix(" 条")
        quality_layout.addRow("上下文窗口:", self.context_window_spin)

        # 专业术语库
        self.use_terminology_check = QCheckBox("使用专业术语库")
        self.use_terminology_check.setChecked(True)
        quality_layout.addRow("", self.use_terminology_check)

        # 后处理
        self.post_process_check = QCheckBox("启用后处理优化")
        self.post_process_check.setChecked(True)
        quality_layout.addRow("", self.post_process_check)

        quality_group.setLayout(quality_layout)
        layout.addWidget(quality_group)

        # 语言对设置
        lang_group = QGroupBox("默认语言对")
        lang_layout = QFormLayout()

        self.default_source_combo = QComboBox()
        self.default_source_combo.addItems([
            "自动检测", "中文", "英文", "日文", "韩文",
            "法文", "德文", "西班牙文", "俄文"
        ])
        lang_layout.addRow("默认源语言:", self.default_source_combo)

        self.default_target_combo = QComboBox()
        self.default_target_combo.addItems([
            "英文", "中文", "日文", "韩文",
            "法文", "德文", "西班牙文", "俄文"
        ])
        lang_layout.addRow("默认目标语言:", self.default_target_combo)

        lang_group.setLayout(lang_layout)
        layout.addWidget(lang_group)

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

        # 帧率限制
        self.fps_limit_spin = QSpinBox()
        self.fps_limit_spin.setRange(30, 144)
        self.fps_limit_spin.setSuffix(" FPS")
        render_layout.addRow("最大帧率:", self.fps_limit_spin)

        render_group.setLayout(render_layout)
        layout.addWidget(render_group)

        # 内存管理组
        memory_group = QGroupBox("内存管理")
        memory_layout = QFormLayout()

        # 内存阈值
        self.memory_threshold_spin = QSpinBox()
        self.memory_threshold_spin.setRange(100, 4000)
        self.memory_threshold_spin.setSingleStep(50)
        self.memory_threshold_spin.setSuffix(" MB")
        memory_layout.addRow("内存警告阈值:", self.memory_threshold_spin)

        # 自动优化
        self.auto_optimize_check = QCheckBox("内存超限自动优化")
        memory_layout.addRow("", self.auto_optimize_check)

        # 缓存大小
        self.cache_size_spin = QSpinBox()
        self.cache_size_spin.setRange(10, 1000)
        self.cache_size_spin.setSuffix(" MB")
        memory_layout.addRow("渲染缓存大小:", self.cache_size_spin)

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

        # 性能报告
        self.perf_report_check = QCheckBox("生成性能报告")
        monitor_layout.addRow("", self.perf_report_check)

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
        self.theme_combo.addItems(["亮色主题", "暗色主题", "跟随系统", "蓝色主题", "绿色主题"])
        appearance_layout.addRow("主题:", self.theme_combo)

        # 字体大小
        self.font_size_spin = QSpinBox()
        self.font_size_spin.setRange(8, 18)
        self.font_size_spin.setSuffix(" pt")
        appearance_layout.addRow("字体大小:", self.font_size_spin)

        # 字体
        self.font_family_combo = QComboBox()
        self.font_family_combo.addItems([
            "微软雅黑", "宋体", "黑体", "Arial", "Consolas"
        ])
        appearance_layout.addRow("字体:", self.font_family_combo)

        # UI缩放
        self.ui_scale_spin = QSpinBox()
        self.ui_scale_spin.setRange(80, 150)
        self.ui_scale_spin.setSuffix(" %")
        appearance_layout.addRow("UI缩放:", self.ui_scale_spin)

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

        # 显示工具栏
        self.show_toolbar_check = QCheckBox("显示工具栏")
        window_layout.addRow("", self.show_toolbar_check)

        # 标签页位置
        self.tab_position_combo = QComboBox()
        self.tab_position_combo.addItems(["顶部", "底部", "左侧", "右侧"])
        window_layout.addRow("标签页位置:", self.tab_position_combo)

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
        self.recent_files_spin.setRange(5, 30)
        self.recent_files_spin.setSuffix(" 个")
        interaction_layout.addRow("最近文件数:", self.recent_files_spin)

        # 双击行为
        self.double_click_combo = QComboBox()
        self.double_click_combo.addItems(["打开文件", "预览", "编辑"])
        interaction_layout.addRow("双击文件:", self.double_click_combo)

        interaction_group.setLayout(interaction_layout)
        layout.addWidget(interaction_group)

        layout.addStretch()

        return widget

    def _create_data_tab(self):
        """创建数据管理选项卡"""
        widget = QWidget()
        layout = QVBoxLayout(widget)

        # 自动保存组
        autosave_group = QGroupBox("自动保存")
        autosave_layout = QFormLayout()

        self.autosave_enabled_check = QCheckBox("启用自动保存")
        autosave_layout.addRow("", self.autosave_enabled_check)

        self.autosave_interval_spin = QSpinBox()
        self.autosave_interval_spin.setRange(1, 60)
        self.autosave_interval_spin.setSuffix(" 分钟")
        autosave_layout.addRow("保存间隔:", self.autosave_interval_spin)

        autosave_group.setLayout(autosave_layout)
        layout.addWidget(autosave_group)

        # 备份设置组
        backup_group = QGroupBox("数据备份")
        backup_layout = QVBoxLayout()

        backup_form = QFormLayout()

        self.backup_enabled_check = QCheckBox("启用自动备份")
        backup_form.addRow("", self.backup_enabled_check)

        self.backup_path_edit = QLineEdit()
        self.backup_path_edit.setPlaceholderText("选择备份目录")
        browse_backup_btn = QPushButton("浏览...")
        browse_backup_btn.clicked.connect(self._browse_backup_path)

        backup_path_layout = QHBoxLayout()
        backup_path_layout.addWidget(self.backup_path_edit)
        backup_path_layout.addWidget(browse_backup_btn)
        backup_form.addRow("备份目录:", backup_path_layout)

        self.backup_count_spin = QSpinBox()
        self.backup_count_spin.setRange(1, 100)
        self.backup_count_spin.setSuffix(" 个")
        backup_form.addRow("保留备份数:", self.backup_count_spin)

        backup_layout.addLayout(backup_form)

        # 备份操作按钮
        backup_btn_layout = QHBoxLayout()
        backup_now_btn = QPushButton("立即备份")
        backup_now_btn.clicked.connect(self._backup_now)
        restore_btn = QPushButton("恢复备份")
        restore_btn.clicked.connect(self._restore_backup)

        backup_btn_layout.addWidget(backup_now_btn)
        backup_btn_layout.addWidget(restore_btn)
        backup_btn_layout.addStretch()

        backup_layout.addLayout(backup_btn_layout)

        backup_group.setLayout(backup_layout)
        layout.addWidget(backup_group)

        # 数据清理组
        cleanup_group = QGroupBox("数据清理")
        cleanup_layout = QVBoxLayout()

        # 清理按钮
        clear_cache_btn = QPushButton("清除翻译缓存")
        clear_cache_btn.clicked.connect(self._clear_cache)
        cleanup_layout.addWidget(clear_cache_btn)

        clear_logs_btn = QPushButton("清除日志文件")
        clear_logs_btn.clicked.connect(self._clear_logs)
        cleanup_layout.addWidget(clear_logs_btn)

        clear_temp_btn = QPushButton("清除临时文件")
        clear_temp_btn.clicked.connect(self._clear_temp)
        cleanup_layout.addWidget(clear_temp_btn)

        cleanup_group.setLayout(cleanup_layout)
        layout.addWidget(cleanup_group)

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

        # 日志大小限制
        self.log_size_spin = QSpinBox()
        self.log_size_spin.setRange(1, 100)
        self.log_size_spin.setSuffix(" MB")
        log_layout.addRow("日志文件大小:", self.log_size_spin)

        log_group.setLayout(log_layout)
        layout.addWidget(log_group)

        # 更新设置组
        update_group = QGroupBox("软件更新")
        update_layout = QFormLayout()

        self.auto_check_update_check = QCheckBox("启动时自动检查更新")
        update_layout.addRow("", self.auto_check_update_check)

        update_channel_combo = QComboBox()
        update_channel_combo.addItems(["稳定版", "测试版", "开发版"])
        update_layout.addRow("更新通道:", update_channel_combo)

        check_update_btn = QPushButton("检查更新")
        check_update_btn.clicked.connect(self._check_update)
        update_layout.addRow("", check_update_btn)

        update_group.setLayout(update_layout)
        layout.addWidget(update_group)

        # 使用统计组
        stats_group = QGroupBox("使用统计")
        stats_layout = QFormLayout()

        self.enable_stats_check = QCheckBox("帮助我们改进产品（匿名统计）")
        stats_layout.addRow("", self.enable_stats_check)

        view_stats_btn = QPushButton("查看统计数据")
        view_stats_btn.clicked.connect(self._view_stats)
        stats_layout.addRow("", view_stats_btn)

        stats_group.setLayout(stats_layout)
        layout.addWidget(stats_group)

        # 重置设置组
        reset_group = QGroupBox("重置")
        reset_layout = QVBoxLayout()

        reset_settings_btn = QPushButton("恢复默认设置")
        reset_settings_btn.clicked.connect(self._reset_settings)
        reset_layout.addWidget(reset_settings_btn)

        reset_all_btn = QPushButton("重置所有数据（包括缓存）")
        reset_all_btn.clicked.connect(self._reset_all)
        reset_layout.addWidget(reset_all_btn)

        reset_group.setLayout(reset_layout)
        layout.addWidget(reset_group)

        # 环境变量组
        env_group = QGroupBox("环境信息")
        env_layout = QFormLayout()

        # DASHSCOPE_API_KEY
        env_key = os.getenv('DASHSCOPE_API_KEY', '(未设置)')
        env_label = QLabel(env_key[:20] + '...' if len(env_key) > 20 else env_key)
        env_label.setStyleSheet("font-family: monospace;")
        env_layout.addRow("DASHSCOPE_API_KEY:", env_label)

        # 配置目录
        config_dir = Path.home() / ".biaoge"
        config_label = QLabel(str(config_dir))
        config_label.setStyleSheet("font-family: monospace; font-size: 10px;")
        env_layout.addRow("配置目录:", config_label)

        env_group.setLayout(env_layout)
        layout.addWidget(env_group)

        layout.addStretch()

        return widget

    def _on_custom_model_toggled(self, checked):
        """自定义模型切换"""
        self.custom_model_edit.setEnabled(checked)
        if checked:
            self.multimodal_combo.setEnabled(False)
            self.image_model_combo.setEnabled(False)
            self.text_model_combo.setEnabled(False)
        else:
            self.multimodal_combo.setEnabled(True)
            self.image_model_combo.setEnabled(True)
            self.text_model_combo.setEnabled(True)

    def _load_settings(self):
        """加载设置"""
        # 阿里云百炼设置
        self.api_key_edit.setText(
            os.getenv('DASHSCOPE_API_KEY', self.config.get('api.api_key', ''))
        )

        # 模型设置
        multimodal = self.config.get('api.multimodal_model', 'qwen-vl-plus')
        multimodal_index = {'qwen-vl-max': 0, 'qwen-vl-plus': 1, 'qwen-max': 2}.get(multimodal, 1)
        self.multimodal_combo.setCurrentIndex(multimodal_index)

        image_model = self.config.get('api.image_model', 'qwen-vl-plus')
        image_index = {'qwen-vl-max': 0, 'qwen-vl-plus': 1, 'qwen-mt-image': 2}.get(image_model, 1)
        self.image_model_combo.setCurrentIndex(image_index)

        text_model = self.config.get('api.text_model', 'qwen-mt-plus')
        text_models = ['qwen-mt-plus', 'qwen-mt-turbo', 'qwen-plus', 'qwen-turbo', 'qwen-max']
        text_index = text_models.index(text_model) if text_model in text_models else 0
        self.text_model_combo.setCurrentIndex(text_index)

        # 自定义模型
        use_custom = self.config.get('api.use_custom_model', False)
        self.use_custom_model.setChecked(use_custom)
        self.custom_model_edit.setText(self.config.get('api.custom_model', ''))

        self.endpoint_edit.setText(
            self.config.get('api.endpoint', 'https://dashscope.aliyuncs.com')
        )
        self.timeout_spin.setValue(self.config.get('api.timeout', 60))
        self.retry_spin.setValue(self.config.get('api.max_retries', 3))

        # 翻译设置
        self.batch_size_spin.setValue(self.config.get('translation.batch_size', 50))
        self.concurrent_spin.setValue(self.config.get('translation.concurrent', 3))
        self.cache_enabled_check.setChecked(self.config.get('translation.cache_enabled', True))
        self.cache_ttl_spin.setValue(self.config.get('translation.cache_ttl_days', 7))
        self.auto_cleanup_check.setChecked(self.config.get('translation.auto_cleanup', True))
        self.context_window_spin.setValue(self.config.get('translation.context_window', 3))
        self.use_terminology_check.setChecked(self.config.get('translation.use_terminology', True))
        self.post_process_check.setChecked(self.config.get('translation.post_process', True))

        # 性能设置
        self.spatial_index_check.setChecked(self.config.get('performance.spatial_index', True))
        self.antialiasing_check.setChecked(self.config.get('performance.antialiasing', True))
        self.entity_threshold_spin.setValue(self.config.get('performance.entity_threshold', 100))
        self.fps_limit_spin.setValue(self.config.get('performance.fps_limit', 60))
        self.memory_threshold_spin.setValue(self.config.get('performance.memory_threshold_mb', 500))
        self.auto_optimize_check.setChecked(self.config.get('performance.auto_optimize', True))
        self.cache_size_spin.setValue(self.config.get('performance.cache_size_mb', 100))
        self.perf_monitor_check.setChecked(self.config.get('performance.monitor_enabled', False))
        self.perf_history_spin.setValue(self.config.get('performance.monitor_history', 100))
        self.perf_report_check.setChecked(self.config.get('performance.generate_report', False))

        # UI设置
        theme_index = self.config.get('ui.theme', 0)
        self.theme_combo.setCurrentIndex(theme_index)
        self.font_size_spin.setValue(self.config.get('ui.font_size', 9))
        self.font_family_combo.setCurrentText(self.config.get('ui.font_family', '微软雅黑'))
        self.ui_scale_spin.setValue(self.config.get('ui.scale', 100))
        self.start_maximized_check.setChecked(self.config.get('ui.start_maximized', False))
        self.remember_position_check.setChecked(self.config.get('ui.remember_position', True))
        self.show_statusbar_check.setChecked(self.config.get('ui.show_statusbar', True))
        self.show_toolbar_check.setChecked(self.config.get('ui.show_toolbar', True))
        self.tab_position_combo.setCurrentIndex(self.config.get('ui.tab_position', 0))
        self.confirm_exit_check.setChecked(self.config.get('ui.confirm_exit', True))
        self.drag_drop_check.setChecked(self.config.get('ui.drag_drop', True))
        self.recent_files_spin.setValue(self.config.get('ui.recent_files_count', 10))
        self.double_click_combo.setCurrentIndex(self.config.get('ui.double_click_action', 0))

        # 数据管理
        self.autosave_enabled_check.setChecked(self.config.get('data.autosave_enabled', True))
        self.autosave_interval_spin.setValue(self.config.get('data.autosave_interval', 5))
        self.backup_enabled_check.setChecked(self.config.get('data.backup_enabled', False))
        self.backup_path_edit.setText(self.config.get('data.backup_path', str(Path.home() / "biaoge_backup")))
        self.backup_count_spin.setValue(self.config.get('data.backup_count', 5))

        # 高级设置
        log_level = self.config.get('logging.level', 'INFO')
        level_index = {'DEBUG': 0, 'INFO': 1, 'WARNING': 2, 'ERROR': 3}.get(log_level, 1)
        self.log_level_combo.setCurrentIndex(level_index)
        self.log_file_edit.setText(self.config.get('logging.file', 'logs/app.log'))
        self.log_size_spin.setValue(self.config.get('logging.max_size_mb', 10))
        self.auto_check_update_check.setChecked(self.config.get('update.auto_check', True))
        self.enable_stats_check.setChecked(self.config.get('stats.enabled', True))

    def _save_settings(self):
        """保存设置 - 完整版本"""
        # API设置
        api_key = self.api_key_edit.text().strip()
        if api_key:
            os.environ['DASHSCOPE_API_KEY'] = api_key
            self.config.set('api.api_key', api_key)

        # 模型设置
        multimodal_models = ['qwen-vl-max', 'qwen-vl-plus', 'qwen-max']
        self.config.set('api.multimodal_model', multimodal_models[self.multimodal_combo.currentIndex()])

        image_models = ['qwen-vl-max', 'qwen-vl-plus', 'qwen-mt-image']
        self.config.set('api.image_model', image_models[self.image_model_combo.currentIndex()])

        text_models = ['qwen-mt-plus', 'qwen-mt-turbo', 'qwen-plus', 'qwen-turbo', 'qwen-max']
        self.config.set('api.text_model', text_models[self.text_model_combo.currentIndex()])

        # 自定义模型
        self.config.set('api.use_custom_model', self.use_custom_model.isChecked())
        self.config.set('api.custom_model', self.custom_model_edit.text())

        self.config.set('api.endpoint', self.endpoint_edit.text())
        self.config.set('api.timeout', self.timeout_spin.value())
        self.config.set('api.max_retries', self.retry_spin.value())

        # 翻译设置
        self.config.set('translation.batch_size', self.batch_size_spin.value())
        self.config.set('translation.concurrent', self.concurrent_spin.value())
        self.config.set('translation.cache_enabled', self.cache_enabled_check.isChecked())
        self.config.set('translation.cache_ttl_days', self.cache_ttl_spin.value())
        self.config.set('translation.auto_cleanup', self.auto_cleanup_check.isChecked())
        self.config.set('translation.context_window', self.context_window_spin.value())
        self.config.set('translation.use_terminology', self.use_terminology_check.isChecked())
        self.config.set('translation.post_process', self.post_process_check.isChecked())

        # 性能设置
        self.config.set('performance.spatial_index', self.spatial_index_check.isChecked())
        self.config.set('performance.antialiasing', self.antialiasing_check.isChecked())
        self.config.set('performance.entity_threshold', self.entity_threshold_spin.value())
        self.config.set('performance.fps_limit', self.fps_limit_spin.value())
        self.config.set('performance.memory_threshold_mb', self.memory_threshold_spin.value())
        self.config.set('performance.auto_optimize', self.auto_optimize_check.isChecked())
        self.config.set('performance.cache_size_mb', self.cache_size_spin.value())
        self.config.set('performance.monitor_enabled', self.perf_monitor_check.isChecked())
        self.config.set('performance.monitor_history', self.perf_history_spin.value())
        self.config.set('performance.generate_report', self.perf_report_check.isChecked())

        # UI设置
        self.config.set('ui.theme', self.theme_combo.currentIndex())
        self.config.set('ui.font_size', self.font_size_spin.value())
        self.config.set('ui.font_family', self.font_family_combo.currentText())
        self.config.set('ui.scale', self.ui_scale_spin.value())
        self.config.set('ui.start_maximized', self.start_maximized_check.isChecked())
        self.config.set('ui.remember_position', self.remember_position_check.isChecked())
        self.config.set('ui.show_statusbar', self.show_statusbar_check.isChecked())
        self.config.set('ui.show_toolbar', self.show_toolbar_check.isChecked())
        self.config.set('ui.tab_position', self.tab_position_combo.currentIndex())
        self.config.set('ui.confirm_exit', self.confirm_exit_check.isChecked())
        self.config.set('ui.drag_drop', self.drag_drop_check.isChecked())
        self.config.set('ui.recent_files_count', self.recent_files_spin.value())
        self.config.set('ui.double_click_action', self.double_click_combo.currentIndex())

        # 数据管理
        self.config.set('data.autosave_enabled', self.autosave_enabled_check.isChecked())
        self.config.set('data.autosave_interval', self.autosave_interval_spin.value())
        self.config.set('data.backup_enabled', self.backup_enabled_check.isChecked())
        self.config.set('data.backup_path', self.backup_path_edit.text())
        self.config.set('data.backup_count', self.backup_count_spin.value())

        # 高级设置
        log_levels = ['DEBUG', 'INFO', 'WARNING', 'ERROR']
        self.config.set('logging.level', log_levels[self.log_level_combo.currentIndex()])
        self.config.set('logging.file', self.log_file_edit.text())
        self.config.set('logging.max_size_mb', self.log_size_spin.value())
        self.config.set('update.auto_check', self.auto_check_update_check.isChecked())
        self.config.set('stats.enabled', self.enable_stats_check.isChecked())

        # 保存配置
        self.config.save()

        logger.info("设置已保存")

    # 事件处理方法
    def _test_api_connection(self):
        """测试API连接"""
        try:
            from ..services.bailian_client import BailianClient

            api_key = self.api_key_edit.text().strip()
            if not api_key:
                QMessageBox.warning(self, "警告", "请先输入API密钥")
                return

            # 确定使用的模型
            if self.use_custom_model.isChecked():
                model = self.custom_model_edit.text().strip()
            else:
                text_models = ['qwen-mt-plus', 'qwen-mt-turbo', 'qwen-plus', 'qwen-turbo', 'qwen-max']
                model = text_models[self.text_model_combo.currentIndex()]

            # 临时设置API密钥
            os.environ['DASHSCOPE_API_KEY'] = api_key

            client = BailianClient(api_key=api_key, model=model)

            if client.test_connection():
                QMessageBox.information(
                    self,
                    "测试成功",
                    f"✅ API连接测试成功！\n\n"
                    f"模型: {model}\n"
                    f"端点: {self.endpoint_edit.text()}\n\n"
                    f"您的配置已正确设置"
                )
            else:
                QMessageBox.warning(self, "测试失败", "API连接测试失败，请检查配置")

        except Exception as e:
            QMessageBox.critical(self, "错误", f"测试失败:\n{str(e)}")

    def _browse_backup_path(self):
        """浏览备份目录"""
        dir_path = QFileDialog.getExistingDirectory(
            self,
            "选择备份目录",
            self.backup_path_edit.text()
        )
        if dir_path:
            self.backup_path_edit.setText(dir_path)

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

    def _backup_now(self):
        """立即备份"""
        QMessageBox.information(self, "备份", "备份功能将在后续版本中实现")

    def _restore_backup(self):
        """恢复备份"""
        QMessageBox.information(self, "恢复", "恢复功能将在后续版本中实现")

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
                QMessageBox.information(self, "成功", "✅ 缓存已清除")
                logger.info("翻译缓存已清除")
            except Exception as e:
                QMessageBox.critical(self, "错误", f"清除缓存失败:\n{str(e)}")

    def _clear_logs(self):
        """清除日志"""
        QMessageBox.information(self, "清除日志", "日志清除功能将在后续版本中实现")

    def _clear_temp(self):
        """清除临时文件"""
        QMessageBox.information(self, "清除临时文件", "临时文件清除功能将在后续版本中实现")

    def _check_update(self):
        """检查更新"""
        QMessageBox.information(
            self,
            "检查更新",
            "当前版本: 1.0.0\n\n✅ 您使用的是最新版本！"
        )

    def _view_stats(self):
        """查看统计"""
        QMessageBox.information(self, "使用统计", "统计数据查看功能将在后续版本中实现")

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

            QMessageBox.information(self, "成功", "✅ 已恢复默认设置")
            logger.info("设置已重置为默认值")

    def _reset_all(self):
        """重置所有数据"""
        reply = QMessageBox.warning(
            self,
            "危险操作",
            "⚠️ 警告：此操作将删除所有数据！\n\n"
            "包括：\n"
            "- 所有配置\n"
            "- 翻译缓存\n"
            "- 日志文件\n"
            "- 临时文件\n\n"
            "确定要继续吗？",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
            QMessageBox.StandardButton.No
        )

        if reply == QMessageBox.StandardButton.Yes:
            QMessageBox.information(self, "提示", "数据重置功能将在后续版本中实现")

    def _on_ok(self):
        """确定按钮"""
        self._save_settings()
        self.accept()

    def _on_apply(self):
        """应用按钮"""
        self._save_settings()
        QMessageBox.information(
            self,
            "提示",
            "✅ 设置已应用\n\n部分设置需要重启应用后生效"
        )
