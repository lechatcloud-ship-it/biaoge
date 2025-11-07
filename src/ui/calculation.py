"""算量界面"""
from PyQt6.QtWidgets import QWidget, QVBoxLayout, QPushButton, QTextEdit, QLabel, QTableWidget, QTableWidgetItem, QHBoxLayout
from PyQt6.QtCore import Qt
try:
    from qfluentwidgets import CardWidget, PrimaryPushButton, TitleLabel, BodyLabel, TableWidget
    FLUENT = True
except:
    CardWidget = QWidget
    PrimaryPushButton = QPushButton
    TitleLabel = QLabel
    BodyLabel = QLabel
    TableWidget = QTableWidget
    FLUENT = False

from ..calculation.component_recognizer import ComponentRecognizer
from ..calculation.quantity_calculator import QuantityCalculator
from ..utils.logger import logger

class CalculationInterface(QWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.document = None
        self.components = []
        self.setupUI()
    
    def setupUI(self):
        layout = QVBoxLayout(self)
        title = TitleLabel("工程量计算") if FLUENT else QLabel("工程量计算")
        layout.addWidget(title)
        
        self.recognizeBtn = PrimaryPushButton("识别构件") if FLUENT else QPushButton("识别构件")
        self.recognizeBtn.clicked.connect(self.onRecognize)
        layout.addWidget(self.recognizeBtn)
        
        self.resultTable = TableWidget() if FLUENT else QTableWidget()
        self.resultTable.setColumnCount(5)
        self.resultTable.setHorizontalHeaderLabels(["类型", "数量", "体积", "面积", "费用"])
        layout.addWidget(self.resultTable)
        
        self.reportText = QTextEdit()
        self.reportText.setReadOnly(True)
        layout.addWidget(self.reportText)
    
    def setDocument(self, document):
        self.document = document
        self.recognizeBtn.setEnabled(True)
    
    def onRecognize(self):
        if not self.document:
            return
        
        recognizer = ComponentRecognizer()
        self.components = recognizer.recognize_components(self.document)
        
        calculator = QuantityCalculator()
        results = calculator.calculate(self.components)
        
        self.resultTable.setRowCount(len(results))
        for i, result in enumerate(results.values()):
            self.resultTable.setItem(i, 0, QTableWidgetItem(result.component_type.value))
            self.resultTable.setItem(i, 1, QTableWidgetItem(str(result.count)))
            self.resultTable.setItem(i, 2, QTableWidgetItem(f"{result.total_volume:.2f}"))
            self.resultTable.setItem(i, 3, QTableWidgetItem(f"{result.total_area:.2f}"))
            self.resultTable.setItem(i, 4, QTableWidgetItem(f"¥{result.total_cost:.2f}"))
        
        report = calculator.generate_report(results)
        self.reportText.setPlainText(report)
        logger.info(f"识别了 {len(self.components)} 个构件")
