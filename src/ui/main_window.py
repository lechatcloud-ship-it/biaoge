"""
主窗口（使用PyQt-Fluent-Widgets）
"""
from PyQt6.QtCore import Qt, QSize
from PyQt6.QtGui import QIcon
from PyQt6.QtWidgets import QApplication

try:
    from qfluentwidgets import (
        FluentWindow, NavigationItemPosition,
        FluentIcon, setTheme, Theme, isDarkTheme
    )
    FLUENT_WIDGETS_AVAILABLE = True
except ImportError:
    # 如果PyQt-Fluent-Widgets未安装，使用PyQt6原生组件
    from PyQt6.QtWidgets import QMainWindow as FluentWindow
    FLUENT_WIDGETS_AVAILABLE = False

from ..utils.logger import logger
from ..utils.config_manager import config


class MainWindow(FluentWindow if FLUENT_WIDGETS_AVAILABLE else object):
    """主窗口"""

    def __init__(self):
        super().__init__()
        self.initWindow()

        if FLUENT_WIDGETS_AVAILABLE:
            self.initNavigation()
        else:
            logger.warning("PyQt-Fluent-Widgets未安装，使用基础UI")
            self.initBasicUI()

    def initWindow(self):
        """初始化窗口"""
        # 窗口标题和图标
        app_name = config.get('app.name', 'DWG智能翻译算量系统')
        self.setWindowTitle(app_name)

        # 窗口大小
        width = config.get('ui.window_width', 1400)
        height = config.get('ui.window_height', 900)
        self.resize(width, height)

        # 设置主题
        if FLUENT_WIDGETS_AVAILABLE:
            theme = config.get('ui.theme', 'auto')
            if theme == 'dark':
                setTheme(Theme.DARK)
            elif theme == 'light':
                setTheme(Theme.LIGHT)
            else:
                setTheme(Theme.AUTO)

            # 设置导航栏宽度
            if hasattr(self, 'navigationInterface'):
                self.navigationInterface.setExpandWidth(250)

        logger.info(f"主窗口初始化完成: {width}x{height}")

    def initNavigation(self):
        """初始化导航（Fluent Widgets）"""
        from .dwg_viewer import DWGViewerInterface
        from .welcome import WelcomeInterface

        # 欢迎界面
        self.welcomeInterface = WelcomeInterface(self)
        self.addSubInterface(
            self.welcomeInterface,
            FluentIcon.HOME,
            '欢迎'
        )

        # 图纸查看界面
        self.dwgViewerInterface = DWGViewerInterface(self)
        self.addSubInterface(
            self.dwgViewerInterface,
            FluentIcon.DOCUMENT,
            '图纸查看'
        )

        # TODO: 添加其他界面
        # self.translationInterface = TranslationInterface(self)
        # self.addSubInterface(...)

        # 设置界面（底部）
        # self.settingsInterface = SettingsInterface(self)
        # self.addSubInterface(
        #     self.settingsInterface,
        #     FluentIcon.SETTING,
        #     '设置',
        #     NavigationItemPosition.BOTTOM
        # )

    def initBasicUI(self):
        """初始化基础UI（无Fluent Widgets时的后备方案）"""
        from PyQt6.QtWidgets import QLabel, QVBoxLayout, QWidget

        central_widget = QWidget()
        layout = QVBoxLayout(central_widget)

        label = QLabel("DWG智能翻译算量系统")
        label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(label)

        self.setCentralWidget(central_widget)
