"""
主窗口 - 完整商业版 (Fluent Design)
"""
from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout,
    QSplitter, QMessageBox, QFileDialog, QStatusBar
)
from PyQt6.QtGui import QAction, QKeySequence, QIcon
from PyQt6.QtCore import Qt, pyqtSignal
from pathlib import Path
from typing import Optional

try:
    from qfluentwidgets import (
        FluentWindow, NavigationItemPosition, FluentIcon,
        InfoBar, InfoBarPosition, setTheme, Theme
    )
    FLUENT_AVAILABLE = True
except ImportError:
    from PyQt6.QtWidgets import QMainWindow as FluentWindow
    FLUENT_AVAILABLE = False

from ..dwg.parser import DWGParser, DWGParseError
from ..dwg.entities import DWGDocument
from .viewer import ViewerWidget
from .translation import TranslationInterface
from .calculation import CalculationInterface
from .export import ExportInterface
from .batch_widget import BatchWidget
from .settings_dialog import SettingsDialog
from .ai_chat_widget import AIChatWidget
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

        if FLUENT_AVAILABLE:
            self._init_fluent_ui()
        else:
            self._init_classic_ui()

        self._connect_signals()
        self._restore_window_state()

        self.setAcceptDrops(True)

        if FLUENT_AVAILABLE:
            # 设置Fluent主题
            setTheme(Theme.AUTO)
            logger.info("主窗口初始化完成 (Fluent Design)")
        else:
            logger.info("主窗口初始化完成 (经典模式)")

    def _init_fluent_ui(self):
        """初始化Fluent Design UI"""
        self.setWindowTitle("表哥 - DWG翻译计算软件 v1.0.0")
        self.setMinimumSize(1200, 800)
        self.resize(1400, 900)

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
            FluentIcon.CALORIES,
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
        """创建主页 - 包含DWG查看器"""
        home_widget = QWidget()
        home_widget.setObjectName("homePage")

        layout = QVBoxLayout(home_widget)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)

        # 创建查看器组件
        self.viewer_widget = ViewerWidget()
        layout.addWidget(self.viewer_widget)

        # 创建底部状态栏
        self.status_bar = QStatusBar()
        self.status_bar.setStyleSheet("""
            QStatusBar {
                background-color: #f5f5f5;
                border-top: 1px solid #ddd;
                padding: 5px 10px;
            }
        """)
        self.status_bar.showMessage("就绪 | 请打开DWG文件开始使用")
        layout.addWidget(self.status_bar)

        return home_widget

    def _init_classic_ui(self):
        """初始化经典UI (Fallback)"""
        self.setWindowTitle("表哥 - DWG翻译计算软件 v1.0.0")
        self.setMinimumSize(1200, 800)

        central_widget = QWidget()
        self.setCentralWidget(central_widget)

        main_layout = QVBoxLayout(central_widget)
        main_layout.setContentsMargins(0, 0, 0, 0)
        main_layout.setSpacing(0)

        splitter = QSplitter(Qt.Orientation.Horizontal)

        self.viewer_widget = ViewerWidget()
        splitter.addWidget(self.viewer_widget)

        from PyQt6.QtWidgets import QTabWidget
        self.tab_widget = QTabWidget()
        self.tab_widget.setMinimumWidth(350)
        self.tab_widget.setMaximumWidth(500)

        self.translation_widget = TranslationInterface()
        self.tab_widget.addTab(self.translation_widget, "📝 翻译")

        self.calculation_widget = CalculationInterface()
        self.tab_widget.addTab(self.calculation_widget, "📊 算量")

        self.export_widget = ExportInterface()
        self.tab_widget.addTab(self.export_widget, "💾 导出")

        self.batch_widget = BatchWidget()
        self.tab_widget.addTab(self.batch_widget, "📦 批量处理")

        self.performance_panel = PerformancePanel()
        self.tab_widget.addTab(self.performance_panel, "⚡ 性能")

        self.ai_assistant_widget = AIAssistantWidget(ai_assistant=self.ai_assistant)
        self.tab_widget.addTab(self.ai_assistant_widget, "🤖 AI助手")

        splitter.addWidget(self.tab_widget)
        splitter.setStretchFactor(0, 7)
        splitter.setStretchFactor(1, 3)

        main_layout.addWidget(splitter)
        self.splitter = splitter

        self._create_actions()
        self._create_menus()
        self._create_toolbars()
        self._create_statusbar()

    def _create_actions(self):
        """创建动作"""
        self.open_action = QAction("打开DWG文件...", self)
        self.open_action.setShortcut(QKeySequence.StandardKey.Open)
        self.open_action.triggered.connect(self.onOpenFile)

        self.batch_action = QAction("批量处理...", self)
        self.batch_action.setShortcut("Ctrl+B")
        self.batch_action.triggered.connect(self.onBatchProcessing)

        self.exit_action = QAction("退出", self)
        self.exit_action.setShortcut(QKeySequence.StandardKey.Quit)
        self.exit_action.triggered.connect(self.close)

        self.zoom_in_action = QAction("放大", self)
        self.zoom_in_action.setShortcut(QKeySequence.StandardKey.ZoomIn)
        self.zoom_in_action.triggered.connect(self.viewer_widget.zoomIn)

        self.zoom_out_action = QAction("缩小", self)
        self.zoom_out_action.setShortcut(QKeySequence.StandardKey.ZoomOut)
        self.zoom_out_action.triggered.connect(self.viewer_widget.zoomOut)

        self.fit_view_action = QAction("适应视图", self)
        self.fit_view_action.setShortcut("F")
        self.fit_view_action.triggered.connect(self.viewer_widget.fitToView)

        self.settings_action = QAction("设置...", self)
        self.settings_action.triggered.connect(self.onSettings)

        self.log_viewer_action = QAction("日志查看器", self)
        self.log_viewer_action.triggered.connect(self.onShowLogViewer)

        self.about_action = QAction("关于", self)
        self.about_action.triggered.connect(self.onAbout)

    def _create_menus(self):
        """创建菜单栏"""
        menubar = self.menuBar()

        file_menu = menubar.addMenu("文件(&F)")
        file_menu.addAction(self.open_action)
        file_menu.addAction(self.batch_action)
        file_menu.addSeparator()
        file_menu.addAction(self.exit_action)

        view_menu = menubar.addMenu("视图(&V)")
        view_menu.addAction(self.zoom_in_action)
        view_menu.addAction(self.zoom_out_action)
        view_menu.addAction(self.fit_view_action)

        tools_menu = menubar.addMenu("工具(&T)")
        tools_menu.addAction(self.log_viewer_action)
        tools_menu.addSeparator()
        tools_menu.addAction(self.settings_action)

        help_menu = menubar.addMenu("帮助(&H)")
        help_menu.addAction(self.about_action)

    def _create_toolbars(self):
        """创建工具栏"""
        file_toolbar = self.addToolBar("文件")
        file_toolbar.addAction(self.open_action)

        view_toolbar = self.addToolBar("视图")
        view_toolbar.addAction(self.zoom_in_action)
        view_toolbar.addAction(self.zoom_out_action)
        view_toolbar.addAction(self.fit_view_action)

    def _create_statusbar(self):
        """创建状态栏"""
        self.status_bar = QStatusBar()
        self.setStatusBar(self.status_bar)
        self.status_bar.showMessage("就绪 | 请打开DWG文件开始使用")

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
        if FLUENT_AVAILABLE:
            # Fluent模式：切换到批量处理子界面
            self.switchTo(self.batch_widget)
        else:
            # 经典模式：切换到批量处理标签页
            for i in range(self.tab_widget.count()):
                if self.tab_widget.tabText(i) == "📦 批量处理":
                    self.tab_widget.setCurrentIndex(i)
                    break

    def openFile(self, file_path: str):
        """打开文件"""
        try:
            if hasattr(self, 'status_bar'):
                self.status_bar.showMessage(f"正在打开: {Path(file_path).name}...")

            parser = DWGParser()
            self.document = parser.parse(file_path)
            self.current_file = Path(file_path)

            self.documentLoaded.emit(self.document)

            self.setWindowTitle(f"表哥 - {self.current_file.name}")

            entity_count = len(self.document.entities)
            layer_count = len(self.document.layers)

            if hasattr(self, 'status_bar'):
                self.status_bar.showMessage(
                    f"已加载: {self.current_file.name} | "
                    f"{entity_count} 个实体 | {layer_count} 个图层"
                )

            # Fluent模式下显示成功提示
            if FLUENT_AVAILABLE:
                InfoBar.success(
                    title='文件已打开',
                    content=f'{self.current_file.name} ({entity_count}个实体)',
                    orient=Qt.Orientation.Horizontal,
                    isClosable=True,
                    position=InfoBarPosition.TOP_RIGHT,
                    duration=2000,
                    parent=self
                )

            logger.info(f"文件打开成功: {file_path}")

        except DWGParseError as e:
            QMessageBox.critical(self, "解析错误", str(e))
            logger.error(f"文件解析失败: {e}")
        except Exception as e:
            QMessageBox.critical(self, "错误", f"打开文件失败:\n{str(e)}")
            logger.error(f"打开文件失败: {e}", exc_info=True)

    def onSettings(self):
        """打开设置对话框"""
        dialog = SettingsDialog(self)
        dialog.exec()

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

        reply = QMessageBox.question(
            self,
            "确认退出",
            "确定要退出表哥软件吗？",
            QMessageBox.StandardButton.Yes | QMessageBox.StandardButton.No,
            QMessageBox.StandardButton.No
        )

        if reply == QMessageBox.StandardButton.Yes:
            logger.info("应用程序正常退出")
            event.accept()
        else:
            event.ignore()
