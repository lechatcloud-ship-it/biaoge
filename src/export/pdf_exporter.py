"""PDF导出器"""
from reportlab.pdfgen import canvas
from reportlab.lib.pagesizes import A4
from ..dwg.entities import DWGDocument
from ..utils.logger import logger

class PDFExporter:
    def export(self, document: DWGDocument, output_path: str):
        """导出为PDF"""
        c = canvas.Canvas(output_path, pagesize=A4)
        c.drawString(100, 800, f"DWG文档: {document.metadata.get('filename', 'N/A')}")
        c.drawString(100, 780, f"实体数: {document.entity_count}")
        c.save()
        logger.info(f"导出PDF: {output_path}")
