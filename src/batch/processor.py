"""
批量文件处理器
"""
from enum import Enum
from pathlib import Path
from typing import List, Optional, Callable, Dict, Any
from dataclasses import dataclass, field
from datetime import datetime
import traceback

from ..dwg.parser import DWGParser, DWGParseError, DWGPasswordError
from ..dwg.entities import DWGDocument
from ..dwg.smart_translator import SmartTranslator
from ..utils.logger import logger


class TaskStatus(Enum):
    """任务状态"""
    PENDING = "pending"  # 等待中
    PROCESSING = "processing"  # 处理中
    COMPLETED = "completed"  # 已完成
    FAILED = "failed"  # 失败
    SKIPPED = "skipped"  # 跳过


@dataclass
class BatchTask:
    """批量处理任务"""
    file_path: Path
    status: TaskStatus = TaskStatus.PENDING
    document: Optional[DWGDocument] = None
    error_message: Optional[str] = None
    start_time: Optional[datetime] = None
    end_time: Optional[datetime] = None
    progress: float = 0.0  # 0.0 - 1.0

    # 翻译相关
    translate: bool = True
    translation_done: bool = False

    # 导出相关
    export_path: Optional[Path] = None

    @property
    def duration(self) -> Optional[float]:
        """处理时长（秒）"""
        if self.start_time and self.end_time:
            return (self.end_time - self.start_time).total_seconds()
        return None

    @property
    def filename(self) -> str:
        """文件名"""
        return self.file_path.name

    @property
    def status_text(self) -> str:
        """状态文本"""
        status_map = {
            TaskStatus.PENDING: "等待中",
            TaskStatus.PROCESSING: "处理中",
            TaskStatus.COMPLETED: "已完成",
            TaskStatus.FAILED: "失败",
            TaskStatus.SKIPPED: "已跳过"
        }
        return status_map.get(self.status, "未知")


class BatchProcessor:
    """
    批量文件处理器

    功能：
    - 批量解析DWG文件
    - 批量翻译
    - 批量导出
    - 进度跟踪
    - 错误处理
    """

    def __init__(self):
        """初始化批量处理器"""
        self.tasks: List[BatchTask] = []
        self.current_task_index: int = -1
        self.is_processing: bool = False
        self.is_cancelled: bool = False

        # 回调函数
        self.on_task_start: Optional[Callable[[BatchTask], None]] = None
        self.on_task_progress: Optional[Callable[[BatchTask, float], None]] = None
        self.on_task_complete: Optional[Callable[[BatchTask], None]] = None
        self.on_task_failed: Optional[Callable[[BatchTask, str], None]] = None
        self.on_all_complete: Optional[Callable[[], None]] = None

    def add_files(self, file_paths: List[str | Path]):
        """
        添加文件到批处理队列

        Args:
            file_paths: 文件路径列表
        """
        for path in file_paths:
            file_path = Path(path)
            if file_path.exists() and file_path.suffix.lower() in ['.dwg', '.dxf']:
                task = BatchTask(file_path=file_path)
                self.tasks.append(task)
                logger.info(f"添加批处理任务: {file_path.name}")

    def clear_tasks(self):
        """清空所有任务"""
        self.tasks.clear()
        self.current_task_index = -1
        logger.info("清空批处理任务列表")

    def remove_task(self, index: int):
        """移除指定任务"""
        if 0 <= index < len(self.tasks):
            task = self.tasks.pop(index)
            logger.info(f"移除批处理任务: {task.filename}")

    def get_statistics(self) -> Dict[str, Any]:
        """
        获取批处理统计信息

        Returns:
            统计字典
        """
        total = len(self.tasks)
        completed = sum(1 for t in self.tasks if t.status == TaskStatus.COMPLETED)
        failed = sum(1 for t in self.tasks if t.status == TaskStatus.FAILED)
        skipped = sum(1 for t in self.tasks if t.status == TaskStatus.SKIPPED)
        pending = sum(1 for t in self.tasks if t.status == TaskStatus.PENDING)
        processing = sum(1 for t in self.tasks if t.status == TaskStatus.PROCESSING)

        total_duration = sum(
            t.duration for t in self.tasks
            if t.duration is not None
        )

        return {
            'total': total,
            'completed': completed,
            'failed': failed,
            'skipped': skipped,
            'pending': pending,
            'processing': processing,
            'total_duration': total_duration,
            'success_rate': (completed / total * 100) if total > 0 else 0.0
        }

    def process_all(
        self,
        translate: bool = True,
        export: bool = False,
        export_dir: Optional[Path] = None
    ):
        """
        处理所有任务

        Args:
            translate: 是否翻译
            export: 是否导出
            export_dir: 导出目录
        """
        if self.is_processing:
            logger.warning("批处理正在进行中，无法重复启动")
            return

        self.is_processing = True
        self.is_cancelled = False

        logger.info(f"开始批量处理，共 {len(self.tasks)} 个任务")

        try:
            for index, task in enumerate(self.tasks):
                if self.is_cancelled:
                    logger.info("批处理已取消")
                    break

                self.current_task_index = index
                task.translate = translate

                if export and export_dir:
                    task.export_path = export_dir / f"{task.file_path.stem}_translated.dwg"

                self._process_task(task)

        finally:
            self.is_processing = False

            if self.on_all_complete:
                self.on_all_complete()

            stats = self.get_statistics()
            logger.info(
                f"批处理完成: "
                f"成功 {stats['completed']}, "
                f"失败 {stats['failed']}, "
                f"跳过 {stats['skipped']}, "
                f"总耗时 {stats['total_duration']:.2f}秒"
            )

    def cancel(self):
        """取消批处理"""
        if self.is_processing:
            self.is_cancelled = True
            logger.info("请求取消批处理")

    def _process_task(self, task: BatchTask):
        """
        处理单个任务

        Args:
            task: 批处理任务
        """
        task.status = TaskStatus.PROCESSING
        task.start_time = datetime.now()
        task.progress = 0.0

        if self.on_task_start:
            self.on_task_start(task)

        try:
            # 步骤1: 解析DWG文件 (30%)
            logger.info(f"解析文件: {task.filename}")
            parser = DWGParser()
            task.document = parser.parse(str(task.file_path))
            task.progress = 0.3

            if self.on_task_progress:
                self.on_task_progress(task, task.progress)

            # 步骤2: 翻译 (60%)
            if task.translate and task.document:
                logger.info(f"翻译文件: {task.filename}")
                translator = SmartTranslator()

                # 提取所有文本实体
                text_entities = [
                    e for e in task.document.entities
                    if hasattr(e, 'text') and e.text
                ]

                total_texts = len(text_entities)
                for idx, entity in enumerate(text_entities):
                    if self.is_cancelled:
                        raise Exception("用户取消操作")

                    # 翻译文本
                    translated = translator.translate(
                        entity.text,
                        from_lang="auto",
                        to_lang="zh-CN"
                    )
                    entity.text = translated

                    # 更新进度 (30% - 90%)
                    progress = 0.3 + (0.6 * (idx + 1) / total_texts)
                    task.progress = progress

                    if self.on_task_progress:
                        self.on_task_progress(task, progress)

                task.translation_done = True

            # 步骤3: 导出 (10%)
            if task.export_path:
                logger.info(f"导出文件: {task.export_path.name}")
                # TODO: 实现导出逻辑
                task.progress = 1.0
            else:
                task.progress = 1.0

            # 完成
            task.status = TaskStatus.COMPLETED
            task.end_time = datetime.now()

            if self.on_task_complete:
                self.on_task_complete(task)

            logger.info(f"任务完成: {task.filename} (耗时 {task.duration:.2f}秒)")

        except DWGPasswordError as e:
            # 密码错误 - 跳过
            task.status = TaskStatus.SKIPPED
            task.error_message = "文件已加密，需要密码"
            task.end_time = datetime.now()

            logger.warning(f"任务跳过（加密文件）: {task.filename}")

            if self.on_task_failed:
                self.on_task_failed(task, task.error_message)

        except DWGParseError as e:
            # 解析错误
            task.status = TaskStatus.FAILED
            task.error_message = f"解析错误: {str(e)[:100]}"
            task.end_time = datetime.now()

            logger.error(f"任务失败: {task.filename} - {task.error_message}")

            if self.on_task_failed:
                self.on_task_failed(task, task.error_message)

        except Exception as e:
            # 其他错误
            task.status = TaskStatus.FAILED
            task.error_message = f"处理失败: {str(e)[:100]}"
            task.end_time = datetime.now()

            logger.error(
                f"任务失败: {task.filename} - {task.error_message}",
                exc_info=True
            )

            if self.on_task_failed:
                self.on_task_failed(task, task.error_message)
