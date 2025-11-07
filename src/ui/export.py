"""导出界面"""
from PyQt6.QtWidgets import QWidget, QVBoxLayout, QPushButton, QFileDialog, QLabel, QMessageBox
from PyQt6.QtCore import Qt
try:
    from qfluentwidgets import CardWidget, PrimaryPushButton, PushButton, TitleLabel
    FLUENT = True
except:
    CardWidget = QWidget
    PrimaryPushButton = QPushButton
    PushButton = QPushButton
    TitleLabel = QLabel
    FLUENT = False

from ..export.dwg_exporter import DWGExporter
from ..export.pdf_exporter import PDFExporter
from ..export.excel_exporter import ExcelExporter
from ..utils.logger import logger

class ExportInterface(QWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.document = None
        self.quantity_results = None
        self.setupUI()
    
    def setupUI(self):
        layout = QVBoxLayout(self)
        title = TitleLabel("导出") if FLUENT else QLabel("导出")
        layout.addWidget(title)
        
        self.exportDWGBtn = PushButton("导出DWG/DXF") if FLUENT else QPushButton("导出DWG/DXF")
        self.exportDWGBtn.clicked.connect(self.onExportDWG)
        layout.addWidget(self.exportDWGBtn)
        
        self.exportPDFBtn = PushButton("导出PDF") if FLUENT else QPushButton("导出PDF")
        self.exportPDFBtn.clicked.connect(self.onExportPDF)
        layout.addWidget(self.exportPDFBtn)
        
        self.exportExcelBtn = PushButton("导出Excel报表") if FLUENT else QPushButton("导出Excel报表")
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
            DWGExporter().export(self.document, path)
            QMessageBox.information(self, "成功", "导出成功！")
    
    def onExportPDF(self):
        if not self.document:
            return
        path, _ = QFileDialog.getSaveFileName(self, "保存PDF", "", "PDF Files (*.pdf)")
        if path:
            PDFExporter().export(self.document, path)
            QMessageBox.information(self, "成功", "导出成功！")
    
    def onExportExcel(self):
        if not self.quantity_results:
            return
        path, _ = QFileDialog.getSaveFileName(self, "保存Excel", "", "Excel Files (*.xlsx)")
        if path:
            ExcelExporter().export_quantity(self.quantity_results, path)
            QMessageBox.information(self, "成功", "导出成功！")
