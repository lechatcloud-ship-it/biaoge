# -*- coding: utf-8 -*-
"""
DWG图纸查看器界面
"""
from PyQt6.QtWidgets import QWidget, QVBoxLayout, QHBoxLayout
from PyQt6.QtCore import Qt

from qfluentwidgets import (
    CardWidget, PrimaryPushButton, PushButton,
    InfoBar, InfoBarPosition, FluentIcon
)

from ..dwg.parser import DWGParser, DWGParseError
from ..dwg.entities import DWGDocument
from ..dwg.renderer import DWGCanvas
from ..utils.logger import logger


class DWGViewerInterface(QWidget):
    """DWG查看器界面"""

    def __init__(self, parent=None):
        super().__init__(parent=parent)
        self.document: DWGDocument = None
        self.setupUI()

    def setupUI(self):
        """设置UI"""
        layout = QVBoxLayout(self)
        layout.setContentsMargins(20, 20, 20, 20)
        layout.setSpacing(20)

        # 工具栏
        toolbar = self._createToolbar()
        layout.addWidget(toolbar)

        # 信息面板
        from qfluentwidgets import BodyLabel
        self.infoLabel = BodyLabel("未加载图纸")
        self.infoLabel.setAlignment(Qt.AlignmentFlag.AlignCenter)
        layout.addWidget(self.infoLabel)

        # DWG渲染画布
        self.canvas = DWGCanvas(self)
        layout.addWidget(self.canvas, 1)

    def _createToolbar(self):
        """创建工具栏"""
        toolbar = CardWidget(self)
        layout = QHBoxLayout(toolbar)
        layout.setContentsMargins(10, 10, 10, 10)

        # 打开按钮
        self.openBtn = PrimaryPushButton("打开DWG文件", self)
        self.openBtn.setIcon(FluentIcon.FOLDER)
        self.openBtn.clicked.connect(self.onOpenFile)
        layout.addWidget(self.openBtn)

        layout.addSpacing(10)

        # 视图控制按钮
        self.zoomInBtn = PushButton("放大", self)
        self.zoomInBtn.setIcon(FluentIcon.ZOOM_IN)
        self.zoomOutBtn = PushButton("缩小", self)
        self.zoomOutBtn.setIcon(FluentIcon.ZOOM_OUT)
        self.fitViewBtn = PushButton("适应窗口", self)
        self.fitViewBtn.setIcon(FluentIcon.FULL_SCREEN)
        self.resetViewBtn = PushButton("重置视图", self)
        self.resetViewBtn.setIcon(FluentIcon.SYNC)

        self.zoomInBtn.clicked.connect(self.onZoomIn)
        self.zoomOutBtn.clicked.connect(self.onZoomOut)
        self.fitViewBtn.clicked.connect(self.onFitView)
        self.resetViewBtn.clicked.connect(self.onResetView)

        layout.addWidget(self.zoomInBtn)
        layout.addWidget(self.zoomOutBtn)
        layout.addWidget(self.fitViewBtn)
        layout.addWidget(self.resetViewBtn)

        # 初始禁用（未加载图纸时）
        self.zoomInBtn.setEnabled(False)
        self.zoomOutBtn.setEnabled(False)
        self.fitViewBtn.setEnabled(False)
        self.resetViewBtn.setEnabled(False)

        layout.addStretch(1)

        return toolbar

    def onOpenFile(self):
        """打开文件"""
        from PyQt6.QtWidgets import QFileDialog
        file_path, _ = QFileDialog.getOpenFileName(
            self,
            "打开DWG文件",
            "",
            "CAD文件 (*.dwg *.dxf);;All Files (*)"
        )

        if not file_path:
            return

        self.loadDrawing(file_path)

    def loadDrawing(self, file_path: str):
        """加载图纸"""
        try:
            logger.info(f"加载图纸: {file_path}")

            # 解析DWG文件
            parser = DWGParser()
            self.document = parser.parse(file_path)

            # 更新信息
            info_text = (
                f"文件: {self.document.metadata.get('filename', 'N/A')}\n"
                f"版本: {self.document.version}\n"
                f"图层数: {self.document.layer_count}\n"
                f"实体数: {self.document.entity_count}"
            )
            self.infoLabel.setText(info_text)

            # 渲染图纸
            self.canvas.setDocument(self.document)

            # 启用视图控制按钮
            self.zoomInBtn.setEnabled(True)
            self.zoomOutBtn.setEnabled(True)
            self.fitViewBtn.setEnabled(True)
            self.resetViewBtn.setEnabled(True)

            # 显示成功提示
            InfoBar.success(
                title='加载成功',
                content=f'已加载 {self.document.entity_count} 个实体',
                orient=Qt.Orientation.Horizontal,
                isClosable=True,
                position=InfoBarPosition.TOP,
                duration=2000,
                parent=self
            )

            # 通知主窗口文档已加载
            parent = self.parent()
            if parent and hasattr(parent, 'onDocumentLoaded'):
                parent.onDocumentLoaded(self.document)

            logger.info("图纸加载成功")

        except DWGParseError as e:
            logger.error(f"解析失败: {e}")
            InfoBar.error(
                title='解析失败',
                content=str(e),
                orient=Qt.Orientation.Horizontal,
                isClosable=True,
                position=InfoBarPosition.TOP,
                duration=3000,
                parent=self
            )

        except Exception as e:
            logger.error(f"加载失败: {e}", exc_info=True)
            InfoBar.error(
                title='加载失败',
                content=f"未知错误: {e}",
                orient=Qt.Orientation.Horizontal,
                isClosable=True,
                position=InfoBarPosition.TOP,
                duration=3000,
                parent=self
            )

    def onZoomIn(self):
        """放大"""
        if self.canvas:
            self.canvas.zoomIn()

    def onZoomOut(self):
        """缩小"""
        if self.canvas:
            self.canvas.zoomOut()

    def onFitView(self):
        """适应窗口"""
        if self.canvas:
            self.canvas.fitToView()

    def onResetView(self):
        """重置视图"""
        if self.canvas:
            self.canvas.resetView()
