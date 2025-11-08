# -*- coding: utf-8 -*-
"""
大规模工程量计算性能测试

模拟真实项目规模的测试
"""
import pytest
import sys
import time
from pathlib import Path

project_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(project_root))

from src.calculation.component_recognizer import ComponentRecognizer, Component, ComponentType
from src.calculation.result_validator import ResultValidator
from src.calculation.quantity_calculator import QuantityCalculator
from src.dwg.entities import DWGDocument, TextEntity, PolylineEntity


class TestDataGenerator:
    """测试数据生成器"""

    @staticmethod
    def generate_text_entity(entity_id, text, position=(0, 0, 0)):
        """生成文本实体"""
        return TextEntity(id=entity_id, position=position, text=text)

    @staticmethod
    def generate_component(comp_id, comp_type, dimensions):
        """生成构件"""
        return Component(
            id=comp_id,
            type=comp_type,
            name=f"{comp_type.value}_{comp_id}",
            entities=[],
            properties={},
            dimensions=dimensions
        )

    @staticmethod
    def generate_dwg_document(num_text_entities=100):
        """生成DWG文档（含大量文本标注）"""
        doc = DWGDocument()
        doc.entities = []

        # 生成各类构件标注
        component_templates = [
            ("KL{}", "300×600"),  # 梁
            ("KZ{}", "600×600"),  # 柱
            ("φ{}", "500"),       # 圆柱
            ("墙{}", "200厚"),     # 墙
            ("板{}", "120厚"),     # 板
        ]

        for i in range(num_text_entities):
            template_idx = i % len(component_templates)
            name_template, dim_template = component_templates[template_idx]

            text = f"{name_template.format(i)} {dim_template}"
            position = (i * 100, (i % 10) * 100, 0)  # 分散位置

            entity = TestDataGenerator.generate_text_entity(
                f"text_{i}", text, position
            )
            doc.entities.append(entity)

        return doc

    @staticmethod
    def generate_components_batch(num_components=100):
        """批量生成构件"""
        components = []

        # 30% 梁
        for i in range(int(num_components * 0.3)):
            components.append(TestDataGenerator.generate_component(
                f"beam_{i}",
                ComponentType.BEAM,
                {'width': 300, 'height': 600, 'length': 6000}
            ))

        # 20% 柱
        for i in range(int(num_components * 0.2)):
            components.append(TestDataGenerator.generate_component(
                f"column_{i}",
                ComponentType.COLUMN,
                {'width': 600, 'height': 600, 'length': 3000}
            ))

        # 30% 墙
        for i in range(int(num_components * 0.3)):
            components.append(TestDataGenerator.generate_component(
                f"wall_{i}",
                ComponentType.WALL,
                {'width': 200, 'height': 3000, 'length': 6000}
            ))

        # 20% 板
        for i in range(int(num_components * 0.2)):
            components.append(TestDataGenerator.generate_component(
                f"slab_{i}",
                ComponentType.SLAB,
                {'width': 3000, 'height': 120, 'length': 6000}
            ))

        return components


class TestLargeScalePerformance:
    """大规模性能测试"""

    def setup_method(self):
        """测试前准备"""
        self.recognizer = ComponentRecognizer()
        self.validator = ResultValidator()
        self.calculator = QuantityCalculator()
        self.generator = TestDataGenerator()

    # ============ 小规模项目（10-50个构件） ============

    def test_small_project_10_components(self):
        """测试小规模项目（10个构件）"""
        components = self.generator.generate_components_batch(10)

        start = time.time()

        # 验证
        validation_result = self.validator.validate(components)

        # 计算
        calc_result = self.calculator.calculate(components)

        elapsed = time.time() - start

        print(f"\n【小规模-10构件】")
        print(f"  耗时: {elapsed*1000:.1f}ms")
        print(f"  验证: {validation_result.passed}/{validation_result.total_components} 通过")
        print(f"  计算: {len(calc_result)} 种构件类型")

        # 10个构件应在100ms内完成
        assert elapsed < 0.1, f"性能不达标: {elapsed*1000:.1f}ms"

    def test_small_project_50_components(self):
        """测试小规模项目（50个构件）"""
        components = self.generator.generate_components_batch(50)

        start = time.time()
        validation_result = self.validator.validate(components)
        calc_result = self.calculator.calculate(components)
        elapsed = time.time() - start

        print(f"\n【小规模-50构件】")
        print(f"  耗时: {elapsed*1000:.1f}ms")
        print(f"  验证: {validation_result.passed}/{validation_result.total_components} 通过")

        # 50个构件应在500ms内完成
        assert elapsed < 0.5, f"性能不达标: {elapsed*1000:.1f}ms"

    # ============ 中规模项目（100-500个构件） ============

    def test_medium_project_100_components(self):
        """测试中规模项目（100个构件）- 典型住宅楼"""
        components = self.generator.generate_components_batch(100)

        start = time.time()
        validation_result = self.validator.validate(components)
        calc_result = self.calculator.calculate(components)
        elapsed = time.time() - start

        print(f"\n【中规模-100构件（住宅楼）】")
        print(f"  耗时: {elapsed:.3f}s")
        print(f"  验证: {validation_result.passed}/{validation_result.total_components} 通过")
        print(f"  平均: {elapsed/100*1000:.1f}ms/构件")

        # 100个构件应在1秒内完成
        assert elapsed < 1.0, f"性能不达标: {elapsed:.3f}s"

    def test_medium_project_500_components(self):
        """测试中规模项目（500个构件）- 多层办公楼"""
        components = self.generator.generate_components_batch(500)

        start = time.time()
        validation_result = self.validator.validate(components)
        calc_result = self.calculator.calculate(components)
        elapsed = time.time() - start

        print(f"\n【中规模-500构件（办公楼）】")
        print(f"  耗时: {elapsed:.3f}s")
        print(f"  验证: {validation_result.passed}/{validation_result.total_components} 通过")
        print(f"  平均: {elapsed/500*1000:.1f}ms/构件")

        # 500个构件应在5秒内完成
        assert elapsed < 5.0, f"性能不达标: {elapsed:.3f}s"

    # ============ 大规模项目（1000+个构件） ============

    def test_large_project_1000_components(self):
        """测试大规模项目（1000个构件）- 高层建筑"""
        components = self.generator.generate_components_batch(1000)

        start = time.time()
        validation_result = self.validator.validate(components)
        calc_result = self.calculator.calculate(components)
        elapsed = time.time() - start

        print(f"\n【大规模-1000构件（高层建筑）】")
        print(f"  耗时: {elapsed:.3f}s")
        print(f"  验证: {validation_result.passed}/{validation_result.total_components} 通过")
        print(f"  平均: {elapsed/1000*1000:.1f}ms/构件")

        # 1000个构件应在10秒内完成
        assert elapsed < 10.0, f"性能不达标: {elapsed:.3f}s"

    def test_large_project_5000_components(self):
        """测试大规模项目（5000个构件）- 大型综合体"""
        components = self.generator.generate_components_batch(5000)

        start = time.time()
        validation_result = self.validator.validate(components)
        calc_result = self.calculator.calculate(components)
        elapsed = time.time() - start

        print(f"\n【大规模-5000构件（大型综合体）】")
        print(f"  耗时: {elapsed:.3f}s")
        print(f"  验证: {validation_result.passed}/{validation_result.total_components} 通过")
        print(f"  平均: {elapsed/5000*1000:.1f}ms/构件")

        # 5000个构件应在50秒内完成
        assert elapsed < 50.0, f"性能不达标: {elapsed:.3f}s"

    # ============ 识别性能测试 ============

    def test_recognition_performance_100_texts(self):
        """测试识别100个文本标注的性能"""
        doc = self.generator.generate_dwg_document(100)

        start = time.time()
        components = self.recognizer.recognize_components(doc)
        elapsed = time.time() - start

        print(f"\n【识别性能-100文本】")
        print(f"  耗时: {elapsed:.3f}s")
        print(f"  识别: {len(components)} 个构件")
        print(f"  平均: {elapsed/100*1000:.1f}ms/文本")

        # 100个文本应在2秒内识别完成
        assert elapsed < 2.0, f"识别性能不达标: {elapsed:.3f}s"

    def test_recognition_performance_500_texts(self):
        """测试识别500个文本标注的性能"""
        doc = self.generator.generate_dwg_document(500)

        start = time.time()
        components = self.recognizer.recognize_components(doc)
        elapsed = time.time() - start

        print(f"\n【识别性能-500文本】")
        print(f"  耗时: {elapsed:.3f}s")
        print(f"  识别: {len(components)} 个构件")
        print(f"  平均: {elapsed/500*1000:.1f}ms/文本")

        # 500个文本应在10秒内识别完成
        assert elapsed < 10.0, f"识别性能不达标: {elapsed:.3f}s"

    # ============ 完整流程性能测试 ============

    def test_end_to_end_workflow_medium_project(self):
        """测试完整流程（识别+验证+计算）- 中规模项目"""
        # 生成包含200个文本标注的DWG文档
        doc = self.generator.generate_dwg_document(200)

        # 识别
        start_recognition = time.time()
        components = self.recognizer.recognize_components(doc)
        time_recognition = time.time() - start_recognition

        # 验证
        start_validation = time.time()
        validation_result = self.validator.validate(components)
        time_validation = time.time() - start_validation

        # 计算
        start_calculation = time.time()
        calc_result = self.calculator.calculate(components)
        time_calculation = time.time() - start_calculation

        total_time = time_recognition + time_validation + time_calculation

        print(f"\n【完整流程-中规模项目】")
        print(f"  识别: {time_recognition:.3f}s")
        print(f"  验证: {time_validation:.3f}s")
        print(f"  计算: {time_calculation:.3f}s")
        print(f"  总计: {total_time:.3f}s")
        print(f"  识别构件: {len(components)} 个")
        print(f"  验证通过: {validation_result.passed}/{validation_result.total_components}")

        # 整体应在15秒内完成
        assert total_time < 15.0, f"完整流程性能不达标: {total_time:.3f}s"

    # ============ 内存使用测试 ============

    def test_memory_usage_large_project(self):
        """测试大规模项目内存使用"""
        import psutil
        import os

        process = psutil.Process(os.getpid())
        mem_before = process.memory_info().rss / 1024 / 1024  # MB

        # 处理1000个构件
        components = self.generator.generate_components_batch(1000)
        validation_result = self.validator.validate(components)
        calc_result = self.calculator.calculate(components)

        mem_after = process.memory_info().rss / 1024 / 1024  # MB
        mem_increase = mem_after - mem_before

        print(f"\n【内存使用-1000构件】")
        print(f"  处理前: {mem_before:.1f}MB")
        print(f"  处理后: {mem_after:.1f}MB")
        print(f"  增长: {mem_increase:.1f}MB")

        # 内存增长应小于100MB
        assert mem_increase < 100, f"内存使用过多: {mem_increase:.1f}MB"

    # ============ 并发处理测试 ============

    def test_concurrent_processing_simulation(self):
        """模拟并发处理多个项目"""
        import concurrent.futures

        def process_project(project_id, num_components):
            """处理单个项目"""
            components = self.generator.generate_components_batch(num_components)
            validation_result = self.validator.validate(components)
            calc_result = self.calculator.calculate(components)
            return project_id, len(components), validation_result.passed

        # 模拟5个项目并发处理
        with concurrent.futures.ThreadPoolExecutor(max_workers=5) as executor:
            futures = []
            for i in range(5):
                future = executor.submit(process_project, f"project_{i}", 100)
                futures.append(future)

            start = time.time()
            results = [future.result() for future in concurrent.futures.as_completed(futures)]
            elapsed = time.time() - start

        print(f"\n【并发处理-5个项目】")
        print(f"  耗时: {elapsed:.3f}s")
        for project_id, num_comps, passed in results:
            print(f"  {project_id}: {num_comps}构件, {passed}通过")

        # 并发处理应在10秒内完成
        assert elapsed < 10.0, f"并发处理性能不达标: {elapsed:.3f}s"


class TestAccuracyStatistics:
    """准确率统计测试"""

    def setup_method(self):
        self.recognizer = ComponentRecognizer()
        self.validator = ResultValidator()
        self.generator = TestDataGenerator()

    def test_extraction_accuracy_statistics(self):
        """测试尺寸提取准确率统计"""
        test_cases = [
            "300×600", "KL1 250×500", "KZ1 600×600", "φ500",
            "墙 200厚", "楼板120厚", "b×h=400×800", "3m×600mm",
            "300, 600", "300(600)", "L1 250×500 L=7200",
        ]

        success = 0
        for text in test_cases:
            dims = self.recognizer._extract_dimensions(text)
            if dims and 'width' in dims:
                success += 1

        accuracy = success / len(test_cases) * 100
        print(f"\n【尺寸提取准确率】: {success}/{len(test_cases)} = {accuracy:.1f}%")

        # 目标：90%+准确率
        assert accuracy >= 90, f"提取准确率不达标: {accuracy:.1f}%"

    def test_validation_accuracy_statistics(self):
        """测试验证准确率统计"""
        # 生成测试数据：50个正常 + 50个异常
        components = []

        # 50个正常构件
        components.extend(self.generator.generate_components_batch(50))

        # 50个异常构件
        for i in range(50):
            if i % 3 == 0:
                # 体积为0
                components.append(Component(
                    id=f"bad_{i}",
                    type=ComponentType.BEAM,
                    name=f"异常梁{i}",
                    entities=[],
                    properties={},
                    dimensions={'width': 300, 'height': 600}  # 缺失length
                ))
            elif i % 3 == 1:
                # 尺寸超出范围
                components.append(Component(
                    id=f"bad_{i}",
                    type=ComponentType.BEAM,
                    name=f"异常梁{i}",
                    entities=[],
                    properties={},
                    dimensions={'width': 50, 'height': 600, 'length': 6000}
                ))
            else:
                # 宽高比异常
                components.append(Component(
                    id=f"bad_{i}",
                    type=ComponentType.BEAM,
                    name=f"异常梁{i}",
                    entities=[],
                    properties={},
                    dimensions={'width': 200, 'height': 1200, 'length': 6000}
                ))

        result = self.validator.validate(components)

        # 计算捕获率
        detected_issues = result.errors + result.warnings
        expected_issues = 50  # 应该检测到50个异常

        capture_rate = min(detected_issues / expected_issues * 100, 100)
        false_positive_rate = max((result.total_components - result.passed - expected_issues) / 50 * 100, 0)

        print(f"\n【验证系统统计】")
        print(f"  总构件: {result.total_components}")
        print(f"  通过: {result.passed}")
        print(f"  警告: {result.warnings}")
        print(f"  错误: {result.errors}")
        print(f"  检测到问题: {detected_issues}")
        print(f"  实际问题: {expected_issues}")
        print(f"  捕获率: {capture_rate:.1f}%")
        print(f"  误报率: {false_positive_rate:.1f}%")

        # 目标：95%+捕获率，<10%误报率
        assert capture_rate >= 90, f"捕获率不达标: {capture_rate:.1f}%"
        assert false_positive_rate < 15, f"误报率过高: {false_positive_rate:.1f}%"


if __name__ == "__main__":
    pytest.main([__file__, "-v", "-s"])
