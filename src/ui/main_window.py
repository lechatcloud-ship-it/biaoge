# -*- coding: utf-8 -*-
"""
主窗口 - 完整商业版 (Fluent Design)
"""
from qfluentwidgets import (
    FluentWindow, NavigationItemPosition, FluentIcon,
    InfoBar, InfoBarPosition, setTheme, Theme,
    MessageBox, isDarkTheme,
    SmoothScrollArea, PrimaryPushButton, PushButton,
    CommandBar, TransparentToolButton
)
from PyQt6.QtWidgets import QWidget, QVBoxLayout, QHBoxLayout, QFileDialog
from PyQt6.QtGui import QAction, QKeySequence
from PyQt6.QtCore import Qt, pyqtSignal
from pathlib import Path
from typing import Optional

from ..dwg.parser import DWGParser, DWGParseError
from ..dwg.entities import DWGDocument
from .viewer import ViewerWidget
from .translation import TranslationInterface
from .calculation import CalculationInterface
from .export import ExportInterface
from .batch_widget import BatchWidget
from .settings_dialog import SettingsDialog
from ..ai.context_manager import ContextManager
from ..ai.ai_assistant import AIAssistant
from ..ai.assistant_widget import AIAssistantWidget
from .about import AboutDialog
from .log_viewer import LogViewerDialog
from .performance_panel import PerformancePanel
from ..utils.logger import logger
from ..utils.config_manager import ConfigManager
from ..utils.config_persistence import ConfigPersistence


class MainWindow(FluentWindow):
    """主窗口 (Fluent Design)"""

    documentLoaded = pyqtSignal(DWGDocument)

    def __init__(self):
        super().__init__()

        self.document: Optional[DWGDocument] = None
        self.current_file: Optional[Path] = None
        self.config = ConfigManager()
        self.app_state = ConfigPersistence()

        # 创建AI助手和上下文管理器
        self.context_manager = ContextManager()
        try:
            self.ai_assistant = AIAssistant(context_manager=self.context_manager)
        except Exception as e:
            logger.warning(f"AI助手初始化失败 (可能未配置API密钥): {e}")
            self.ai_assistant = None

        self._init_ui()
        self._connect_signals()
        self._restore_window_state()

        self.setAcceptDrops(True)

        # 设置Fluent主题
        setTheme(Theme.AUTO)
        logger.info("主窗口初始化完成 (Fluent Design)")

    def _init_ui(self):
        """初始化Fluent Design UI"""
        self.setWindowTitle("表哥 - DWG翻译计算软件 v1.0.0")
        self.setMinimumSize(1200, 800)
        self.resize(1400, 900)

        # 添加标题栏按钮 - 设置和关于
        self._add_title_bar_buttons()

        # 配置导航栏
        self.navigationInterface.setExpandWidth(200)
        self.navigationInterface.expand(useAni=False)

        # 创建主界面组件 - 包含查看器的主页
        self.home_page = self._create_home_page()

        # 创建各个功能界面
        self.translation_widget = TranslationInterface()
        self.translation_widget.setObjectName("translationInterface")

        self.calculation_widget = CalculationInterface()
        self.calculation_widget.setObjectName("calculationInterface")

        self.export_widget = ExportInterface()
        self.export_widget.setObjectName("exportInterface")

        self.batch_widget = BatchWidget()
        self.batch_widget.setObjectName("batchInterface")

        self.performance_panel = PerformancePanel()
        self.performance_panel.setObjectName("performancePanel")

        self.ai_assistant_widget = AIAssistantWidget(ai_assistant=self.ai_assistant)
        self.ai_assistant_widget.setObjectName("aiAssistantInterface")

        # 添加导航项 - 顶部
        self.addSubInterface(
            self.home_page,
            FluentIcon.HOME,
            "主页",
            NavigationItemPosition.TOP
        )

        self.addSubInterface(
            self.translation_widget,
            FluentIcon.LANGUAGE,
            "翻译",
            NavigationItemPosition.TOP
        )

        self.addSubInterface(
            self.calculation_widget,
            FluentIcon.CALCULATOR,
            "算量",
            NavigationItemPosition.TOP
        )

        self.addSubInterface(
            self.export_widget,
            FluentIcon.SAVE,
            "导出",
            NavigationItemPosition.TOP
        )

        self.addSubInterface(
            self.batch_widget,
            FluentIcon.FOLDER,
            "批量处理",
            NavigationItemPosition.TOP
        )

        # 添加导航项 - 底部
        self.addSubInterface(
            self.performance_panel,
            FluentIcon.SPEED_HIGH,
            "性能监控",
            NavigationItemPosition.BOTTOM
        )

        self.addSubInterface(
            self.ai_assistant_widget,
            FluentIcon.ROBOT,
            "AI助手",
            NavigationItemPosition.BOTTOM
        )

    def _create_home_page(self) -> QWidget:
        """创建主页 - 包含DWG查看器和快捷按钮"""
        home_widget = QWidget()
        home_widget.setObjectName("homePage")

        layout = QVBoxLayout(home_widget)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)

        # 创建顶部快捷按钮栏
        button_bar = self._create_quick_actions_bar()
        layout.addWidget(button_bar)

        # 创建查看器组件
        self.viewer_widget = ViewerWidget()
        layout.addWidget(self.viewer_widget)

        return home_widget

    def _create_quick_actions_bar(self) -> QWidget:
        """创建快捷操作栏"""
        bar_widget = QWidget()
        bar_widget.setObjectName("quickActionsBar")
        bar_widget.setStyleSheet("""
            QWidget#quickActionsBar {
                background-color: transparent;
                padding: 10px;
            }
        """)

        layout = QHBoxLayout(bar_widget)
        layout.setContentsMargins(10, 5, 10, 5)
        layout.setSpacing(10)

        # 打开文件按钮
        open_btn = PrimaryPushButton(FluentIcon.FOLDER, "打开DWG文件")
        open_btn.clicked.connect(self.onOpenFile)
        layout.addWidget(open_btn)

        layout.addSpacing(20)

        # 翻译按钮
        translate_btn = PushButton(FluentIcon.LANGUAGE, "智能翻译")
        translate_btn.setEnabled(False)  # 默认禁用，打开文件后启用
        translate_btn.clicked.connect(self.onQuickTranslate)
        self.quick_translate_btn = translate_btn
        layout.addWidget(translate_btn)

        # 算量按钮
        calc_btn = PushButton(FluentIcon.CALCULATOR, "智能算量")
        calc_btn.setEnabled(False)  # 默认禁用，打开文件后启用
        calc_btn.clicked.connect(self.onQuickCalculate)
        self.quick_calc_btn = calc_btn
        layout.addWidget(calc_btn)

        layout.addStretch()

        return bar_widget

    def _add_title_bar_buttons(self):
        """在标题栏添加设置和关于按钮"""
        # 添加设置按钮到标题栏
        settings_btn = TransparentToolButton(FluentIcon.SETTING, self)
        settings_btn.setToolTip("设置")
        settings_btn.clicked.connect(self.onSettings)
        self.titleBar.hBoxLayout.insertWidget(
            self.titleBar.hBoxLayout.count() - 1,
            settings_btn,
            0,
            Qt.AlignmentFlag.AlignVCenter
        )

        # 添加关于按钮
        about_btn = TransparentToolButton(FluentIcon.INFO, self)
        about_btn.setToolTip("关于")
        about_btn.clicked.connect(self.onAbout)
        self.titleBar.hBoxLayout.insertWidget(
            self.titleBar.hBoxLayout.count() - 1,
            about_btn,
            0,
            Qt.AlignmentFlag.AlignVCenter
        )

    def _connect_signals(self):
        """连接信号"""
        self.documentLoaded.connect(self.viewer_widget.setDocument)
        self.documentLoaded.connect(self.translation_widget.setDocument)
        self.documentLoaded.connect(self.calculation_widget.setDocument)
        self.documentLoaded.connect(self.export_widget.setDocument)
        # 连接到上下文管理器
        self.documentLoaded.connect(self._update_dwg_context)

        self.calculation_widget.parent_window = self
        self.export_widget.parent_window = self
        self.translation_widget.parent_window = self

    def _update_dwg_context(self, document: DWGDocument):
        """更新DWG上下文到AI助手"""
        try:
            from datetime import datetime
            self.context_manager.set_dwg_document(
                document,
                self.current_file.name if self.current_file else "未命名",
                str(self.current_file) if self.current_file else "",
                datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            )
            logger.info("DWG上下文已更新到AI助手")
        except Exception as e:
            logger.error(f"更新DWG上下文失败: {e}")

    def _restore_window_state(self):
        """恢复窗口状态"""
        try:
            state = self.app_state.load()
            if 'window_geometry' in state:
                geom = state['window_geometry']
                self.setGeometry(geom['x'], geom['y'], geom['width'], geom['height'])
            if state.get('window_maximized', False):
                self.showMaximized()
        except Exception as e:
            logger.warning(f"恢复窗口状态失败: {e}")

    def _save_window_state(self):
        """保存窗口状态"""
        try:
            state = {
                'window_geometry': {
                    'x': self.x(),
                    'y': self.y(),
                    'width': self.width(),
                    'height': self.height()
                },
                'window_maximized': self.isMaximized()
            }
            self.app_state.save(state)
        except Exception as e:
            logger.warning(f"保存窗口状态失败: {e}")

    def onOpenFile(self):
        """打开文件对话框"""
        file_path, _ = QFileDialog.getOpenFileName(
            self,
            "打开DWG文件",
            "",
            "DWG文件 (*.dwg *.dxf);;所有文件 (*.*)"
        )

        if file_path:
            self.openFile(file_path)

    def onBatchProcessing(self):
        """切换到批量处理界面"""
        self.switchTo(self.batch_widget)

    def openFile(self, file_path: str):
        """打开文件"""
        try:
            # 显示加载提示
            InfoBar.info(
                title='正在打开',
                content=f'{Path(file_path).name}',
                orient=Qt.Orientation.Horizontal,
                isClosable=False,
                position=InfoBarPosition.TOP,
                duration=1000,
                parent=self
            )

            parser = DWGParser()
            self.document = parser.parse(file_path)
            self.current_file = Path(file_path)

            self.documentLoaded.emit(self.document)

            self.setWindowTitle(f"表哥 - {self.current_file.name}")

            entity_count = len(self.document.entities)
            layer_count = len(self.document.layers)

            # 显示成功提示
            InfoBar.success(
                title='文件已打开',
                content=f'{self.current_file.name} ({entity_count}个实体, {layer_count}个图层)',
                orient=Qt.Orientation.Horizontal,
                isClosable=True,
                position=InfoBarPosition.TOP_RIGHT,
                duration=3000,
                parent=self
            )

            # 启用快捷按钮
            if hasattr(self, 'quick_translate_btn'):
                self.quick_translate_btn.setEnabled(True)
            if hasattr(self, 'quick_calc_btn'):
                self.quick_calc_btn.setEnabled(True)

            logger.info(f"文件打开成功: {file_path}")

        except DWGParseError as e:
            # 使用Fluent MessageBox
            w = MessageBox(
                "解析错误",
                str(e),
                self
            )
            w.exec()
            logger.error(f"文件解析失败: {e}")
        except Exception as e:
            # 使用Fluent MessageBox
            w = MessageBox(
                "错误",
                f"打开文件失败:\n{str(e)}",
                self
            )
            w.exec()
            logger.error(f"打开文件失败: {e}", exc_info=True)

    def onQuickTranslate(self):
        """快捷翻译 - 切换到翻译界面"""
        if not self.document:
            InfoBar.warning(
                title='请先打开文件',
                content='请先打开DWG文件再进行翻译',
                orient=Qt.Orientation.Horizontal,
                isClosable=True,
                position=InfoBarPosition.TOP,
                duration=2000,
                parent=self
            )
            return

        # 切换到翻译界面
        self.switchTo(self.translation_widget)
        logger.info("切换到翻译界面")

    def onQuickCalculate(self):
        """快捷算量 - 切换到算量界面"""
        if not self.document:
            InfoBar.warning(
                title='请先打开文件',
                content='请先打开DWG文件再进行算量',
                orient=Qt.Orientation.Horizontal,
                isClosable=True,
                position=InfoBarPosition.TOP,
                duration=2000,
                parent=self
            )
            return

        # 切换到算量界面
        self.switchTo(self.calculation_widget)
        logger.info("切换到算量界面")

    def onSettings(self):
        """打开设置对话框"""
        dialog = SettingsDialog(self)
        if dialog.exec():
            # 设置对话框关闭后，重新加载配置
            self._reload_config()
            logger.info("设置已更新并应用")

    def _reload_config(self):
        """重新加载配置 - API密钥立即生效"""
        try:
            # 重新加载配置
            self.config = ConfigManager()

            # 重新初始化AI助手（使用新的API密钥）
            if self.ai_assistant:
                try:
                    self.ai_assistant = AIAssistant(context_manager=self.context_manager)
                    # 更新AI助手界面的引用
                    if hasattr(self, 'ai_assistant_widget'):
                        self.ai_assistant_widget.ai_assistant = self.ai_assistant
                    logger.info("AI助手已使用新配置重新初始化")
                except Exception as e:
                    logger.warning(f"AI助手重新初始化失败: {e}")

            InfoBar.success(
                title='配置已更新',
                content='设置已保存并立即生效',
                orient=Qt.Orientation.Horizontal,
                isClosable=True,
                position=InfoBarPosition.TOP,
                duration=2000,
                parent=self
            )
        except Exception as e:
            logger.error(f"重新加载配置失败: {e}")

    def onShowLogViewer(self):
        """显示日志查看器"""
        dialog = LogViewerDialog(self)
        dialog.exec()

    def onAbout(self):
        """显示关于对话框"""
        dialog = AboutDialog(self)
        dialog.exec()

    def dragEnterEvent(self, event):
        """拖动进入"""
        if event.mimeData().hasUrls():
            event.acceptProposedAction()

    def dropEvent(self, event):
        """放下文件"""
        urls = event.mimeData().urls()
        if urls:
            file_path = urls[0].toLocalFile()
            if file_path.lower().endswith(('.dwg', '.dxf')):
                self.openFile(file_path)

    def closeEvent(self, event):
        """关闭事件"""
        self._save_window_state()

        # 使用Fluent MessageBox
        w = MessageBox(
            "确认退出",
            "确定要退出表哥软件吗？",
            self
        )

        if w.exec():
            logger.info("应用程序正常退出")
            event.accept()
        else:
            event.ignore()
