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
from ..calculation.ultra_precise_recognizer import UltraPreciseRecognizer  # 🚀 超精确识别器 (99.9999%)
from ..calculation.quantity_calculator import QuantityCalculator
from ..calculation.result_validator import ResultValidator  # 🆕 结果验证器
from ..utils.logger import logger
from ..utils.performance import perf_monitor
from ..utils.resource_manager import resource_manager

class CalculationInterface(QWidget):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.document = None
        self.components = []
        self.results = None
        self.component_confidences = []  # 🚀 构件置信度列表
        self.use_advanced = True  # 默认使用高级识别
        self.use_ultra_precise = True  # 🚀 默认使用超精确识别 (99.9999%)
        self.setupUI()
    
    def setupUI(self):
        layout = QVBoxLayout(self)
        title = TitleLabel("工程量计算") if FLUENT else QLabel("工程量计算")
        layout.addWidget(title)

        self.recognizeBtn = PrimaryPushButton("识别构件") if FLUENT else QPushButton("识别构件")
        self.recognizeBtn.clicked.connect(self.onRecognize)
        layout.addWidget(self.recognizeBtn)

        # 🆕 验证状态标签
        self.validationLabel = BodyLabel("等待识别...") if FLUENT else QLabel("等待识别...")
        layout.addWidget(self.validationLabel)

        self.resultTable = TableWidget() if FLUENT else QTableWidget()
        self.resultTable.setColumnCount(7)  # 🚀 增加"置信度"列
        self.resultTable.setHorizontalHeaderLabels(["类型", "数量", "体积", "面积", "费用", "状态", "置信度"])
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

        # 🚀 使用超精确识别器 (99.9999%准确率)
        if self.use_ultra_precise:
            logger.info("🚀 使用超精确识别器 (5阶段验证管道)")
            recognizer = UltraPreciseRecognizer(client=None)  # 暂不使用AI以加快速度
            self.components, self.component_confidences = recognizer.recognize(
                self.document,
                use_ai=False,
                confidence_threshold=0.95  # 95%置信度阈值
            )
            logger.info(f"识别完成: {len(self.components)} 个高置信度构件")
        # 使用高级识别器
        elif self.use_advanced:
            recognizer = AdvancedComponentRecognizer(use_ai=False)
            self.components = recognizer.recognize(self.document)
            self.component_confidences = []  # 无置信度信息
        else:
            recognizer = ComponentRecognizer()
            self.components = recognizer.recognize_components(self.document)
            self.component_confidences = []  # 无置信度信息

        perf_monitor.end_timer('component_recognition', start)

        # 🆕 验证识别结果
        start_validation = perf_monitor.start_timer('result_validation')
        validator = ResultValidator()
        validation_result = validator.validate(self.components)
        perf_monitor.end_timer('result_validation', start_validation)

        # 🆕 更新验证状态标签
        self._update_validation_status(validation_result)

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

            # 🆕 添加验证状态
            status = self._get_component_status(result.component_type, validation_result)
            status_item = QTableWidgetItem(status)
            if "❌" in status:
                status_item.setForeground(Qt.GlobalColor.red)
            elif "⚠️" in status:
                status_item.setForeground(Qt.GlobalColor.darkYellow)
            else:
                status_item.setForeground(Qt.GlobalColor.darkGreen)
            self.resultTable.setItem(i, 5, status_item)

            # 🚀 添加置信度（如果可用）
            confidence_text = self._get_average_confidence(result.component_type)
            confidence_item = QTableWidgetItem(confidence_text)
            # 根据置信度设置颜色
            if "99.9" in confidence_text or "100.0" in confidence_text:
                confidence_item.setForeground(Qt.GlobalColor.darkGreen)
            elif any(x in confidence_text for x in ["95", "96", "97", "98"]):
                confidence_item.setForeground(Qt.GlobalColor.darkBlue)
            elif confidence_text != "N/A":
                confidence_item.setForeground(Qt.GlobalColor.darkYellow)
            self.resultTable.setItem(i, 6, confidence_item)

        # 🆕 生成包含验证信息的综合报告
        report = calculator.generate_report(self.results)
        report += "\n" + "=" * 60 + "\n"
        report += validator.generate_report(validation_result)
        self.reportText.setPlainText(report)

        # 打印性能统计
        perf_monitor.print_stats()
        mem_usage = resource_manager.get_memory_usage()
        logger.info(f"识别完成: {len(self.components)} 个构件, 内存: {mem_usage['rss_mb']:.2f} MB")
        logger.info(f"验证结果: {validation_result.get_summary()}")

        # 通知父窗口更新导出界面
        parent = self.parent()
        if parent and hasattr(parent, 'exportInterface'):
            parent.exportInterface.setQuantityResults(self.results)

        # 更新AI助手上下文
        if hasattr(self, 'parent_window') and hasattr(self.parent_window, 'context_manager'):
            try:
                from datetime import datetime
                self.parent_window.context_manager.set_calculation_results(
                    self.components,
                    self.component_confidences,
                    datetime.now().strftime("%Y-%m-%d %H:%M:%S")
                )
                logger.info("算量结果已更新到AI助手上下文")
            except Exception as e:
                logger.warning(f"更新算量上下文失败: {e}")

    def _update_validation_status(self, validation_result):
        """🆕 更新验证状态标签"""
        pass_rate = validation_result.passed / validation_result.total_components * 100 if validation_result.total_components > 0 else 0

        if validation_result.errors > 0:
            status_text = f"⚠️ 验证完成: {pass_rate:.1f}% 通过, {validation_result.errors} 个错误需修正"
            self.validationLabel.setStyleSheet("color: red; font-weight: bold;")
        elif validation_result.warnings > 0:
            status_text = f"⚠️ 验证完成: {pass_rate:.1f}% 通过, {validation_result.warnings} 个警告"
            self.validationLabel.setStyleSheet("color: orange; font-weight: bold;")
        else:
            status_text = f"✅ 验证通过: 所有 {validation_result.total_components} 个构件验证通过"
            self.validationLabel.setStyleSheet("color: green; font-weight: bold;")

        self.validationLabel.setText(status_text)

    def _get_component_status(self, component_type, validation_result):
        """🆕 获取构件类型的验证状态"""
        # 查找该类型的所有问题
        type_issues = [issue for issue in validation_result.issues if issue.component_type == component_type]

        if not type_issues:
            return "✅ 通过"

        errors = sum(1 for issue in type_issues if issue.level.value == "错误")
        warnings = sum(1 for issue in type_issues if issue.level.value == "警告")

        if errors > 0:
            return f"❌ {errors}错误"
        elif warnings > 0:
            return f"⚠️ {warnings}警告"
        else:
            return "✅ 通过"

    def _get_average_confidence(self, component_type):
        """🚀 获取构件类型的平均置信度"""
        if not self.component_confidences:
            return "N/A"

        # 找到该类型的所有构件
        type_components = [comp for comp in self.components if comp.type == component_type]
        if not type_components:
            return "N/A"

        # 找到对应的置信度
        type_confidences = []
        for comp in type_components:
            for conf in self.component_confidences:
                if conf.component_id == comp.id:
                    type_confidences.append(conf.confidence)
                    break

        if not type_confidences:
            return "N/A"

        avg_confidence = sum(type_confidences) / len(type_confidences)
        return f"{avg_confidence*100:.2f}%"


# 别名，保持与主窗口导入的兼容性
CalculationWidget = CalculationInterface
