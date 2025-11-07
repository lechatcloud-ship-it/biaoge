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
from ..calculation.advanced_recognizer import AdvancedComponentRecognizer
from ..calculation.quantity_calculator import QuantityCalculator
from ..utils.logger import logger
from ..utils.performance import perf_monitor
from ..utils.resource_manager import resource_manager

class CalculationInterface(QWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.document = None
        self.components = []
        self.results = None
        self.use_advanced = True  # 默认使用高级识别
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

        # 性能监控
        start = perf_monitor.start_timer('component_recognition')

        # 检查内存
        resource_manager.check_memory_threshold()

        # 使用高级识别器
        if self.use_advanced:
            recognizer = AdvancedComponentRecognizer(use_ai=False)
            self.components = recognizer.recognize(self.document)
        else:
            recognizer = ComponentRecognizer()
            self.components = recognizer.recognize_components(self.document)

        perf_monitor.end_timer('component_recognition', start)

        # 计算工程量
        start = perf_monitor.start_timer('quantity_calculation')
        calculator = QuantityCalculator()
        self.results = calculator.calculate(self.components)
        perf_monitor.end_timer('quantity_calculation', start)

        # 显示结果
        self.resultTable.setRowCount(len(self.results))
        for i, result in enumerate(self.results.values()):
            self.resultTable.setItem(i, 0, QTableWidgetItem(result.component_type.value))
            self.resultTable.setItem(i, 1, QTableWidgetItem(str(result.count)))
            self.resultTable.setItem(i, 2, QTableWidgetItem(f"{result.total_volume:.2f}"))
            self.resultTable.setItem(i, 3, QTableWidgetItem(f"{result.total_area:.2f}"))
            self.resultTable.setItem(i, 4, QTableWidgetItem(f"¥{result.total_cost:.2f}"))

        report = calculator.generate_report(self.results)
        self.reportText.setPlainText(report)

        # 打印性能统计
        perf_monitor.print_stats()
        mem_usage = resource_manager.get_memory_usage()
        logger.info(f"识别完成: {len(self.components)} 个构件, 内存: {mem_usage['rss_mb']:.2f} MB")

        # 通知父窗口更新导出界面
        parent = self.parent()
        if parent and hasattr(parent, 'exportInterface'):
            parent.exportInterface.setQuantityResults(self.results)
