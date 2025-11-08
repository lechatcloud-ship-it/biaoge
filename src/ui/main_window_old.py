# -*- coding: utf-8 -*-
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
        from .translation import TranslationInterface
        from .calculation import CalculationInterface
        from .export import ExportInterface
        from .settings import SettingsInterface

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

        # 翻译界面
        self.translationInterface = TranslationInterface(self)
        self.addSubInterface(
            self.translationInterface,
            FluentIcon.LANGUAGE,
            '智能翻译'
        )

        # 算量界面
        self.calculationInterface = CalculationInterface(self)
        self.addSubInterface(
            self.calculationInterface,
            FluentIcon.CALCULATOR,
            '工程算量'
        )

        # 导出界面
        self.exportInterface = ExportInterface(self)
        self.addSubInterface(
            self.exportInterface,
            FluentIcon.SHARE,
            '导出'
        )

        # 设置界面（底部）
        self.settingsInterface = SettingsInterface(self)
        self.addSubInterface(
            self.settingsInterface,
            FluentIcon.SETTING,
            '设置',
            NavigationItemPosition.BOTTOM
        )

    def initBasicUI(self):
        """初始化基础UI（无Fluent Widgets时的后备方案）"""
        from PyQt6.QtWidgets import QLabel, QVBoxLayout, QWidget

        central_widget = QWidget()
        layout = QVBoxLayout(central_widget)

        label = QLabel("DWG智能翻译算量系统")
        label.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(label)

        self.setCentralWidget(central_widget)

    def refreshCanvas(self):
        """刷新画布（翻译完成后调用）"""
        if hasattr(self, 'dwgViewerInterface') and self.dwgViewerInterface.canvas:
            self.dwgViewerInterface.canvas.update()
            logger.info("画布已刷新显示翻译结果")

    def onDocumentLoaded(self, document):
        """文档加载完成的回调"""
        # 通知所有界面
        if hasattr(self, 'translationInterface'):
            self.translationInterface.setDocument(document)
        if hasattr(self, 'calculationInterface'):
            self.calculationInterface.setDocument(document)
        if hasattr(self, 'exportInterface'):
            self.exportInterface.setDocument(document)

        logger.info("文档已同步到所有界面")
