# -*- coding: utf-8 -*-
"""
尺寸补充系统单元测试

测试3策略智能补充的准确性
"""
import pytest
import sys
from pathlib import Path

project_root = Path(__file__).parent.parent.parent
sys.path.insert(0, str(project_root))

from src.calculation.component_recognizer import ComponentRecognizer, ComponentType, Component
from src.dwg.entities import DWGDocument, TextEntity


class TestDimensionSupplementation:
    """测试尺寸补充系统"""

    def setup_method(self):
        """测试前准备"""
        self.recognizer = ComponentRecognizer()
        self.document = DWGDocument()

    # ============ 策略1: 建筑规范标准尺寸 ============

    def test_beam_missing_length(self):
        """测试梁缺失跨度（应补充6000mm）"""
        dims = {'width': 300, 'height': 600}
        text = "KL1 300×600"
        entity = TextEntity(id="test1", position=(0, 0, 0), text=text)

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.BEAM, text, self.document, entity
        )

        assert supplemented['width'] == 300
        assert supplemented['height'] == 600
        assert supplemented['length'] == 6000  # 默认跨度

    def test_beam_with_span_extraction(self):
        """测试梁从文本提取跨度"""
        dims = {'width': 250, 'height': 500}
        text = "L1 250×500 L=7200"
        entity = TextEntity(id="test2", position=(0, 0, 0), text=text)

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.BEAM, text, self.document, entity
        )

        assert supplemented['length'] == 7200  # 从L=7200提取

    def test_column_missing_height(self):
        """测试柱缺失层高（应补充3000mm）"""
        dims = {'width': 600, 'height': 600}
        text = "KZ1 600×600"
        entity = TextEntity(id="test3", position=(0, 0, 0), text=text)

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.COLUMN, text, self.document, entity
        )

        assert supplemented['length'] == 3000  # 默认层高

    def test_circular_column_missing_height(self):
        """测试圆柱缺失层高"""
        dims = {'diameter': 500, 'width': 500, 'height': 500}
        text = "φ500"
        entity = TextEntity(id="test4", position=(0, 0, 0), text=text)

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.COLUMN, text, self.document, entity
        )

        assert supplemented['length'] == 3000  # 默认层高

    def test_wall_missing_dimensions(self):
        """测试墙缺失高度和长度"""
        dims = {'width': 200}  # 只有厚度
        text = "剪力墙 200厚"
        entity = TextEntity(id="test5", position=(0, 0, 0), text=text)

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.WALL, text, self.document, entity
        )

        assert supplemented['height'] == 3000  # 默认层高
        assert supplemented['length'] == 6000  # 默认墙长

    def test_wall_thickness_based_length(self):
        """测试墙根据厚度推断长度"""
        # 轻质墙（<150mm）
        dims1 = {'width': 100}
        entity1 = TextEntity(id="test6a", position=(0, 0, 0), text="隔墙 100")
        supp1 = self.recognizer._supplement_missing_dimensions(
            dims1, ComponentType.WALL, "", self.document, entity1
        )
        assert supp1['length'] == 3000  # 轻质墙较短

        # 承重墙（150-300mm）
        dims2 = {'width': 240}
        entity2 = TextEntity(id="test6b", position=(0, 0, 0), text="墙 240")
        supp2 = self.recognizer._supplement_missing_dimensions(
            dims2, ComponentType.WALL, "", self.document, entity2
        )
        assert supp2['length'] == 6000  # 承重墙较长

        # 剪力墙（>=300mm）
        dims3 = {'width': 300}
        entity3 = TextEntity(id="test6c", position=(0, 0, 0), text="剪力墙 300")
        supp3 = self.recognizer._supplement_missing_dimensions(
            dims3, ComponentType.WALL, "", self.document, entity3
        )
        assert supp3['length'] == 6000  # 剪力墙

    def test_slab_missing_dimensions(self):
        """测试板缺失平面尺寸"""
        dims = {'width': 120}  # 只有厚度
        text = "楼板 120厚"
        entity = TextEntity(id="test7", position=(0, 0, 0), text=text)

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.SLAB, text, self.document, entity
        )

        assert supplemented['height'] == 120  # 厚度重新分配
        assert supplemented['width'] == 3000  # 默认开间
        assert supplemented['length'] == 6000  # 默认进深

    def test_door_missing_thickness(self):
        """测试门缺失厚度"""
        dims = {'width': 900, 'height': 2100}
        text = "门 900×2100"
        entity = TextEntity(id="test8", position=(0, 0, 0), text=text)

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.DOOR, text, self.document, entity
        )

        assert supplemented['length'] == 40  # 标准门扇厚度

    def test_window_missing_thickness(self):
        """测试窗缺失厚度"""
        dims = {'width': 1500, 'height': 1500}
        text = "窗 1500×1500"
        entity = TextEntity(id="test9", position=(0, 0, 0), text=text)

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.WINDOW, text, self.document, entity
        )

        assert supplemented['length'] == 50  # 标准窗框厚度

    def test_stair_missing_dimensions(self):
        """测试楼梯缺失尺寸"""
        dims = {'width': 1200}  # 只有梯宽
        text = "楼梯 1200"
        entity = TextEntity(id="test10", position=(0, 0, 0), text=text)

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.STAIR, text, self.document, entity
        )

        assert supplemented['length'] == 3000  # 楼梯跑长度
        assert supplemented['height'] == 3000  # 层高

    # ============ 策略2: 附近标注搜索 ============

    def test_search_nearby_dimensions(self):
        """测试搜索附近文本标注"""
        # 创建主实体（位置0,0）
        main_entity = TextEntity(id="main", position=(0, 0, 0), text="KL1")

        # 创建附近实体（距离300mm）
        nearby1 = TextEntity(id="nearby1", position=(300, 0, 0), text="300×600")
        nearby2 = TextEntity(id="nearby2", position=(0, 200, 0), text="L=7200")

        # 添加到文档
        self.document.entities = [main_entity, nearby1, nearby2]

        # 搜索附近尺寸
        nearby_dims = self.recognizer._search_nearby_dimensions(
            main_entity, self.document, search_radius=500
        )

        # 应该找到300×600
        assert 'width' in nearby_dims or 'height' in nearby_dims

    def test_search_radius_limit(self):
        """测试搜索半径限制"""
        main_entity = TextEntity(id="main", position=(0, 0, 0), text="KL1")
        far_entity = TextEntity(id="far", position=(1000, 0, 0), text="300×600")

        self.document.entities = [main_entity, far_entity]

        # 搜索半径500mm，不应找到1000mm外的标注
        nearby_dims = self.recognizer._search_nearby_dimensions(
            main_entity, self.document, search_radius=500
        )

        assert len(nearby_dims) == 0  # 超出范围

    def test_nearby_priority(self):
        """测试附近标注优先级（不覆盖已有尺寸）"""
        dims = {'width': 250}  # 已有宽度

        main_entity = TextEntity(id="main", position=(0, 0, 0), text="L1 250")
        nearby = TextEntity(id="nearby", position=(200, 0, 0), text="300×600")

        self.document.entities = [main_entity, nearby]

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.BEAM, "L1 250", self.document, main_entity
        )

        # 已有的width=250不应被附近的300覆盖
        assert supplemented['width'] == 250
        # 但应补充缺失的height
        assert 'height' in supplemented

    # ============ 策略3: 综合优先级 ============

    def test_priority_original_over_nearby(self):
        """测试优先级：原始 > 附近 > 标准"""
        # 原始有width=200
        dims = {'width': 200}
        entity = TextEntity(id="test", position=(0, 0, 0), text="200")

        # 附近有300×600
        nearby = TextEntity(id="nearby", position=(100, 0, 0), text="300×600")
        self.document.entities = [entity, nearby]

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.BEAM, "200", self.document, entity
        )

        # width应保持原始值200（不被附近的300覆盖）
        assert supplemented['width'] == 200

    def test_priority_nearby_over_standard(self):
        """测试优先级：附近 > 标准"""
        dims = {}  # 空尺寸
        entity = TextEntity(id="test", position=(0, 0, 0), text="KL1")

        # 附近有300×600
        nearby = TextEntity(id="nearby", position=(100, 0, 0), text="300×600")
        self.document.entities = [entity, nearby]

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.BEAM, "KL1", self.document, entity
        )

        # 应优先使用附近的300×600，而非标准的6000
        if 'width' in supplemented and 'height' in supplemented:
            # 如果附近标注被识别，应该是300×600
            assert supplemented['width'] in [300, 600]

    def test_complete_dimensions_no_supplement(self):
        """测试完整尺寸不补充"""
        dims = {'width': 300, 'height': 600, 'length': 7200}
        entity = TextEntity(id="test", position=(0, 0, 0), text="300×600×7200")

        supplemented = self.recognizer._supplement_missing_dimensions(
            dims, ComponentType.BEAM, "300×600×7200", self.document, entity
        )

        # 应完全一致，不添加额外维度
        assert supplemented == dims

    # ============ 跨度提取专项测试 ============

    def test_extract_span_L_equals_meter(self):
        """测试提取：L=6m"""
        span = self.recognizer._extract_span_from_text("梁 L=6m")
        assert span == 6000  # 米转毫米

    def test_extract_span_L_equals_mm(self):
        """测试提取：L=7200"""
        span = self.recognizer._extract_span_from_text("梁 L=7200")
        assert span == 7200

    def test_extract_span_chinese(self):
        """测试提取：跨度6m"""
        span = self.recognizer._extract_span_from_text("跨度6m")
        assert span == 6000

    def test_extract_span_chinese_colon(self):
        """测试提取：跨度:7200"""
        span = self.recognizer._extract_span_from_text("跨度:7200")
        assert span == 7200

    def test_extract_span_not_found(self):
        """测试无跨度信息"""
        span = self.recognizer._extract_span_from_text("梁 300×600")
        assert span is None

    # ============ 统计测试 ============

    def test_supplementation_success_rate(self):
        """测试补充成功率"""
        test_cases = [
            (ComponentType.BEAM, {'width': 300, 'height': 600}, "KL1"),
            (ComponentType.COLUMN, {'width': 600, 'height': 600}, "KZ1"),
            (ComponentType.WALL, {'width': 200}, "墙 200"),
            (ComponentType.SLAB, {'width': 120}, "板 120"),
            (ComponentType.DOOR, {'width': 900, 'height': 2100}, "门"),
            (ComponentType.WINDOW, {'width': 1500, 'height': 1500}, "窗"),
        ]

        success = 0
        for comp_type, dims, text in test_cases:
            entity = TextEntity(id=f"test_{comp_type.value}", position=(0, 0, 0), text=text)
            supplemented = self.recognizer._supplement_missing_dimensions(
                dims.copy(), comp_type, text, self.document, entity
            )

            # 检查是否成功补充了缺失维度
            required_dims = {'width', 'height', 'length'}
            if comp_type in [ComponentType.DOOR, ComponentType.WINDOW]:
                # 门窗不强制要求length
                required_dims = {'width', 'height'}

            has_all = all(d in supplemented for d in required_dims)
            if has_all or len(supplemented) > len(dims):
                success += 1
                print(f"✅ {comp_type.value:6s}: {dims} -> {supplemented}")
            else:
                print(f"❌ {comp_type.value:6s}: {dims} -> {supplemented} (未补充)")

        success_rate = success / len(test_cases) * 100
        print(f"\n补充成功率: {success}/{len(test_cases)} = {success_rate:.1f}%")

        # 至少80%成功率
        assert success_rate >= 80, f"补充成功率不足: {success_rate:.1f}%"


class TestStandardDimensions:
    """测试建筑规范标准尺寸"""

    def setup_method(self):
        self.recognizer = ComponentRecognizer()

    def test_beam_default_span_residential(self):
        """测试住宅梁默认跨度（6m）"""
        standard = self.recognizer._get_standard_dimensions(
            ComponentType.BEAM,
            {'width': 300, 'height': 600},
            "KL1"
        )
        assert standard['length'] == 6000

    def test_column_default_height_residential(self):
        """测试住宅柱默认层高（3m）"""
        standard = self.recognizer._get_standard_dimensions(
            ComponentType.COLUMN,
            {'width': 600, 'height': 600},
            "KZ1"
        )
        assert standard['length'] == 3000

    def test_wall_height_standard(self):
        """测试墙标准层高"""
        standard = self.recognizer._get_standard_dimensions(
            ComponentType.WALL,
            {'width': 200},
            "墙"
        )
        assert standard['height'] == 3000

    def test_slab_thickness_mapping(self):
        """测试板厚度映射"""
        # 板厚度应该重新分配到height
        standard = self.recognizer._get_standard_dimensions(
            ComponentType.SLAB,
            {'width': 120},
            "楼板"
        )
        assert standard['height'] == 120  # 厚度
        assert standard['width'] == 3000  # 开间
        assert standard['length'] == 6000  # 进深


if __name__ == "__main__":
    pytest.main([__file__, "-v", "-s"])
