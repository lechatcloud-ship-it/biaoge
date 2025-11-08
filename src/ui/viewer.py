# -*- coding: utf-8 -*-
"""
查看器组件 - 包含工具栏和画布
"""
from PyQt6.QtWidgets import (
    QWidget, QVBoxLayout, QHBoxLayout, QToolBar
)
from PyQt6.QtCore import Qt, pyqtSignal
from PyQt6.QtGui import QAction, QIcon

from qfluentwidgets import PushButton, BodyLabel, ComboBox

from ..dwg.renderer import DWGCanvas
from ..dwg.entities import DWGDocument
from typing import Optional


class ViewerWidget(QWidget):
    """查看器组件"""

    def __init__(self, parent=None):
        super().__init__(parent)

        self.document: Optional[DWGDocument] = None

        self._init_ui()

    def _init_ui(self):
        """初始化UI"""
        layout = QVBoxLayout(self)
        layout.setContentsMargins(0, 0, 0, 0)
        layout.setSpacing(0)

        # 画布 - 必须先创建，因为工具栏需要引用它
        self.canvas = DWGCanvas()

        # 工具栏
        toolbar = self._create_toolbar()
        layout.addWidget(toolbar)

        # 画布
        layout.addWidget(self.canvas)

        # 状态栏
        status_bar = self._create_statusbar()
        layout.addWidget(status_bar)

    def _create_toolbar(self):
        """创建工具栏"""
        toolbar = QToolBar()
        toolbar.setMovable(False)

        # 缩放按钮
        zoom_in_btn = PushButton("+")
        zoom_in_btn.setToolTip("放大 (Ctrl++)")
        zoom_in_btn.clicked.connect(self.canvas.zoomIn)
        toolbar.addWidget(zoom_in_btn)

        zoom_out_btn = PushButton("-")
        zoom_out_btn.setToolTip("缩小 (Ctrl+-)")
        zoom_out_btn.clicked.connect(self.canvas.zoomOut)
        toolbar.addWidget(zoom_out_btn)

        fit_btn = PushButton("适应")
        fit_btn.setToolTip("适应视图 (F)")
        fit_btn.clicked.connect(self.canvas.fitToView)
        toolbar.addWidget(fit_btn)

        reset_btn = PushButton("重置")
        reset_btn.setToolTip("重置视图 (R)")
        reset_btn.clicked.connect(self.canvas.resetView)
        toolbar.addWidget(reset_btn)

        toolbar.addSeparator()

        # 图层控制
        toolbar.addWidget(BodyLabel(" 图层: "))

        self.layer_combo = ComboBox()
        self.layer_combo.addItem("显示所有图层")
        self.layer_combo.currentIndexChanged.connect(self._on_layer_changed)
        toolbar.addWidget(self.layer_combo)

        toolbar.addSeparator()

        # 选项
        self.axes_btn = PushButton("坐标轴")
        self.axes_btn.setCheckable(True)
        self.axes_btn.setChecked(True)
        self.axes_btn.toggled.connect(self._on_axes_toggled)
        toolbar.addWidget(self.axes_btn)

        self.aa_btn = PushButton("抗锯齿")
        self.aa_btn.setCheckable(True)
        self.aa_btn.setChecked(True)
        self.aa_btn.toggled.connect(self._on_aa_toggled)
        toolbar.addWidget(self.aa_btn)

        return toolbar

    def _create_statusbar(self):
        """创建状态栏"""
        statusbar = QWidget()
        statusbar.setMaximumHeight(25)
        statusbar.setStyleSheet("background-color: #f5f5f5; border-top: 1px solid #ddd;")

        layout = QHBoxLayout(statusbar)
        layout.setContentsMargins(10, 2, 10, 2)

        # 缩放级别
        self.zoom_label = BodyLabel("缩放: 1.00x")
        layout.addWidget(self.zoom_label)

        layout.addStretch()

        # 实体数量
        self.entity_label = BodyLabel("实体: 0")
        layout.addWidget(self.entity_label)

        # 监听视口变化
        self.canvas.viewportChanged.connect(self._on_viewport_changed)

        return statusbar

    def _on_viewport_changed(self, zoom, offset):
        """视口变化"""
        self.zoom_label.setText(f"缩放: {zoom:.2f}x")

    def _on_layer_changed(self, index):
        """图层切换"""
        if not self.document:
            return

        if index == 0:  # 显示所有图层
            self.canvas.setAllLayersVisible(True)
        else:
            layer_name = self.layer_combo.currentText()
            # TODO: 实现单独图层显示逻辑

    def _on_axes_toggled(self, checked):
        """坐标轴切换"""
        self.canvas.show_axes = checked
        self.canvas.update()

    def _on_aa_toggled(self, checked):
        """抗锯齿切换"""
        self.canvas.antialiasing = checked
        self.canvas.update()

    def setDocument(self, document: DWGDocument):
        """设置文档"""
        self.document = document
        self.canvas.setDocument(document)

        # 更新图层列表
        self.layer_combo.clear()
        self.layer_combo.addItem("显示所有图层")
        for layer in document.layers:
            self.layer_combo.addItem(layer.name)

        # 更新实体数量
        self.entity_label.setText(f"实体: {len(document.entities)}")

    # 公开方法供主窗口调用
    def zoomIn(self):
        """放大"""
        self.canvas.zoomIn()

    def zoomOut(self):
        """缩小"""
        self.canvas.zoomOut()

    def fitToView(self):
        """适应视图"""
        self.canvas.fitToView()

    def resetView(self):
        """重置视图"""
        self.canvas.resetView()
