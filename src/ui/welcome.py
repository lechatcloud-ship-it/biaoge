"""
欢迎界面
"""
from PyQt6.QtWidgets import QWidget, QVBoxLayout, QLabel, QPushButton
from PyQt6.QtCore import Qt
from PyQt6.QtGui import QFont

try:
    from qfluentwidgets import ScrollArea, CardWidget, PrimaryPushButton, BodyLabel, TitleLabel
    FLUENT_WIDGETS_AVAILABLE = True
except ImportError:
    ScrollArea = QWidget
    FLUENT_WIDGETS_AVAILABLE = False

from ..utils.config_manager import config


class WelcomeInterface(ScrollArea if FLUENT_WIDGETS_AVAILABLE else QWidget):
    """欢迎界面"""

    def __init__(self, parent=None):
        super().__init__(parent=parent)

        if FLUENT_WIDGETS_AVAILABLE:
            self.setupFluentUI()
        else:
            self.setupBasicUI()

    def setupFluentUI(self):
        """设置Fluent UI"""
        self.view = QWidget(self)
        self.vBoxLayout = QVBoxLayout(self.view)
        self.vBoxLayout.setContentsMargins(40, 40, 40, 40)
        self.vBoxLayout.setSpacing(20)

        # 标题
        title = TitleLabel("欢迎使用DWG智能翻译算量系统", self)
        self.vBoxLayout.addWidget(title)

        # 介绍卡片
        intro_card = self._createIntroCard()
        self.vBoxLayout.addWidget(intro_card)

        # 快速开始卡片
        quick_start_card = self._createQuickStartCard()
        self.vBoxLayout.addWidget(quick_start_card)

        # 添加弹性空间
        self.vBoxLayout.addStretch(1)

        self.setWidget(self.view)
        self.setWidgetResizable(True)

    def _createIntroCard(self):
        """创建介绍卡片"""
        card = CardWidget(self)
        layout = QVBoxLayout(card)
        layout.setContentsMargins(20, 20, 20, 20)

        title = BodyLabel("功能特性", self)
        title.setStyleSheet("font-weight: bold; font-size: 16px;")
        layout.addWidget(title)

        features = [
            "🌍 智能翻译：支持中英日韩等多语言DWG图纸翻译",
            "📊 自动算量：AI识别构件，自动计算工程量",
            "📤 多格式导出：支持DWG/DXF/PDF/Excel等格式",
            "⚡ 高性能：Qt原生渲染，50000+实体流畅显示",
        ]

        for feature in features:
            label = BodyLabel(feature, self)
            layout.addWidget(label)

        return card

    def _createQuickStartCard(self):
        """创建快速开始卡片"""
        card = CardWidget(self)
        layout = QVBoxLayout(card)
        layout.setContentsMargins(20, 20, 20, 20)

        title = BodyLabel("快速开始", self)
        title.setStyleSheet("font-weight: bold; font-size: 16px;")
        layout.addWidget(title)

        description = BodyLabel(
            "点击左侧"图纸查看"，开始导入DWG文件...",
            self
        )
        layout.addWidget(description)

        # 按钮
        btn = PrimaryPushButton("打开图纸", self)
        btn.clicked.connect(self._onOpenDrawing)
        layout.addWidget(btn)

        return card

    def setupBasicUI(self):
        """设置基础UI"""
        layout = QVBoxLayout(self)
        layout.setContentsMargins(40, 40, 40, 40)

        title = QLabel("欢迎使用DWG智能翻译算量系统")
        title.setFont(QFont("Arial", 20, QFont.Weight.Bold))
        title.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(title)

        description = QLabel("点击左侧菜单开始使用...")
        description.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(description)

        layout.addStretch(1)

    def _onOpenDrawing(self):
        """打开图纸"""
        # 切换到图纸查看界面
        parent = self.parent()
        if hasattr(parent, 'dwgViewerInterface'):
            parent.switchTo(parent.dwgViewerInterface)
