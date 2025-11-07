"""Excel导出器"""
from openpyxl import Workbook
from ..calculation.quantity_calculator import QuantityResult
from ..utils.logger import logger

class ExcelExporter:
    def export_quantity(self, results: dict, output_path: str):
        """导出工程量报表"""
        wb = Workbook()
        ws = wb.active
        ws.title = "工程量报表"
        
        ws.append(["构件类型", "数量", "体积(m³)", "面积(m²)", "费用(元)"])
        
        total_cost = 0
        for result in results.values():
            ws.append([
                result.component_type.value,
                result.count,
                f"{result.total_volume:.2f}",
                f"{result.total_area:.2f}",
                f"{result.total_cost:.2f}"
            ])
            total_cost += result.total_cost
        
        ws.append([])
        ws.append(["合计", "", "", "", f"{total_cost:.2f}"])
        
        wb.save(output_path)
        logger.info(f"导出Excel: {output_path}")
