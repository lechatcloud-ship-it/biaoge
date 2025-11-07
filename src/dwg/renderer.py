"""
DWG渲染器（基于QPainter）
"""
from PyQt6.QtWidgets import QWidget
from PyQt6.QtGui import QPainter, QPen, QBrush, QColor, QFont, QPainterPath
from PyQt6.QtCore import Qt, QPointF, QRectF, QSizeF, pyqtSignal
from typing import List, Optional
import math

from .entities import (
    DWGDocument, Entity, LineEntity, CircleEntity,
    TextEntity, PolylineEntity, EntityType
)
from .spatial_index import SpatialIndex
from ..utils.logger import logger
from ..utils.performance import perf_monitor
from ..utils.resource_manager import resource_manager


class DWGCanvas(QWidget):
    """DWG画布 - 基于QPainter的高性能渲染器"""

    # 信号
    entityClicked = pyqtSignal(str)  # 实体被点击（entity_id）
    viewportChanged = pyqtSignal(float, QPointF)  # 视口改变（zoom, offset）

    def __init__(self, parent=None):
        super().__init__(parent)

        # 文档数据
        self.document: Optional[DWGDocument] = None
        self.visible_entities: List[Entity] = []

        # 视口变换
        self.zoom_level = 1.0
        self.pan_offset = QPointF(0, 0)
        self._pan_start = None

        # 背景色
        self.background_color = QColor(33, 33, 33)  # 深色背景

        # 渲染选项
        self.show_axes = True
        self.show_grid = False
        self.antialiasing = True

        # 图层可见性
        self.visible_layers = set()

        # 性能优化 - 商业级
        self.setMouseTracking(True)
        self.setAttribute(Qt.WidgetAttribute.WA_OpaquePaintEvent)
        self.setAttribute(Qt.WidgetAttribute.WA_NoSystemBackground)

        # 空间索引（商业级优化）
        self.spatial_index: Optional[SpatialIndex] = None
        self.use_spatial_index = True  # 默认启用

        # 渲染缓存
        self._viewport_bbox = None
        self._last_zoom = 1.0
        self._last_pan = QPointF(0, 0)

        # 设置最小尺寸
        self.setMinimumSize(400, 300)

        logger.info("DWG画布初始化完成（商业级）")

    def setDocument(self, document: DWGDocument):
        """设置DWG文档"""
        start = perf_monitor.start_timer('set_document')

        self.document = document

        # 初始化所有图层为可见
        self.visible_layers = {layer.name for layer in document.layers}

        # 构建空间索引（商业级优化）
        if self.use_spatial_index and len(document.entities) > 100:
            index_start = perf_monitor.start_timer('spatial_index_build')
            self.spatial_index = SpatialIndex()
            self.spatial_index.build(document.entities)
            perf_monitor.end_timer('spatial_index_build', index_start)
            logger.info(f"空间索引构建完成: {len(document.entities)}个实体")
        else:
            self.spatial_index = None

        # 更新可见实体
        self._updateVisibleEntities()

        # 自适应视图
        self.fitToView()

        # 检查内存
        resource_manager.check_memory_threshold()

        perf_monitor.end_timer('set_document', start)
        logger.info(f"设置文档: {len(document.entities)}个实体, {len(document.layers)}个图层")
        self.update()

    def _updateVisibleEntities(self):
        """更新可见实体列表（视锥剔除 + 图层过滤）- 商业级优化"""
        if not self.document:
            self.visible_entities = []
            return

        start = perf_monitor.start_timer('update_visible_entities')

        # 计算视口边界框
        viewport_bbox = self._calculateViewportBBox()

        # 使用空间索引查询（商业级优化）
        if self.spatial_index and viewport_bbox:
            # 空间索引查询
            entities_in_view = self.spatial_index.query(viewport_bbox)

            # 图层过滤
            self.visible_entities = [
                entity for entity in entities_in_view
                if entity.layer in self.visible_layers
            ]
        else:
            # 回退到简单过滤
            self.visible_entities = [
                entity for entity in self.document.entities
                if entity.layer in self.visible_layers
            ]

        perf_monitor.end_timer('update_visible_entities', start)
        logger.debug(f"可见实体: {len(self.visible_entities)}/{len(self.document.entities)}")

    def paintEvent(self, event):
        """绘制事件 - 商业级性能监控"""
        start = perf_monitor.start_timer('paint_event')

        painter = QPainter(self)

        # 抗锯齿
        if self.antialiasing:
            painter.setRenderHint(QPainter.RenderHint.Antialiasing)
            painter.setRenderHint(QPainter.RenderHint.TextAntialiasing)
            painter.setRenderHint(QPainter.RenderHint.SmoothPixmapTransform)

        # 绘制背景
        painter.fillRect(self.rect(), self.background_color)

        if not self.document:
            self._drawEmptyState(painter)
            perf_monitor.end_timer('paint_event', start)
            return

        # 检查视口是否变化，触发可见实体更新
        if self._viewportChanged():
            self._updateVisibleEntities()
            self._last_zoom = self.zoom_level
            self._last_pan = QPointF(self.pan_offset)

        # 应用视口变换
        painter.save()
        painter.translate(self.width() / 2, self.height() / 2)
        painter.translate(self.pan_offset)
        painter.scale(self.zoom_level, -self.zoom_level)  # Y轴翻转（CAD坐标系）

        # 绘制坐标轴
        if self.show_axes:
            self._drawAxes(painter)

        # 绘制实体（商业级优化）
        entity_start = perf_monitor.start_timer('draw_entities')
        for entity in self.visible_entities:
            self._drawEntity(painter, entity)
        perf_monitor.end_timer('draw_entities', entity_start)

        painter.restore()

        # 绘制UI叠加层
        self._drawOverlay(painter)

        perf_monitor.end_timer('paint_event', start)
        perf_monitor.increment('frame_count')

    def _drawEmptyState(self, painter: QPainter):
        """绘制空状态提示"""
        painter.setPen(QColor(150, 150, 150))
        font = QFont("Arial", 12)
        painter.setFont(font)
        painter.drawText(self.rect(), Qt.AlignmentFlag.AlignCenter,
                        "未加载DWG文件\n点击\"打开DWG文件\"开始")

    def _drawAxes(self, painter: QPainter):
        """绘制坐标轴"""
        pen = QPen(QColor(80, 80, 80))
        pen.setWidth(0)  # 1像素线
        painter.setPen(pen)

        # X轴（红色）
        painter.setPen(QColor(200, 80, 80))
        painter.drawLine(QPointF(-10000, 0), QPointF(10000, 0))

        # Y轴（绿色）
        painter.setPen(QColor(80, 200, 80))
        painter.drawLine(QPointF(0, -10000), QPointF(0, 10000))

    def _drawEntity(self, painter: QPainter, entity: Entity):
        """绘制单个实体"""
        try:
            if entity.entity_type == EntityType.LINE:
                self._drawLine(painter, entity)
            elif entity.entity_type == EntityType.CIRCLE:
                self._drawCircle(painter, entity)
            elif entity.entity_type == EntityType.TEXT:
                self._drawText(painter, entity)
            elif entity.entity_type == EntityType.POLYLINE:
                self._drawPolyline(painter, entity)
        except Exception as e:
            logger.warning(f"绘制实体失败 ({entity.id}): {e}")

    def _drawLine(self, painter: QPainter, line: LineEntity):
        """绘制线段"""
        pen = QPen(QColor(line.color))
        pen.setWidthF(max(line.lineweight, 0.5) / self.zoom_level)
        pen.setCosmetic(True)  # 线宽不随缩放变化
        painter.setPen(pen)

        start = QPointF(line.start[0], line.start[1])
        end = QPointF(line.end[0], line.end[1])
        painter.drawLine(start, end)

    def _drawCircle(self, painter: QPainter, circle: CircleEntity):
        """绘制圆"""
        pen = QPen(QColor(circle.color))
        pen.setWidthF(0.5 / self.zoom_level)
        pen.setCosmetic(True)
        painter.setPen(pen)
        painter.setBrush(Qt.BrushStyle.NoBrush)

        center = QPointF(circle.center[0], circle.center[1])
        # 因为Y轴翻转，需要用椭圆而非圆
        painter.drawEllipse(center, circle.radius, circle.radius)

    def _drawText(self, painter: QPainter, text: TextEntity):
        """绘制文本"""
        painter.save()

        # 获取显示文本（优先显示翻译）
        display_text = text.translated_text if text.translated_text else text.text

        if not display_text:
            painter.restore()
            return

        # 设置颜色
        painter.setPen(QColor(text.color))

        # 移动到文本位置
        pos = QPointF(text.position[0], text.position[1])
        painter.translate(pos)

        # Y轴翻转回来（文本不需要翻转）
        painter.scale(1, -1)

        # 旋转（CAD是逆时针）
        if text.rotation != 0:
            painter.rotate(-text.rotation)

        # 设置字体
        font = QFont(text.style if text.style else "Arial")
        font.setPointSizeF(text.height / self.zoom_level)
        painter.setFont(font)

        # 绘制文本
        painter.drawText(QPointF(0, 0), display_text)

        painter.restore()

    def _drawPolyline(self, painter: QPainter, polyline: PolylineEntity):
        """绘制多段线"""
        if not polyline.points or len(polyline.points) < 2:
            return

        pen = QPen(QColor(polyline.color))
        pen.setWidthF(max(polyline.lineweight, 0.5) / self.zoom_level)
        pen.setCosmetic(True)
        painter.setPen(pen)
        painter.setBrush(Qt.BrushStyle.NoBrush)

        # 构建路径
        path = QPainterPath()
        first_point = polyline.points[0]
        path.moveTo(first_point[0], first_point[1])

        for point in polyline.points[1:]:
            path.lineTo(point[0], point[1])

        # 闭合多段线
        if polyline.closed:
            path.closeSubpath()

        painter.drawPath(path)

    def _drawOverlay(self, painter: QPainter):
        """绘制UI叠加层（缩放级别等）"""
        painter.setPen(QColor(200, 200, 200))
        font = QFont("Arial", 10)
        painter.setFont(font)

        # 显示缩放级别
        zoom_text = f"缩放: {self.zoom_level:.2f}x"
        painter.drawText(10, 20, zoom_text)

        # 显示实体数量
        if self.document:
            entity_text = f"实体: {len(self.visible_entities)}/{len(self.document.entities)}"
            painter.drawText(10, 40, entity_text)

    # ==================== 交互事件 ====================

    def wheelEvent(self, event):
        """鼠标滚轮缩放"""
        delta = event.angleDelta().y()
        factor = 1.15 if delta > 0 else 1 / 1.15

        old_zoom = self.zoom_level
        self.zoom_level *= factor
        self.zoom_level = max(0.01, min(100.0, self.zoom_level))

        # 以鼠标位置为中心缩放
        if old_zoom != self.zoom_level:
            # 计算鼠标在世界坐标的位置
            mouse_pos = event.position()
            widget_center = QPointF(self.width() / 2, self.height() / 2)
            offset_from_center = mouse_pos - widget_center

            # 调整平移偏移以保持鼠标位置不变
            scale_factor = self.zoom_level / old_zoom
            self.pan_offset = self.pan_offset * scale_factor + offset_from_center * (1 - scale_factor)

            self.viewportChanged.emit(self.zoom_level, self.pan_offset)
            self.update()

    def mousePressEvent(self, event):
        """鼠标按下"""
        if event.button() == Qt.MouseButton.MiddleButton:
            self._pan_start = event.position()
            self.setCursor(Qt.CursorShape.ClosedHandCursor)

    def mouseMoveEvent(self, event):
        """鼠标移动"""
        if event.buttons() & Qt.MouseButton.MiddleButton and self._pan_start:
            delta = event.position() - self._pan_start
            self.pan_offset += delta
            self._pan_start = event.position()
            self.viewportChanged.emit(self.zoom_level, self.pan_offset)
            self.update()

    def mouseReleaseEvent(self, event):
        """鼠标释放"""
        if event.button() == Qt.MouseButton.MiddleButton:
            self._pan_start = None
            self.setCursor(Qt.CursorShape.ArrowCursor)

    # ==================== 视图控制 ====================

    def fitToView(self):
        """自适应视图"""
        if not self.document or not self.document.entities:
            return

        # 计算边界框
        bbox = self._calculateBoundingBox()
        if not bbox:
            return

        min_x, min_y, max_x, max_y = bbox
        width = max_x - min_x
        height = max_y - min_y

        if width == 0 or height == 0:
            return

        # 计算缩放级别
        margin = 0.9  # 留10%边距
        zoom_x = (self.width() * margin) / width
        zoom_y = (self.height() * margin) / height
        self.zoom_level = min(zoom_x, zoom_y)

        # 居中
        center_x = (min_x + max_x) / 2
        center_y = (min_y + max_y) / 2
        self.pan_offset = QPointF(-center_x * self.zoom_level, center_y * self.zoom_level)

        self.viewportChanged.emit(self.zoom_level, self.pan_offset)
        self.update()

        logger.info(f"自适应视图: bbox=({min_x:.2f}, {min_y:.2f}, {max_x:.2f}, {max_y:.2f}), zoom={self.zoom_level:.2f}")

    def _calculateBoundingBox(self):
        """计算所有实体的边界框"""
        if not self.document or not self.document.entities:
            return None

        min_x = min_y = float('inf')
        max_x = max_y = float('-inf')

        for entity in self.document.entities:
            if entity.entity_type == EntityType.LINE:
                min_x = min(min_x, entity.start[0], entity.end[0])
                max_x = max(max_x, entity.start[0], entity.end[0])
                min_y = min(min_y, entity.start[1], entity.end[1])
                max_y = max(max_y, entity.start[1], entity.end[1])

            elif entity.entity_type == EntityType.CIRCLE:
                min_x = min(min_x, entity.center[0] - entity.radius)
                max_x = max(max_x, entity.center[0] + entity.radius)
                min_y = min(min_y, entity.center[1] - entity.radius)
                max_y = max(max_y, entity.center[1] + entity.radius)

            elif entity.entity_type == EntityType.TEXT:
                min_x = min(min_x, entity.position[0])
                max_x = max(max_x, entity.position[0])
                min_y = min(min_y, entity.position[1])
                max_y = max(max_y, entity.position[1])

            elif entity.entity_type == EntityType.POLYLINE:
                for point in entity.points:
                    min_x = min(min_x, point[0])
                    max_x = max(max_x, point[0])
                    min_y = min(min_y, point[1])
                    max_y = max(max_y, point[1])

        if min_x == float('inf'):
            return None

        return (min_x, min_y, max_x, max_y)

    def _calculateViewportBBox(self):
        """计算视口边界框（世界坐标）- 商业级优化"""
        if not self.document:
            return None

        # 视口中心在世界坐标
        center_x = -self.pan_offset.x() / self.zoom_level
        center_y = self.pan_offset.y() / self.zoom_level

        # 视口半宽高（世界坐标）
        half_width = (self.width() / 2) / self.zoom_level
        half_height = (self.height() / 2) / self.zoom_level

        # 返回边界框 (min_x, min_y, max_x, max_y)
        return (
            center_x - half_width,
            center_y - half_height,
            center_x + half_width,
            center_y + half_height
        )

    def _viewportChanged(self) -> bool:
        """检查视口是否变化"""
        zoom_changed = abs(self.zoom_level - self._last_zoom) > 0.01
        pan_changed = (self.pan_offset - self._last_pan).manhattanLength() > 1.0
        return zoom_changed or pan_changed

    def resetView(self):
        """重置视图"""
        self.zoom_level = 1.0
        self.pan_offset = QPointF(0, 0)
        self.viewportChanged.emit(self.zoom_level, self.pan_offset)
        self.update()

    def zoomIn(self):
        """放大"""
        self.zoom_level *= 1.25
        self.zoom_level = min(self.zoom_level, 100.0)
        self.viewportChanged.emit(self.zoom_level, self.pan_offset)
        self.update()

    def zoomOut(self):
        """缩小"""
        self.zoom_level /= 1.25
        self.zoom_level = max(self.zoom_level, 0.01)
        self.viewportChanged.emit(self.zoom_level, self.pan_offset)
        self.update()

    # ==================== 图层控制 ====================

    def setLayerVisible(self, layer_name: str, visible: bool):
        """设置图层可见性"""
        if visible:
            self.visible_layers.add(layer_name)
        else:
            self.visible_layers.discard(layer_name)

        self._updateVisibleEntities()
        self.update()

    def setAllLayersVisible(self, visible: bool):
        """设置所有图层可见性"""
        if visible:
            self.visible_layers = {layer.name for layer in self.document.layers}
        else:
            self.visible_layers.clear()

        self._updateVisibleEntities()
        self.update()
