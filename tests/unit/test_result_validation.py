"""
结果验证系统单元测试

测试5维度验证体系的准确性
"""
import pytest
import sys
from pathlib import Path

project_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(project_root))

from src.calculation.component_recognizer import Component, ComponentType
from src.calculation.result_validator import ResultValidator, ValidationLevel


class TestResultValidation:
    """测试结果验证系统"""

    def setup_method(self):
        """测试前准备"""
        self.validator = ResultValidator()

    # ============ 维度1: 尺寸完整性验证 ============

    def test_complete_dimensions_pass(self):
        """测试完整尺寸通过验证"""
        component = Component(
            id="test1",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 300, 'height': 600, 'length': 6000}
        )

        result = self.validator.validate([component])
        assert result.passed == 1
        assert result.errors == 0
        assert result.warnings == 0

    def test_missing_dimensions_warning(self):
        """测试缺失尺寸警告"""
        component = Component(
            id="test2",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 300, 'height': 600}  # 缺失length
        )

        result = self.validator.validate([component])
        assert result.warnings >= 1

    def test_empty_dimensions_error(self):
        """测试空尺寸错误"""
        component = Component(
            id="test3",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={}
        )

        result = self.validator.validate([component])
        assert result.errors >= 1

    # ============ 维度2: 尺寸范围验证（基于GB规范） ============

    def test_beam_width_in_range(self):
        """测试梁宽在规范范围内"""
        component = Component(
            id="test4",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 300, 'height': 600, 'length': 6000}
        )

        result = self.validator.validate([component])
        # 300mm在200-1000mm范围内
        assert result.passed == 1

    def test_beam_width_out_of_range_warning(self):
        """测试梁宽超出范围警告"""
        component = Component(
            id="test5",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 1200, 'height': 600, 'length': 6000}  # 超出1000mm
        )

        result = self.validator.validate([component])
        assert result.warnings >= 1 or result.errors >= 1

    def test_beam_width_severely_out_of_range_error(self):
        """测试梁宽严重超出范围错误"""
        component = Component(
            id="test6",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 50, 'height': 600, 'length': 6000}  # 远小于200mm
        )

        result = self.validator.validate([component])
        # 50mm远小于规范最小值200mm
        assert result.errors >= 1 or result.warnings >= 1

    def test_column_dimensions_in_range(self):
        """测试柱尺寸在规范范围内"""
        component = Component(
            id="test7",
            type=ComponentType.COLUMN,
            name="KZ1",
            entities=[],
            properties={},
            dimensions={'width': 600, 'height': 600, 'length': 3000}
        )

        result = self.validator.validate([component])
        assert result.passed == 1

    def test_circular_column_diameter_range(self):
        """测试圆柱直径范围"""
        component = Component(
            id="test8",
            type=ComponentType.COLUMN,
            name="圆柱",
            entities=[],
            properties={},
            dimensions={'diameter': 500, 'width': 500, 'height': 500, 'length': 3000}
        )

        result = self.validator.validate([component])
        assert result.passed == 1

    def test_wall_thickness_range(self):
        """测试墙厚度范围"""
        # 合理厚度
        component1 = Component(
            id="test9a",
            type=ComponentType.WALL,
            name="墙",
            entities=[],
            properties={},
            dimensions={'width': 200, 'height': 3000, 'length': 6000}
        )
        result1 = self.validator.validate([component1])
        assert result1.passed == 1

        # 过薄（<50mm）
        component2 = Component(
            id="test9b",
            type=ComponentType.WALL,
            name="墙",
            entities=[],
            properties={},
            dimensions={'width': 30, 'height': 3000, 'length': 6000}
        )
        result2 = self.validator.validate([component2])
        assert result2.warnings >= 1 or result2.errors >= 1

    def test_slab_thickness_range(self):
        """测试板厚度范围"""
        component = Component(
            id="test10",
            type=ComponentType.SLAB,
            name="楼板",
            entities=[],
            properties={},
            dimensions={'width': 3000, 'height': 120, 'length': 6000}
        )

        result = self.validator.validate([component])
        # 120mm在60-300mm范围内
        assert result.passed == 1

    # ============ 维度3: 尺寸比例验证 ============

    def test_beam_width_height_ratio_normal(self):
        """测试梁宽高比正常"""
        component = Component(
            id="test11",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 300, 'height': 600, 'length': 6000}  # 比例0.5
        )

        result = self.validator.validate([component])
        # 0.5在0.3-1.0范围内
        assert result.passed == 1

    def test_beam_width_height_ratio_abnormal(self):
        """测试梁宽高比异常"""
        component = Component(
            id="test12",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 200, 'height': 1000, 'length': 6000}  # 比例0.2
        )

        result = self.validator.validate([component])
        # 0.2 < 0.3，应有警告
        assert result.warnings >= 1

    def test_column_aspect_ratio_suspicious(self):
        """测试柱长宽比可疑（可能是墙）"""
        component = Component(
            id="test13",
            type=ComponentType.COLUMN,
            name="KZ1",
            entities=[],
            properties={},
            dimensions={'width': 300, 'height': 1500, 'length': 3000}  # 长宽比5
        )

        result = self.validator.validate([component])
        # 长宽比>3，可能是墙而非柱
        assert result.warnings >= 1

    def test_slab_thickness_span_ratio_normal(self):
        """测试板厚跨比正常"""
        component = Component(
            id="test14",
            type=ComponentType.SLAB,
            name="楼板",
            entities=[],
            properties={},
            dimensions={'width': 3000, 'height': 100, 'length': 3000}  # 厚跨比1/30
        )

        result = self.validator.validate([component])
        assert result.passed == 1

    def test_slab_thickness_span_ratio_abnormal(self):
        """测试板厚跨比异常"""
        component = Component(
            id="test15",
            type=ComponentType.SLAB,
            name="楼板",
            entities=[],
            properties={},
            dimensions={'width': 3000, 'height': 50, 'length': 3000}  # 厚跨比1/60
        )

        result = self.validator.validate([component])
        # 1/60 > 1/50，应有警告
        assert result.warnings >= 1

    # ============ 维度4: 尺寸模数验证 ============

    def test_standard_modular_dimension(self):
        """测试标准模数尺寸（50mm倍数）"""
        component = Component(
            id="test16",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 300, 'height': 600, 'length': 6000}  # 都是50mm倍数
        )

        result = self.validator.validate([component])
        assert result.passed == 1

    def test_common_beam_dimensions(self):
        """测试常见梁尺寸"""
        # 常见梁截面
        common_beams = [
            (200, 300), (250, 400), (300, 600), (350, 700), (400, 800)
        ]

        for width, height in common_beams:
            component = Component(
                id=f"test_beam_{width}x{height}",
                type=ComponentType.BEAM,
                name=f"KL {width}×{height}",
                entities=[],
                properties={},
                dimensions={'width': width, 'height': height, 'length': 6000}
            )
            result = self.validator.validate([component])
            assert result.passed == 1, f"常见尺寸 {width}×{height} 应通过验证"

    def test_common_column_dimensions(self):
        """测试常见柱尺寸"""
        common_columns = [300, 400, 500, 600, 700, 800]

        for size in common_columns:
            component = Component(
                id=f"test_col_{size}",
                type=ComponentType.COLUMN,
                name=f"KZ{size}",
                entities=[],
                properties={},
                dimensions={'width': size, 'height': size, 'length': 3000}
            )
            result = self.validator.validate([component])
            assert result.passed == 1, f"常见柱尺寸 {size}×{size} 应通过验证"

    def test_non_standard_dimension_warning(self):
        """测试非标准尺寸警告"""
        component = Component(
            id="test17",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 275, 'height': 625, 'length': 6000}  # 非标准值
        )

        result = self.validator.validate([component])
        # 275和625不是常见值，应有警告
        assert result.warnings >= 1

    # ============ 维度5: 体积/面积验证 ============

    def test_volume_zero_error(self):
        """测试体积为0错误"""
        component = Component(
            id="test18",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 300, 'height': 600}  # 缺失length，体积为0
        )

        result = self.validator.validate([component])
        # 体积为0应该是ERROR
        assert result.errors >= 1

    def test_volume_abnormally_large_warning(self):
        """测试体积异常大警告"""
        component = Component(
            id="test19",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 3000, 'height': 6000, 'length': 60000}  # 体积>1000m³
        )

        result = self.validator.validate([component])
        # 体积过大，应有警告（可能单位错误）
        assert result.warnings >= 1

    def test_normal_volume(self):
        """测试正常体积"""
        component = Component(
            id="test20",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions={'width': 300, 'height': 600, 'length': 6000}  # 1.08m³
        )

        result = self.validator.validate([component])
        assert result.passed == 1

    # ============ 综合场景测试 ============

    def test_multiple_components_mixed_results(self):
        """测试多个构件混合结果"""
        components = [
            # 正常的梁
            Component(id="good_beam", type=ComponentType.BEAM, name="KL1", entities=[], properties={},
                     dimensions={'width': 300, 'height': 600, 'length': 6000}),
            # 宽高比异常的梁
            Component(id="bad_ratio_beam", type=ComponentType.BEAM, name="KL2", entities=[], properties={},
                     dimensions={'width': 200, 'height': 1000, 'length': 6000}),
            # 体积为0的柱
            Component(id="zero_volume_column", type=ComponentType.COLUMN, name="KZ1", entities=[], properties={},
                     dimensions={'width': 600, 'height': 600}),
            # 正常的墙
            Component(id="good_wall", type=ComponentType.WALL, name="墙1", entities=[], properties={},
                     dimensions={'width': 200, 'height': 3000, 'length': 6000}),
        ]

        result = self.validator.validate(components)

        assert result.total_components == 4
        assert result.passed >= 2  # 至少2个通过
        assert result.warnings >= 1  # 至少1个警告
        assert result.errors >= 1  # 至少1个错误

    def test_validation_report_generation(self):
        """测试验证报告生成"""
        components = [
            Component(id="test1", type=ComponentType.BEAM, name="KL1", entities=[], properties={},
                     dimensions={'width': 300, 'height': 600, 'length': 6000}),
            Component(id="test2", type=ComponentType.BEAM, name="KL2", entities=[], properties={},
                     dimensions={'width': 200, 'height': 1000, 'length': 6000}),
        ]

        result = self.validator.validate(components)
        report = self.validator.generate_report(result)

        # 报告应包含关键信息
        assert "验证报告" in report
        assert "通过" in report or "警告" in report or "错误" in report
        assert len(report) > 100  # 报告应该有实质内容

    def test_validation_result_to_dict(self):
        """测试验证结果转字典"""
        components = [
            Component(id="test", type=ComponentType.BEAM, name="KL1", entities=[], properties={},
                     dimensions={'width': 300, 'height': 600, 'length': 6000}),
        ]

        result = self.validator.validate(components)
        result_dict = result.to_dict()

        assert 'total' in result_dict
        assert 'passed' in result_dict
        assert 'warnings' in result_dict
        assert 'errors' in result_dict
        assert 'pass_rate' in result_dict
        assert 'issues' in result_dict

    # ============ 性能测试 ============

    def test_validation_performance_100_components(self):
        """测试验证100个构件的性能"""
        import time

        components = []
        for i in range(100):
            components.append(Component(
                id=f"comp_{i}",
                type=ComponentType.BEAM,
                name=f"KL{i}",
                entities=[],
                properties={},
                dimensions={'width': 300, 'height': 600, 'length': 6000}
            ))

        start = time.time()
        result = self.validator.validate(components)
        elapsed = time.time() - start

        # 100个构件验证应在0.5秒内完成
        assert elapsed < 0.5, f"验证性能不达标: {elapsed:.3f}秒"
        assert result.total_components == 100

    # ============ 边界情况 ============

    def test_empty_component_list(self):
        """测试空构件列表"""
        result = self.validator.validate([])
        assert result.total_components == 0
        assert result.passed == 0

    def test_component_without_dimensions(self):
        """测试无尺寸构件"""
        component = Component(
            id="test",
            type=ComponentType.BEAM,
            name="KL1",
            entities=[],
            properties={},
            dimensions=None
        )

        result = self.validator.validate([component])
        assert result.errors >= 1


class TestValidationStatistics:
    """测试验证统计功能"""

    def setup_method(self):
        self.validator = ResultValidator()

    def test_pass_rate_calculation(self):
        """测试通过率计算"""
        components = [
            # 3个正常构件
            Component(id="1", type=ComponentType.BEAM, name="KL1", entities=[], properties={},
                     dimensions={'width': 300, 'height': 600, 'length': 6000}),
            Component(id="2", type=ComponentType.COLUMN, name="KZ1", entities=[], properties={},
                     dimensions={'width': 600, 'height': 600, 'length': 3000}),
            Component(id="3", type=ComponentType.WALL, name="墙1", entities=[], properties={},
                     dimensions={'width': 200, 'height': 3000, 'length': 6000}),
            # 1个异常构件
            Component(id="4", type=ComponentType.BEAM, name="KL2", entities=[], properties={},
                     dimensions={'width': 200, 'height': 1200, 'length': 6000}),
        ]

        result = self.validator.validate(components)

        # 计算通过率
        pass_rate = result.passed / result.total_components * 100
        print(f"通过率: {pass_rate:.1f}%")

        assert result.total_components == 4
        assert pass_rate >= 50  # 至少50%通过率

    def test_error_capture_rate(self):
        """测试错误捕获率"""
        # 创建10个构件，其中5个有明显错误
        components = []

        # 5个正常构件
        for i in range(5):
            components.append(Component(
                id=f"good_{i}",
                type=ComponentType.BEAM,
                name=f"KL{i}",
                entities=[],
                properties={},
                dimensions={'width': 300, 'height': 600, 'length': 6000}
            ))

        # 5个有错误的构件（体积为0）
        for i in range(5):
            components.append(Component(
                id=f"bad_{i}",
                type=ComponentType.BEAM,
                name=f"KL{i+10}",
                entities=[],
                properties={},
                dimensions={'width': 300, 'height': 600}  # 缺失length
            ))

        result = self.validator.validate(components)

        # 错误捕获率 = 捕获的错误数 / 实际错误数
        # 至少应捕获4个（80%捕获率）
        assert result.errors + result.warnings >= 4, f"错误捕获不足: {result.errors + result.warnings}/5"


if __name__ == "__main__":
    pytest.main([__file__, "-v", "-s"])
