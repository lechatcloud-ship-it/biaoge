"""
批量处理功能测试
"""
import unittest
import tempfile
import shutil
from pathlib import Path
import ezdxf

from src.batch.processor import BatchProcessor, BatchTask, TaskStatus
from src.utils.logger import logger


class TestBatchProcessing(unittest.TestCase):
    """批量处理测试"""

    def setUp(self):
        """设置测试环境"""
        # 创建临时目录
        self.test_dir = Path(tempfile.mkdtemp())

        # 创建测试DWG文件
        self.test_files = []
        for i in range(3):
            dwg_file = self.test_dir / f"test_{i}.dwg"
            doc = ezdxf.new('R2010')
            msp = doc.modelspace()

            # 添加一些测试内容
            msp.add_text(
                f"Test Text {i}",
                dxfattribs={'insert': (0, i * 10), 'height': 2.5}
            )
            msp.add_line((0, 0), (100, 100))

            doc.saveas(dwg_file)
            self.test_files.append(dwg_file)

        logger.info(f"创建测试文件: {len(self.test_files)} 个")

    def tearDown(self):
        """清理测试环境"""
        shutil.rmtree(self.test_dir)
        logger.info("清理测试环境完成")

    def test_01_add_files(self):
        """测试添加文件到批处理列表"""
        processor = BatchProcessor()

        # 添加文件
        processor.add_files(self.test_files)

        self.assertEqual(len(processor.tasks), 3)
        for task in processor.tasks:
            self.assertEqual(task.status, TaskStatus.PENDING)

        logger.info("✓ 测试01: 添加文件成功")

    def test_02_remove_task(self):
        """测试移除任务"""
        processor = BatchProcessor()
        processor.add_files(self.test_files)

        # 移除第一个任务
        processor.remove_task(0)

        self.assertEqual(len(processor.tasks), 2)
        logger.info("✓ 测试02: 移除任务成功")

    def test_03_clear_tasks(self):
        """测试清空任务列表"""
        processor = BatchProcessor()
        processor.add_files(self.test_files)

        # 清空任务
        processor.clear_tasks()

        self.assertEqual(len(processor.tasks), 0)
        logger.info("✓ 测试03: 清空任务成功")

    def test_04_get_statistics(self):
        """测试获取统计信息"""
        processor = BatchProcessor()
        processor.add_files(self.test_files)

        stats = processor.get_statistics()

        self.assertEqual(stats['total'], 3)
        self.assertEqual(stats['pending'], 3)
        self.assertEqual(stats['completed'], 0)
        self.assertEqual(stats['failed'], 0)

        logger.info("✓ 测试04: 统计信息正确")

    def test_05_process_single_file(self):
        """测试处理单个文件"""
        processor = BatchProcessor()
        processor.add_files([self.test_files[0]])

        # 不翻译（避免调用API）
        processor.process_all(translate=False, export=False)

        task = processor.tasks[0]
        self.assertEqual(task.status, TaskStatus.COMPLETED)
        self.assertIsNotNone(task.document)
        self.assertIsNotNone(task.duration)
        self.assertEqual(task.progress, 1.0)

        logger.info(f"✓ 测试05: 单文件处理成功 (耗时 {task.duration:.2f}秒)")

    def test_06_process_multiple_files(self):
        """测试处理多个文件"""
        processor = BatchProcessor()
        processor.add_files(self.test_files)

        # 不翻译（避免调用API）
        processor.process_all(translate=False, export=False)

        stats = processor.get_statistics()
        self.assertEqual(stats['completed'], 3)
        self.assertEqual(stats['failed'], 0)
        self.assertEqual(stats['success_rate'], 100.0)

        logger.info(f"✓ 测试06: 多文件处理成功 (总耗时 {stats['total_duration']:.2f}秒)")

    def test_07_handle_invalid_file(self):
        """测试处理无效文件"""
        processor = BatchProcessor()

        # 创建一个无效的DWG文件（实际上是文本文件）
        invalid_file = self.test_dir / "invalid.dwg"
        invalid_file.write_text("This is not a valid DWG file")

        processor.add_files([invalid_file])
        processor.process_all(translate=False, export=False)

        task = processor.tasks[0]
        self.assertEqual(task.status, TaskStatus.FAILED)
        self.assertIsNotNone(task.error_message)

        logger.info("✓ 测试07: 无效文件处理正确（标记为失败）")

    def test_08_callback_invoked(self):
        """测试回调函数被正确调用"""
        processor = BatchProcessor()
        processor.add_files([self.test_files[0]])

        # 设置回调标记
        callback_flags = {
            'task_start': False,
            'task_progress': False,
            'task_complete': False,
            'all_complete': False
        }

        def on_start(task):
            callback_flags['task_start'] = True

        def on_progress(task, progress):
            callback_flags['task_progress'] = True

        def on_complete(task):
            callback_flags['task_complete'] = True

        def on_all_complete():
            callback_flags['all_complete'] = True

        processor.on_task_start = on_start
        processor.on_task_progress = on_progress
        processor.on_task_complete = on_complete
        processor.on_all_complete = on_all_complete

        processor.process_all(translate=False, export=False)

        # 验证所有回调都被调用了
        self.assertTrue(callback_flags['task_start'])
        self.assertTrue(callback_flags['task_progress'])
        self.assertTrue(callback_flags['task_complete'])
        self.assertTrue(callback_flags['all_complete'])

        logger.info("✓ 测试08: 回调函数正确调用")

    def test_09_batch_task_properties(self):
        """测试BatchTask属性"""
        task = BatchTask(file_path=self.test_files[0])

        self.assertEqual(task.filename, self.test_files[0].name)
        self.assertEqual(task.status_text, "等待中")
        self.assertIsNone(task.duration)

        # 模拟任务执行
        from datetime import datetime, timedelta
        task.start_time = datetime.now()
        task.end_time = task.start_time + timedelta(seconds=2.5)

        self.assertAlmostEqual(task.duration, 2.5, delta=0.1)

        logger.info("✓ 测试09: BatchTask属性正确")


if __name__ == '__main__':
    unittest.main()
