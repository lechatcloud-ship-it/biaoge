# -*- coding: utf-8 -*-
"""
Aspose.CAD Python API 详细测试
验证是否能够正确访问实体的几何属性
"""
import sys
from pathlib import Path

def test_aspose_api():
    """测试Aspose.CAD Python API的实际能力"""

    try:
        from aspose.cad import Image
        from aspose.cad.fileformats.cad import CadImage
        from aspose.cad.fileformats.cad.cadobjects import (
            CadBaseEntity, CadLine, CadCircle, CadText, CadPolyline
        )

        print("=" * 70)
        print("Aspose.CAD Python API 详细测试")
        print("=" * 70)

        # 查找测试DWG文件
        test_files = list(Path(".").rglob("*.dwg"))[:3]  # 最多测试3个文件

        if not test_files:
            print("\n⚠️  未找到测试DWG文件")
            print("\n📝 API类型检查:")
            print(f"  - CadBaseEntity: {CadBaseEntity}")
            print(f"  - CadLine: {CadLine}")
            print(f"  - CadCircle: {CadCircle}")
            print(f"  - CadText: {CadText}")
            print(f"  - CadPolyline: {CadPolyline}")

            print("\n✅ 结论: Aspose.CAD Python提供了具体的实体类型!")
            print("   问题可能在于类型转换方式不对")
            return

        print(f"\n✓ 找到 {len(test_files)} 个测试文件\n")

        for dwg_file in test_files:
            print(f"\n{'='*70}")
            print(f"测试文件: {dwg_file.name}")
            print(f"{'='*70}")

            try:
                # 加载DWG
                image = Image.load(str(dwg_file))

                if not isinstance(image, CadImage):
                    print("❌ 不是有效的CAD文件")
                    continue

                print(f"✓ 文件加载成功")
                print(f"  版本: {getattr(image, 'version', 'Unknown')}")
                print(f"  尺寸: {image.width} x {image.height}")

                # 检查实体访问
                if not hasattr(image, 'entities'):
                    print("❌ 无法访问entities属性")
                    continue

                entity_count = len(list(image.entities))
                print(f"  实体数量: {entity_count}")

                if entity_count == 0:
                    print("⚠️  文件中没有实体")
                    continue

                # 详细检查前10个实体
                print("\n🔍 详细实体分析（前10个）:")
                print("-" * 70)

                for i, entity in enumerate(image.entities):
                    if i >= 10:
                        break

                    print(f"\n实体 #{i+1}:")
                    print(f"  - 类型: {type(entity)}")
                    print(f"  - 类型名: {getattr(entity, 'type_name', 'N/A')}")

                    # 方法1: isinstance检查
                    if isinstance(entity, CadLine):
                        print("  ✅ isinstance(CadLine) = True")
                        if hasattr(entity, 'first_point'):
                            print(f"     起点: {entity.first_point}")
                        if hasattr(entity, 'second_point'):
                            print(f"     终点: {entity.second_point}")

                    elif isinstance(entity, CadCircle):
                        print("  ✅ isinstance(CadCircle) = True")
                        if hasattr(entity, 'center_point'):
                            print(f"     中心: {entity.center_point}")
                        if hasattr(entity, 'radius'):
                            print(f"     半径: {entity.radius}")

                    elif isinstance(entity, CadText):
                        print("  ✅ isinstance(CadText) = True")
                        if hasattr(entity, 'default_value'):
                            print(f"     文本: {entity.default_value}")

                    else:
                        print(f"  ⚠️  未知实体类型或isinstance失败")
                        print(f"     可用属性: {[attr for attr in dir(entity) if not attr.startswith('_')][:10]}")

                        # 尝试通过type_name判断
                        type_name = getattr(entity, 'type_name', None)
                        if type_name:
                            print(f"     type_name = {type_name}")

                            # 尝试访问常见属性
                            if type_name == 'LINE' or type_name == 'LWPOLYLINE':
                                if hasattr(entity, 'first_point'):
                                    print(f"     ✅ 可以访问first_point: {entity.first_point}")
                                else:
                                    print(f"     ❌ 无法访问first_point")

                                if hasattr(entity, 'bounds'):
                                    print(f"     ⚠️  只能访问bounds: {entity.bounds}")

                print("\n" + "=" * 70)

                # 统计分析
                type_stats = {}
                accessible_count = 0
                bounds_only_count = 0

                for entity in image.entities:
                    type_name = getattr(entity, 'type_name', 'UNKNOWN')
                    type_stats[type_name] = type_stats.get(type_name, 0) + 1

                    # 检查是否能访问几何属性
                    has_geometry = (
                        hasattr(entity, 'first_point') or
                        hasattr(entity, 'center_point') or
                        hasattr(entity, 'radius') or
                        hasattr(entity, 'vertices')
                    )

                    has_only_bounds = hasattr(entity, 'bounds') and not has_geometry

                    if has_geometry:
                        accessible_count += 1
                    if has_only_bounds:
                        bounds_only_count += 1

                print("\n📊 统计结果:")
                print(f"  总实体数: {entity_count}")
                print(f"  ✅ 可访问几何属性: {accessible_count} ({accessible_count/entity_count*100:.1f}%)")
                print(f"  ❌ 只能访问bounds: {bounds_only_count} ({bounds_only_count/entity_count*100:.1f}%)")

                print("\n📈 实体类型分布:")
                for type_name, count in sorted(type_stats.items(), key=lambda x: x[1], reverse=True)[:10]:
                    print(f"  - {type_name}: {count}")

            except Exception as e:
                print(f"❌ 文件处理失败: {e}")
                import traceback
                traceback.print_exc()

        print("\n" + "=" * 70)
        print("✅ 测试完成")
        print("=" * 70)

    except ImportError as e:
        print(f"❌ 导入失败: {e}")
        print("\n请确保已安装: pip install aspose-cad")
    except Exception as e:
        print(f"❌ 测试失败: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    test_aspose_api()
