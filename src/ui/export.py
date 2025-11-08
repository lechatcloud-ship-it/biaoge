# -*- coding: utf-8 -*-
"""导出界面"""
from PyQt6.QtWidgets import QWidget, QVBoxLayout, QFileDialog
from PyQt6.QtCore import Qt

from qfluentwidgets import (
    CardWidget, PrimaryPushButton, PushButton, TitleLabel,
    InfoBar, InfoBarPosition, FluentIcon
)

from ..export.dwg_exporter import DWGExporter
from ..export.advanced_dwg_exporter import AdvancedDWGExporter
from ..export.pdf_exporter import PDFExporter
from ..export.excel_exporter import ExcelExporter
from ..utils.logger import logger
from ..utils.performance import perf_monitor

class ExportInterface(QWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.document = None
        self.quantity_results = None
        self.setupUI()

    def setupUI(self):
        layout = QVBoxLayout(self)
        title = TitleLabel("导出")
        layout.addWidget(title)

        self.exportDWGBtn = PushButton("导出DWG/DXF")
        self.exportDWGBtn.setIcon(FluentIcon.SAVE)
        self.exportDWGBtn.clicked.connect(self.onExportDWG)
        layout.addWidget(self.exportDWGBtn)

        self.exportPDFBtn = PushButton("导出PDF")
        self.exportPDFBtn.setIcon(FluentIcon.DOCUMENT)
        self.exportPDFBtn.clicked.connect(self.onExportPDF)
        layout.addWidget(self.exportPDFBtn)

        self.exportExcelBtn = PushButton("导出Excel报表")
        self.exportExcelBtn.setIcon(FluentIcon.CHART)
        self.exportExcelBtn.clicked.connect(self.onExportExcel)
        layout.addWidget(self.exportExcelBtn)

        layout.addStretch()

    def setDocument(self, document):
        self.document = document
        self.exportDWGBtn.setEnabled(True)
        self.exportPDFBtn.setEnabled(True)

    def setQuantityResults(self, results):
        self.quantity_results = results
        self.exportExcelBtn.setEnabled(True)

    def onExportDWG(self):
        if not self.document:
            return
        path, _ = QFileDialog.getSaveFileName(self, "保存DWG", "", "DWG Files (*.dwg);;DXF Files (*.dxf)")
        if path:
            try:
                start = perf_monitor.start_timer('dwg_export')
                exporter = AdvancedDWGExporter()
                success = exporter.export(self.document, path, version='R2018')
                perf_monitor.end_timer('dwg_export', start)

                if success:
                    InfoBar.success(
                        title='导出成功',
                        content=f"文件: {path}",
                        orient=Qt.Orientation.Horizontal,
                        isClosable=True,
                        position=InfoBarPosition.TOP,
                        duration=2000,
                        parent=self
                    )
                    logger.info(f"DWG导出成功: {path}")
                else:
                    InfoBar.warning(
                        title='导出警告',
                        content="导出完成，但部分实体可能失败",
                        orient=Qt.Orientation.Horizontal,
                        isClosable=True,
                        position=InfoBarPosition.TOP,
                        duration=3000,
                        parent=self
                    )
            except Exception as e:
                logger.error(f"导出失败: {e}")
                InfoBar.error(
                    title='导出失败',
                    content=str(e),
                    orient=Qt.Orientation.Horizontal,
                    isClosable=True,
                    position=InfoBarPosition.TOP,
                    duration=3000,
                    parent=self
                )

    def onExportPDF(self):
        if not self.document:
            return
        path, _ = QFileDialog.getSaveFileName(self, "保存PDF", "", "PDF Files (*.pdf)")
        if path:
            PDFExporter().export(self.document, path)
            InfoBar.success(
                title='导出成功',
                content="PDF文件已保存",
                orient=Qt.Orientation.Horizontal,
                isClosable=True,
                position=InfoBarPosition.TOP,
                duration=2000,
                parent=self
            )

    def onExportExcel(self):
        if not self.quantity_results:
            return
        path, _ = QFileDialog.getSaveFileName(self, "保存Excel", "", "Excel Files (*.xlsx)")
        if path:
            ExcelExporter().export_quantity(self.quantity_results, path)
            InfoBar.success(
                title='导出成功',
                content="Excel文件已保存",
                orient=Qt.Orientation.Horizontal,
                isClosable=True,
                position=InfoBarPosition.TOP,
                duration=2000,
                parent=self
            )
