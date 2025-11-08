"""
尺寸提取功能单元测试

测试10+种CAD标注格式的提取准确性
"""
import pytest
import sys
from pathlib import Path

# 添加项目根目录到Python路径
project_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(project_root))

from src.calculation.component_recognizer import ComponentRecognizer, ComponentType


class TestDimensionExtraction:
    """测试尺寸提取功能"""

    def setup_method(self):
        """测试前准备"""
        self.recognizer = ComponentRecognizer()

    # ============ 格式1: 乘号/星号/x ============

    def test_multiply_sign_format(self):
        """测试乘号格式：300×600"""
        dims = self.recognizer._extract_dimensions("300×600")
        assert dims['width'] == 300
        assert dims['height'] == 600
        assert 'length' not in dims

    def test_asterisk_format(self):
        """测试星号格式：300*600"""
        dims = self.recognizer._extract_dimensions("300*600")
        assert dims['width'] == 300
        assert dims['height'] == 600

    def test_lowercase_x_format(self):
        """测试小写x格式：300x600"""
        dims = self.recognizer._extract_dimensions("300x600")
        assert dims['width'] == 300
        assert dims['height'] == 600

    def test_uppercase_x_format(self):
        """测试大写X格式：300X600"""
        dims = self.recognizer._extract_dimensions("300X600")
        assert dims['width'] == 300
        assert dims['height'] == 600

    # ============ 格式2: 三维尺寸 ============

    def test_three_dimensional(self):
        """测试三维尺寸：300×600×900"""
        dims = self.recognizer._extract_dimensions("300×600×900")
        assert dims['width'] == 300
        assert dims['height'] == 600
        assert dims['length'] == 900

    def test_three_dimensional_mixed(self):
        """测试三维尺寸混合格式：300x600*900"""
        dims = self.recognizer._extract_dimensions("300x600*900")
        assert dims['width'] == 300
        assert dims['height'] == 600
        assert dims['length'] == 900

    # ============ 格式3: 直径标注 ============

    def test_diameter_phi_lowercase(self):
        """测试小写φ直径：φ300"""
        dims = self.recognizer._extract_dimensions("φ300")
        assert dims['diameter'] == 300
        assert dims['width'] == 300
        assert dims['height'] == 300

    def test_diameter_phi_uppercase(self):
        """测试大写Φ直径：Φ300"""
        dims = self.recognizer._extract_dimensions("Φ300")
        assert dims['diameter'] == 300
        assert dims['width'] == 300

    def test_diameter_o_slash(self):
        """测试ø直径：ø500"""
        dims = self.recognizer._extract_dimensions("ø500")
        assert dims['diameter'] == 500

    def test_diameter_with_space(self):
        """测试带空格的直径：φ 450"""
        dims = self.recognizer._extract_dimensions("φ 450")
        assert dims['diameter'] == 450

    # ============ 格式4: 逗号分隔 ============

    def test_comma_separated_two(self):
        """测试逗号分隔2个尺寸：300, 600"""
        dims = self.recognizer._extract_dimensions("300, 600")
        assert dims['width'] == 300
        assert dims['height'] == 600

    def test_comma_separated_three(self):
        """测试逗号分隔3个尺寸：300, 600, 900"""
        dims = self.recognizer._extract_dimensions("300, 600, 900")
        assert dims['width'] == 300
        assert dims['height'] == 600
        assert dims['length'] == 900

    def test_chinese_comma(self):
        """测试中文逗号：300，600"""
        dims = self.recognizer._extract_dimensions("300，600")
        assert dims['width'] == 300
        assert dims['height'] == 600

    # ============ 格式5: 带标签 ============

    def test_labeled_lowercase(self):
        """测试小写标签：b×h=300×600"""
        dims = self.recognizer._extract_dimensions("b×h=300×600")
        assert dims['width'] == 300
        assert dims['height'] == 600

    def test_labeled_uppercase(self):
        """测试大写标签：B×H=400×800"""
        dims = self.recognizer._extract_dimensions("B×H=400×800")
        assert dims['width'] == 400
        assert dims['height'] == 800

    def test_labeled_length(self):
        """测试长度标签：L×B=5000×300"""
        dims = self.recognizer._extract_dimensions("L×B=5000×300")
        assert dims['width'] == 5000
        assert dims['height'] == 300

    # ============ 格式6: 斜杠分隔 ============

    def test_slash_format(self):
        """测试斜杠格式：300/600"""
        dims = self.recognizer._extract_dimensions("300/600")
        assert dims['width'] == 300
        assert dims['height'] == 600

    # ============ 格式7: 短横线分隔 ============

    def test_dash_format(self):
        """测试短横线格式：300-600（非范围）"""
        dims = self.recognizer._extract_dimensions("300-600")
        assert dims['width'] == 300
        assert dims['height'] == 600

    def test_dash_three_dimensions(self):
        """测试短横线三维：300-600-900"""
        dims = self.recognizer._extract_dimensions("300-600-900")
        assert dims['width'] == 300
        assert dims['height'] == 600
        assert dims['length'] == 900

    def test_dash_range_ignored(self):
        """测试范围格式应被忽略：2-5层"""
        dims = self.recognizer._extract_dimensions("2-5层")
        # 应该返回空或只提取单个数值
        assert len(dims) <= 1  # 不应提取为width-height

    # ============ 格式8: 括号标注 ============

    def test_parenthesis_format(self):
        """测试括号格式：300(600)"""
        dims = self.recognizer._extract_dimensions("300(600)")
        assert dims['width'] == 300
        assert dims['height'] == 600

    def test_parenthesis_with_space(self):
        """测试带空格括号：400 ( 800 )"""
        dims = self.recognizer._extract_dimensions("400 ( 800 )")
        assert dims['width'] == 400
        assert dims['height'] == 800

    # ============ 格式9: 带单位 ============

    def test_millimeter_unit(self):
        """测试毫米单位：3000mm"""
        dims = self.recognizer._extract_dimensions("3000mm")
        assert dims['width'] == 3000

    def test_meter_unit(self):
        """测试米单位：3m -> 3000mm"""
        dims = self.recognizer._extract_dimensions("3m")
        assert dims['width'] == 3000

    def test_centimeter_unit(self):
        """测试厘米单位：300cm -> 3000mm"""
        dims = self.recognizer._extract_dimensions("300cm")
        assert dims['width'] == 3000

    def test_inch_unit(self):
        """测试英寸单位：12" -> 304.8mm"""
        dims = self.recognizer._extract_dimensions('12"')
        assert abs(dims['width'] - 304.8) < 0.1

    def test_feet_unit(self):
        """测试英尺单位：10' -> 3048mm"""
        dims = self.recognizer._extract_dimensions("10'")
        assert abs(dims['width'] - 3048) < 0.1

    def test_mixed_units(self):
        """测试混合单位：3m × 600mm"""
        dims = self.recognizer._extract_dimensions("3m × 600mm")
        assert dims['width'] == 3000
        assert dims['height'] == 600

    # ============ 格式10: 小数支持 ============

    def test_decimal_dimensions(self):
        """测试小数尺寸：300.5×600.8"""
        dims = self.recognizer._extract_dimensions("300.5×600.8")
        assert abs(dims['width'] - 300.5) < 0.01
        assert abs(dims['height'] - 600.8) < 0.01

    def test_decimal_with_unit(self):
        """测试小数+单位：3.5m -> 3500mm"""
        dims = self.recognizer._extract_dimensions("3.5m")
        assert dims['width'] == 3500

    # ============ 复杂实际场景 ============

    def test_beam_notation(self):
        """测试梁标注：KL1 300×600"""
        dims = self.recognizer._extract_dimensions("KL1 300×600")
        assert dims['width'] == 300
        assert dims['height'] == 600

    def test_column_notation(self):
        """测试柱标注：KZ1 600×600"""
        dims = self.recognizer._extract_dimensions("KZ1 600×600")
        assert dims['width'] == 600
        assert dims['height'] == 600

    def test_wall_thickness(self):
        """测试墙厚标注：剪力墙 200厚"""
        dims = self.recognizer._extract_dimensions("剪力墙 200厚")
        assert dims['width'] == 200

    def test_slab_thickness(self):
        """测试板厚标注：楼板120厚"""
        dims = self.recognizer._extract_dimensions("楼板120厚")
        assert dims['width'] == 120

    def test_beam_with_span(self):
        """测试带跨度的梁：L1 250×500 L=7200"""
        text = "L1 250×500 L=7200"
        dims = self.recognizer._extract_dimensions(text)
        # 应该提取到250×500
        assert dims['width'] == 250
        assert dims['height'] == 500

    def test_complex_notation(self):
        """测试复杂标注：C1(300×600) L=6000"""
        dims = self.recognizer._extract_dimensions("C1(300×600) L=6000")
        # 应该能提取300×600
        assert dims['width'] == 300
        assert dims['height'] == 600

    # ============ 边界情况 ============

    def test_empty_string(self):
        """测试空字符串"""
        dims = self.recognizer._extract_dimensions("")
        assert len(dims) == 0

    def test_no_numbers(self):
        """测试无数字文本"""
        dims = self.recognizer._extract_dimensions("框架梁")
        assert len(dims) == 0

    def test_single_number(self):
        """测试单个数字：500"""
        dims = self.recognizer._extract_dimensions("500")
        assert dims['width'] == 500

    def test_very_small_dimension(self):
        """测试非常小的尺寸：10mm"""
        dims = self.recognizer._extract_dimensions("10mm")
        assert dims['width'] == 10

    def test_very_large_dimension(self):
        """测试非常大的尺寸：12000mm"""
        dims = self.recognizer._extract_dimensions("12000mm")
        assert dims['width'] == 12000

    # ============ 性能测试 ============

    def test_extraction_performance(self):
        """测试提取性能（1000次）"""
        import time
        test_cases = [
            "300×600",
            "φ500",
            "b×h=400×800",
            "3m×600mm",
            "KL1 250×500 L=6000"
        ]

        start = time.time()
        for _ in range(1000):
            for text in test_cases:
                self.recognizer._extract_dimensions(text)
        elapsed = time.time() - start

        # 1000*5=5000次提取应在1秒内完成
        assert elapsed < 1.0, f"提取性能不达标: {elapsed:.3f}秒"


class TestDimensionExtractionStatistics:
    """测试尺寸提取统计"""

    def setup_method(self):
        """测试前准备"""
        self.recognizer = ComponentRecognizer()

    def test_format_coverage(self):
        """测试格式覆盖率"""
        # 定义所有支持的格式
        test_cases = [
            ("300×600", "乘号"),
            ("300*600", "星号"),
            ("300x600", "小写x"),
            ("300X600", "大写X"),
            ("300×600×900", "三维"),
            ("φ300", "直径φ"),
            ("Φ300", "直径Φ"),
            ("ø300", "直径ø"),
            ("300, 600", "逗号2维"),
            ("300, 600, 900", "逗号3维"),
            ("b×h=300×600", "带标签"),
            ("300/600", "斜杠"),
            ("300-600", "短横线"),
            ("300(600)", "括号"),
            ("3000mm", "毫米"),
            ("3m", "米"),
            ("300cm", "厘米"),
        ]

        success = 0
        total = len(test_cases)

        for text, format_name in test_cases:
            dims = self.recognizer._extract_dimensions(text)
            if dims and 'width' in dims:
                success += 1
                print(f"✅ {format_name:12s}: {text:20s} -> {dims}")
            else:
                print(f"❌ {format_name:12s}: {text:20s} -> 提取失败")

        success_rate = success / total * 100
        print(f"\n格式覆盖率: {success}/{total} = {success_rate:.1f}%")

        # 至少90%成功率
        assert success_rate >= 90, f"格式覆盖率不足: {success_rate:.1f}%"


if __name__ == "__main__":
    # 运行测试
    pytest.main([__file__, "-v", "-s"])
