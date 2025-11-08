#!/usr/bin/env python3
"""
快速功能测试 - 直接测试核心函数
"""
import sys
from pathlib import Path

# 添加项目根目录到路径
project_root = Path(__file__).parent
sys.path.insert(0, str(project_root))

from src.calculation.component_recognizer import ComponentRecognizer, ComponentType, Component
from src.calculation.result_validator import ResultValidator
from src.dwg.entities import DWGDocument, TextEntity, EntityType


def test_dimension_extraction():
    """测试尺寸提取功能"""
    print("\n" + "="*60)
    print("测试1: 尺寸提取功能（10+格式）")
    print("="*60)

    # 创建recognizer（不需要client，测试模式）
    recognizer = ComponentRecognizer(init_client=False)

    test_cases = [
        ("300×600", "乘号"),
        ("300*600", "星号"),
        ("300x600", "小写x"),
        ("300X600", "大写X"),
        ("300×600×900", "三维"),
        ("φ300", "直径φ"),
        ("Φ500", "直径Φ"),
        ("300, 600", "逗号"),
        ("b×h=400×800", "带标签"),
        ("300/600", "斜杠"),
        ("300-600", "短横线"),
        ("300(600)", "括号"),
        ("3m", "米转mm"),
        ("300cm", "厘米转mm"),
    ]

    success = 0
    for text, format_name in test_cases:
        dims = recognizer._extract_dimensions(text)
        if dims and 'width' in dims:
            success += 1
            print(f"  ✅ {format_name:12s}: {text:20s} -> {dims}")
        else:
            print(f"  ❌ {format_name:12s}: {text:20s} -> 提取失败")

    success_rate = success / len(test_cases) * 100
    print(f"\n  提取成功率: {success}/{len(test_cases)} = {success_rate:.1f}%")

    return success_rate >= 90


def test_dimension_supplementation():
    """测试尺寸补充功能"""
    print("\n" + "="*60)
    print("测试2: 尺寸补充功能（3策略）")
    print("="*60)

    recognizer = ComponentRecognizer(init_client=False)
    document = DWGDocument()

    test_cases = [
        (ComponentType.BEAM, {'width': 300, 'height': 600}, "KL1", "梁缺长度"),
        (ComponentType.COLUMN, {'width': 600, 'height': 600}, "KZ1", "柱缺层高"),
        (ComponentType.WALL, {'width': 200}, "墙 200", "墙缺高度长度"),
        (ComponentType.SLAB, {'width': 120}, "板 120", "板缺平面尺寸"),
    ]

    success = 0
    for comp_type, dims, text, desc in test_cases:
        entity = TextEntity(
            id=f"test_{comp_type.value}",
            entity_type=EntityType.TEXT,
            layer="0",
            color="7",
            position=(0, 0, 0),
            text=text
        )
        supplemented = recognizer._supplement_missing_dimensions(
            dims.copy(), comp_type, text, document, entity
        )

        # 检查是否成功补充
        original_keys = set(dims.keys())
        new_keys = set(supplemented.keys()) - original_keys

        if len(new_keys) > 0:
            success += 1
            print(f"  ✅ {desc:15s}: {dims} -> {supplemented} (新增: {new_keys})")
        else:
            print(f"  ❌ {desc:15s}: {dims} -> {supplemented} (未补充)")

    success_rate = success / len(test_cases) * 100
    print(f"\n  补充成功率: {success}/{len(test_cases)} = {success_rate:.1f}%")

    return success_rate >= 75


def test_result_validation():
    """测试结果验证功能"""
    print("\n" + "="*60)
    print("测试3: 结果验证功能（5维度）")
    print("="*60)

    validator = ResultValidator()

    # 创建测试构件
    components = [
        # 正常梁
        Component(id="1", type=ComponentType.BEAM, name="KL1", entities=[], properties={},
                 dimensions={'width': 300, 'height': 600, 'length': 6000}),
        # 正常柱
        Component(id="2", type=ComponentType.COLUMN, name="KZ1", entities=[], properties={},
                 dimensions={'width': 600, 'height': 600, 'length': 3000}),
        # 异常梁（宽高比异常）
        Component(id="3", type=ComponentType.BEAM, name="KL2", entities=[], properties={},
                 dimensions={'width': 200, 'height': 1200, 'length': 6000}),
        # 异常柱（体积为0）
        Component(id="4", type=ComponentType.COLUMN, name="KZ2", entities=[], properties={},
                 dimensions={'width': 600, 'height': 600}),
    ]

    result = validator.validate(components)

    print(f"  总构件: {result.total_components}")
    print(f"  ✅ 通过: {result.passed}")
    print(f"  ⚠️  警告: {result.warnings}")
    print(f"  ❌ 错误: {result.errors}")

    pass_rate = result.passed / result.total_components * 100
    print(f"\n  通过率: {pass_rate:.1f}%")

    # 检查错误捕获
    detected_issues = result.errors + result.warnings
    expected_issues = 2  # 应检测到2个异常

    capture_rate = min(detected_issues / expected_issues * 100, 100)
    print(f"  错误捕获率: {detected_issues}/{expected_issues} = {capture_rate:.1f}%")

    return pass_rate >= 50 and capture_rate >= 80


def test_performance():
    """测试性能"""
    print("\n" + "="*60)
    print("测试4: 性能测试")
    print("="*60)

    import time

    recognizer = ComponentRecognizer(init_client=False)

    # 测试尺寸提取性能
    test_texts = ["300×600", "φ500", "b×h=400×800", "3m×600mm"] * 250

    start = time.time()
    for text in test_texts:
        recognizer._extract_dimensions(text)
    elapsed = time.time() - start

    print(f"  尺寸提取: {len(test_texts)}次 耗时 {elapsed*1000:.1f}ms")
    print(f"  平均: {elapsed/len(test_texts)*1000:.3f}ms/次")

    # 目标：1000次提取 < 1秒
    performance_ok = elapsed < 1.0

    if performance_ok:
        print(f"  ✅ 性能达标（<1秒）")
    else:
        print(f"  ❌ 性能不达标（>{elapsed:.3f}秒）")

    return performance_ok


def main():
    """运行所有测试"""
    print("\n🚀 开始快速功能测试...")

    results = []

    # 运行各测试
    results.append(("尺寸提取", test_dimension_extraction()))
    results.append(("尺寸补充", test_dimension_supplementation()))
    results.append(("结果验证", test_result_validation()))
    results.append(("性能测试", test_performance()))

    # 总结
    print("\n" + "="*60)
    print("测试总结")
    print("="*60)

    passed = sum(1 for _, result in results if result)
    total = len(results)

    for name, result in results:
        status = "✅ 通过" if result else "❌ 失败"
        print(f"  {name:12s}: {status}")

    pass_rate = passed / total * 100
    print(f"\n  通过率: {passed}/{total} = {pass_rate:.1f}%")

    if pass_rate >= 75:
        print("\n  ✅ 测试通过！系统核心功能正常")
        return 0
    else:
        print("\n  ❌ 测试失败，需要修复")
        return 1


if __name__ == "__main__":
    sys.exit(main())
