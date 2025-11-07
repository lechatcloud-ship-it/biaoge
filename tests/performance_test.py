"""
性能基准测试 - 验证商业级标准

测试目标：
- 渲染性能：50K+实体 < 100ms帧时间
- 内存占用：< 500MB
- 解析性能：大型文件 < 5s
- 翻译性能：批量翻译效率
"""
import time
import psutil
import os
from pathlib import Path
import sys

# 添加项目路径
sys.path.insert(0, str(Path(__file__).parent.parent))

from src.dwg.entities import (
    DWGDocument, LineEntity, CircleEntity,
    TextEntity, EntityType, Layer
)
from src.dwg.spatial_index import SpatialIndex
from src.utils.performance import perf_monitor
from src.utils.resource_manager import resource_manager
from src.calculation.advanced_recognizer import AdvancedComponentRecognizer
from src.export.advanced_dwg_exporter import AdvancedDWGExporter


class PerformanceTest:
    """性能测试套件"""

    def __init__(self):
        self.results = {}
        self.process = psutil.Process(os.getpid())

    def run_all_tests(self):
        """运行所有测试"""
        print("=" * 60)
        print("商业级性能基准测试")
        print("=" * 60)
        print()

        self.test_spatial_index_performance()
        self.test_large_document_creation()
        self.test_memory_usage()
        self.test_recognizer_performance()
        self.test_exporter_performance()

        self.print_summary()

    def test_spatial_index_performance(self):
        """测试空间索引性能"""
        print("1. 空间索引性能测试")
        print("-" * 60)

        # 创建测试实体
        entity_counts = [1000, 10000, 50000]

        for count in entity_counts:
            entities = self._create_test_entities(count)

            # 构建索引
            start = time.perf_counter()
            spatial_index = SpatialIndex()
            spatial_index.build(entities)
            build_time = (time.perf_counter() - start) * 1000

            # 查询测试
            bbox = (0, 0, 1000, 1000)
            start = time.perf_counter()
            results = spatial_index.query(bbox)
            query_time = (time.perf_counter() - start) * 1000

            print(f"  实体数: {count:6d} | "
                  f"构建: {build_time:6.2f}ms | "
                  f"查询: {query_time:6.2f}ms | "
                  f"结果: {len(results):6d}")

            self.results[f'spatial_index_build_{count}'] = build_time
            self.results[f'spatial_index_query_{count}'] = query_time

        print()

    def test_large_document_creation(self):
        """测试大型文档创建"""
        print("2. 大型文档创建测试")
        print("-" * 60)

        counts = [10000, 50000, 100000]

        for count in counts:
            start = time.perf_counter()
            entities = self._create_test_entities(count)
            create_time = (time.perf_counter() - start) * 1000

            memory_mb = self.process.memory_info().rss / 1024 / 1024

            print(f"  实体数: {count:6d} | "
                  f"创建: {create_time:6.2f}ms | "
                  f"内存: {memory_mb:6.2f}MB")

            self.results[f'document_create_{count}'] = create_time
            self.results[f'memory_usage_{count}'] = memory_mb

            # 清理
            del entities

        print()

    def test_memory_usage(self):
        """测试内存管理"""
        print("3. 内存管理测试")
        print("-" * 60)

        before = resource_manager.get_memory_usage()
        print(f"  初始内存: {before['rss_mb']:.2f}MB")

        # 创建大量实体
        entities = self._create_test_entities(50000)
        after_create = resource_manager.get_memory_usage()
        print(f"  创建50K实体后: {after_create['rss_mb']:.2f}MB")

        # 优化内存
        freed = resource_manager.optimize_memory()
        after_optimize = resource_manager.get_memory_usage()
        print(f"  优化后: {after_optimize['rss_mb']:.2f}MB")
        print(f"  释放: {freed:.2f}MB")

        self.results['memory_optimization'] = freed

        print()

    def test_recognizer_performance(self):
        """测试构件识别性能"""
        print("4. 构件识别性能测试")
        print("-" * 60)

        # 创建测试文档
        document = self._create_test_document_with_text()

        recognizer = AdvancedComponentRecognizer(use_ai=False)

        start = time.perf_counter()
        components = recognizer.recognize(document)
        recognize_time = (time.perf_counter() - start) * 1000

        print(f"  文本实体: {len([e for e in document.entities if e.entity_type == EntityType.TEXT])}")
        print(f"  识别时间: {recognize_time:.2f}ms")
        print(f"  识别构件: {len(components)}")

        self.results['recognition_time'] = recognize_time
        self.results['components_found'] = len(components)

        print()

    def test_exporter_performance(self):
        """测试导出性能"""
        print("5. DWG导出性能测试")
        print("-" * 60)

        # 创建测试文档
        document = self._create_test_document_with_text()

        exporter = AdvancedDWGExporter()

        output_path = Path(__file__).parent / 'test_export.dxf'

        start = time.perf_counter()
        success = exporter.export(document, str(output_path), version='R2018')
        export_time = (time.perf_counter() - start) * 1000

        print(f"  实体数: {len(document.entities)}")
        print(f"  导出时间: {export_time:.2f}ms")
        print(f"  导出状态: {'成功' if success else '失败'}")

        if output_path.exists():
            file_size = output_path.stat().st_size / 1024
            print(f"  文件大小: {file_size:.2f}KB")
            output_path.unlink()  # 清理

        self.results['export_time'] = export_time

        print()

    def _create_test_entities(self, count: int) -> list:
        """创建测试实体"""
        entities = []

        for i in range(count):
            # 混合不同类型的实体
            entity_type = i % 4

            if entity_type == 0:
                # 线段
                entity = LineEntity(
                    id=f"line_{i}",
                    entity_type=EntityType.LINE,
                    start=(i * 10.0, 0.0, 0.0),
                    end=(i * 10.0 + 100, 100.0, 0.0),
                    layer='0',
                    color=7
                )
            elif entity_type == 1:
                # 圆
                entity = CircleEntity(
                    id=f"circle_{i}",
                    entity_type=EntityType.CIRCLE,
                    center=(i * 10.0, i * 10.0, 0.0),
                    radius=50.0,
                    layer='0',
                    color=7
                )
            elif entity_type == 2:
                # 文本
                entity = TextEntity(
                    id=f"text_{i}",
                    entity_type=EntityType.TEXT,
                    text=f"Text {i}",
                    position=(i * 10.0, i * 10.0, 0.0),
                    height=10.0,
                    layer='0',
                    color=7
                )
            else:
                # 默认线段
                entity = LineEntity(
                    id=f"line_{i}",
                    entity_type=EntityType.LINE,
                    start=(i * 10.0, 0.0, 0.0),
                    end=(i * 10.0 + 50, 50.0, 0.0),
                    layer='0',
                    color=7
                )

            entities.append(entity)

        return entities

    def _create_test_document_with_text(self) -> DWGDocument:
        """创建包含文本的测试文档"""
        document = DWGDocument()
        document.layers.append(Layer(
            name='0',
            color=7,
            linetype='Continuous',
            lineweight=0
        ))

        # 添加建筑构件文本
        test_texts = [
            "KL-1 300×600",
            "KZ-1 500×500",
            "墙 200厚 C30",
            "楼板 120厚",
            "框架梁 KL-2 250×500",
            "框架柱 KZ-2 600×600",
            "内墙 NQ-1 200",
            "外墙 WQ-1 240",
        ]

        for i, text in enumerate(test_texts):
            entity = TextEntity(
                id=f"text_{i}",
                entity_type=EntityType.TEXT,
                text=text,
                position=(i * 100.0, i * 100.0, 0.0),
                height=10.0,
                layer='0',
                color=7
            )
            document.entities.append(entity)

        # 添加一些几何实体
        for i in range(100):
            line = LineEntity(
                id=f"line_{i}",
                entity_type=EntityType.LINE,
                start=(i * 10.0, 0.0, 0.0),
                end=(i * 10.0 + 100, 100.0, 0.0),
                layer='0',
                color=7
            )
            document.entities.append(line)

        return document

    def print_summary(self):
        """打印测试总结"""
        print("=" * 60)
        print("测试总结")
        print("=" * 60)
        print()

        # 检查商业级标准
        passed = []
        failed = []

        # 标准1：50K实体空间索引查询 < 10ms
        if 'spatial_index_query_50000' in self.results:
            query_time = self.results['spatial_index_query_50000']
            if query_time < 10:
                passed.append(f"✅ 50K实体查询: {query_time:.2f}ms < 10ms")
            else:
                failed.append(f"❌ 50K实体查询: {query_time:.2f}ms >= 10ms")

        # 标准2：内存占用 < 500MB
        if 'memory_usage_50000' in self.results:
            memory = self.results['memory_usage_50000']
            if memory < 500:
                passed.append(f"✅ 内存占用: {memory:.2f}MB < 500MB")
            else:
                failed.append(f"❌ 内存占用: {memory:.2f}MB >= 500MB")

        # 标准3：构件识别 < 100ms
        if 'recognition_time' in self.results:
            rec_time = self.results['recognition_time']
            if rec_time < 100:
                passed.append(f"✅ 构件识别: {rec_time:.2f}ms < 100ms")
            else:
                failed.append(f"❌ 构件识别: {rec_time:.2f}ms >= 100ms")

        # 标准4：导出性能 < 200ms（小文件）
        if 'export_time' in self.results:
            export_time = self.results['export_time']
            if export_time < 200:
                passed.append(f"✅ DWG导出: {export_time:.2f}ms < 200ms")
            else:
                failed.append(f"❌ DWG导出: {export_time:.2f}ms >= 200ms")

        print("通过的标准:")
        for item in passed:
            print(f"  {item}")

        if failed:
            print("\n未通过的标准:")
            for item in failed:
                print(f"  {item}")

        print()
        print(f"总计: {len(passed)}/{len(passed) + len(failed)} 项通过")
        print()

        # 性能监控统计
        print("性能监控统计:")
        # 显示主要性能指标
        key_metrics = ['component_recognition', 'dwg_export']

        for metric in key_metrics:
            try:
                stats = perf_monitor.get_stats(metric)
                if stats and stats.get('count', 0) > 0:
                    print(f"  {metric:30s}: avg={stats['avg']:6.2f}ms, count={stats['count']}")
            except:
                pass

        print()
        print("=" * 60)


if __name__ == '__main__':
    test = PerformanceTest()
    test.run_all_tests()
