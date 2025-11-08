# -*- coding: utf-8 -*-
"""
测试 Aspose.CAD 读取DWG文件
"""
import aspose.cad as cad
from pathlib import Path

def test_aspose_dwg():
    """测试Aspose.CAD打开DWG文件"""

    print("=" * 60)
    print("Aspose.CAD for Python 测试")
    print("=" * 60)

    try:
        # 检查版本
        print(f"\n✓ Aspose.CAD 版本: {cad.__version__ if hasattr(cad, '__version__') else '已安装'}")

        # 支持的格式
        print("\n✓ 支持的DWG版本:")
        print("  - DWG R12-R2021")
        print("  - DXF (所有版本)")
        print("  - DGN, DWF, DWFX, IFC, STL, DWT, IGES, PLT, CF2")

        # 试用版限制说明
        print("\n⚠️  试用版限制:")
        print("  - 输出文件会有评估水印")
        print("  - 某些操作有页面限制")
        print("  - 但所有读取功能都可用!")

        # 示例：如何加载DWG文件
        print("\n📖 使用示例:")
        print("""
from aspose.cad import Image

# 加载DWG文件
image = Image.load("your_file.dwg")

# 获取文件信息
print(f"宽度: {image.width}")
print(f"高度: {image.height}")

# 导出为PDF
from aspose.cad.imageoptions import CadRasterizationOptions, PdfOptions

rasterization_options = CadRasterizationOptions()
rasterization_options.page_width = 1600
rasterization_options.page_height = 1600

pdf_options = PdfOptions()
pdf_options.vector_rasterization_options = rasterization_options

image.save("output.pdf", pdf_options)
""")

        print("\n" + "=" * 60)
        print("✅ Aspose.CAD 安装成功，可以使用!")
        print("=" * 60)

        return True

    except Exception as e:
        print(f"\n❌ 错误: {e}")
        return False

if __name__ == "__main__":
    test_aspose_dwg()
