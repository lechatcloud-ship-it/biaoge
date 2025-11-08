# -*- coding: utf-8 -*-
"""
建筑行业专业术语词典和翻译规则

基于GB/T 50001-2017、16G101等建筑规范
确保翻译和识别的专业性和准确性
"""
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass
from enum import Enum


class ComponentCategory(Enum):
    """构件大类"""
    STRUCTURE = "结构构件"
    ARCHITECTURE = "建筑构件"
    MEP = "机电构件"
    DECORATION = "装饰构件"


@dataclass
class ComponentTerm:
    """构件术语"""
    chinese: str  # 中文
    english: str  # 英文
    abbreviation: str  # 缩写
    category: ComponentCategory
    aliases: List[str]  # 别名
    standard_code: Optional[str] = None  # 标准代号


# ============================================
# 结构构件专业术语库（基于16G101-1等规范）
# ============================================

STRUCTURE_TERMS = {
    # 梁类
    "框架梁": ComponentTerm(
        chinese="框架梁",
        english="Frame Beam",
        abbreviation="KL",
        category=ComponentCategory.STRUCTURE,
        aliases=["KL", "框梁", "主梁"],
        standard_code="16G101-1"
    ),
    "次梁": ComponentTerm(
        chinese="次梁",
        english="Secondary Beam",
        abbreviation="L",
        category=ComponentCategory.STRUCTURE,
        aliases=["L", "连系梁"],
        standard_code="16G101-1"
    ),
    "连梁": ComponentTerm(
        chinese="连梁",
        english="Coupling Beam",
        abbreviation="LL",
        category=ComponentCategory.STRUCTURE,
        aliases=["LL", "连系梁"],
        standard_code="16G101-1"
    ),
    "圈梁": ComponentTerm(
        chinese="圈梁",
        english="Ring Beam",
        abbreviation="QL",
        category=ComponentCategory.STRUCTURE,
        aliases=["QL", "环梁"],
        standard_code="16G101-1"
    ),
    "过梁": ComponentTerm(
        chinese="过梁",
        english="Lintel",
        abbreviation="GL",
        category=ComponentCategory.STRUCTURE,
        aliases=["GL", "门窗过梁"],
        standard_code="16G101-1"
    ),
    "基础梁": ComponentTerm(
        chinese="基础梁",
        english="Foundation Beam",
        abbreviation="JL",
        category=ComponentCategory.STRUCTURE,
        aliases=["JL", "地基梁"],
        standard_code="16G101-1"
    ),

    # 柱类
    "框架柱": ComponentTerm(
        chinese="框架柱",
        english="Frame Column",
        abbreviation="KZ",
        category=ComponentCategory.STRUCTURE,
        aliases=["KZ", "框柱"],
        standard_code="16G101-1"
    ),
    "构造柱": ComponentTerm(
        chinese="构造柱",
        english="Constructional Column",
        abbreviation="GZ",
        category=ComponentCategory.STRUCTURE,
        aliases=["GZ", "构柱"],
        standard_code="16G101-1"
    ),
    "芯柱": ComponentTerm(
        chinese="芯柱",
        english="Core Column",
        abbreviation="XZ",
        category=ComponentCategory.STRUCTURE,
        aliases=["XZ"],
        standard_code="16G101-1"
    ),
    "梁上柱": ComponentTerm(
        chinese="梁上柱",
        english="Column on Beam",
        abbreviation="LSZ",
        category=ComponentCategory.STRUCTURE,
        aliases=["LSZ"],
        standard_code="16G101-1"
    ),

    # 墙类
    "剪力墙": ComponentTerm(
        chinese="剪力墙",
        english="Shear Wall",
        abbreviation="Q",
        category=ComponentCategory.STRUCTURE,
        aliases=["Q", "墙", "抗震墙"],
        standard_code="16G101-1"
    ),
    "承重墙": ComponentTerm(
        chinese="承重墙",
        english="Load-bearing Wall",
        abbreviation="CQ",
        category=ComponentCategory.STRUCTURE,
        aliases=["CQ", "承墙"],
        standard_code="16G101-1"
    ),
    "填充墙": ComponentTerm(
        chinese="填充墙",
        english="Infill Wall",
        abbreviation="TC",
        category=ComponentCategory.STRUCTURE,
        aliases=["TC", "隔墙", "非承重墙"],
        standard_code="GB 50003"
    ),
    "地下室外墙": ComponentTerm(
        chinese="地下室外墙",
        english="Basement Exterior Wall",
        abbreviation="DWQ",
        category=ComponentCategory.STRUCTURE,
        aliases=["DWQ", "地外墙"],
        standard_code="16G101-1"
    ),

    # 板类
    "楼板": ComponentTerm(
        chinese="楼板",
        english="Floor Slab",
        abbreviation="LB",
        category=ComponentCategory.STRUCTURE,
        aliases=["LB", "板", "现浇板"],
        standard_code="16G101-1"
    ),
    "屋面板": ComponentTerm(
        chinese="屋面板",
        english="Roof Slab",
        abbreviation="WB",
        category=ComponentCategory.STRUCTURE,
        aliases=["WB", "屋板"],
        standard_code="16G101-1"
    ),
    "阳台板": ComponentTerm(
        chinese="阳台板",
        english="Balcony Slab",
        abbreviation="YTB",
        category=ComponentCategory.STRUCTURE,
        aliases=["YTB"],
        standard_code="16G101-1"
    ),
    "雨篷": ComponentTerm(
        chinese="雨篷",
        english="Canopy",
        abbreviation="YP",
        category=ComponentCategory.STRUCTURE,
        aliases=["YP", "挑檐"],
        standard_code="16G101-1"
    ),

    # 基础类
    "独立基础": ComponentTerm(
        chinese="独立基础",
        english="Isolated Footing",
        abbreviation="DJC",
        category=ComponentCategory.STRUCTURE,
        aliases=["DJC", "独基"],
        standard_code="16G101-3"
    ),
    "条形基础": ComponentTerm(
        chinese="条形基础",
        english="Strip Footing",
        abbreviation="TJC",
        category=ComponentCategory.STRUCTURE,
        aliases=["TJC", "条基"],
        standard_code="16G101-3"
    ),
    "筏板基础": ComponentTerm(
        chinese="筏板基础",
        english="Raft Foundation",
        abbreviation="FB",
        category=ComponentCategory.STRUCTURE,
        aliases=["FB", "筏基"],
        standard_code="16G101-3"
    ),
    "桩基础": ComponentTerm(
        chinese="桩基础",
        english="Pile Foundation",
        abbreviation="ZJC",
        category=ComponentCategory.STRUCTURE,
        aliases=["ZJC", "桩基"],
        standard_code="16G101-3"
    ),
}

# ============================================
# 建筑构件专业术语库（基于GB 50352等规范）
# ============================================

ARCHITECTURE_TERMS = {
    # 门类
    "防火门": ComponentTerm(
        chinese="防火门",
        english="Fire Door",
        abbreviation="FM",
        category=ComponentCategory.ARCHITECTURE,
        aliases=["FM", "防火门"],
        standard_code="GB 12955"
    ),
    "防盗门": ComponentTerm(
        chinese="防盗门",
        english="Security Door",
        abbreviation="FDM",
        category=ComponentCategory.ARCHITECTURE,
        aliases=["FDM", "防盗门"],
        standard_code="GB 17565"
    ),
    "木门": ComponentTerm(
        chinese="木门",
        english="Wooden Door",
        abbreviation="MM",
        category=ComponentCategory.ARCHITECTURE,
        aliases=["MM"],
        standard_code="GB/T 29498"
    ),
    "铝合金门": ComponentTerm(
        chinese="铝合金门",
        english="Aluminum Door",
        abbreviation="LM",
        category=ComponentCategory.ARCHITECTURE,
        aliases=["LM", "铝门"],
        standard_code="GB/T 8478"
    ),

    # 窗类
    "铝合金窗": ComponentTerm(
        chinese="铝合金窗",
        english="Aluminum Window",
        abbreviation="LC",
        category=ComponentCategory.ARCHITECTURE,
        aliases=["LC", "铝窗"],
        standard_code="GB/T 8478"
    ),
    "塑钢窗": ComponentTerm(
        chinese="塑钢窗",
        english="UPVC Window",
        abbreviation="SC",
        category=ComponentCategory.ARCHITECTURE,
        aliases=["SC", "塑窗"],
        standard_code="GB/T 28887"
    ),
    "木窗": ComponentTerm(
        chinese="木窗",
        english="Wooden Window",
        abbreviation="MC",
        category=ComponentCategory.ARCHITECTURE,
        aliases=["MC"],
        standard_code="GB/T 29498"
    ),
    "飘窗": ComponentTerm(
        chinese="飘窗",
        english="Bay Window",
        abbreviation="PC",
        category=ComponentCategory.ARCHITECTURE,
        aliases=["PC", "凸窗"],
        standard_code="JGJ/T 471"
    ),

    # 楼梯类
    "楼梯": ComponentTerm(
        chinese="楼梯",
        english="Stair",
        abbreviation="LT",
        category=ComponentCategory.ARCHITECTURE,
        aliases=["LT", "楼梯间"],
        standard_code="GB 50096"
    ),
    "电梯": ComponentTerm(
        chinese="电梯",
        english="Elevator",
        abbreviation="DT",
        category=ComponentCategory.ARCHITECTURE,
        aliases=["DT"],
        standard_code="GB 7588"
    ),
    "扶梯": ComponentTerm(
        chinese="扶梯",
        english="Escalator",
        abbreviation="FT",
        category=ComponentCategory.ARCHITECTURE,
        aliases=["FT", "自动扶梯"],
        standard_code="GB 16899"
    ),
}

# ============================================
# 尺寸标注专业术语（基于GB/T 50001-2017）
# ============================================

DIMENSION_TERMS = {
    "轴线": ["轴", "轴线", "AXIS"],
    "标高": ["标高", "EL", "±0.000"],
    "尺寸": ["尺寸", "DIM", "DIMENSION"],
    "跨度": ["跨度", "L", "SPAN"],
    "开间": ["开间", "BAY"],
    "进深": ["进深", "DEPTH"],
    "层高": ["层高", "H", "FLOOR HEIGHT"],
    "净高": ["净高", "净空", "CLEAR HEIGHT"],
    "厚度": ["厚", "厚度", "THK", "THICKNESS"],
    "直径": ["φ", "Φ", "ø", "∅", "DIA", "DIAMETER", "直径"],
    "半径": ["R", "r", "RADIUS", "半径"],
    "截面": ["截面", "SECTION", "b×h"],
}

# ============================================
# 材料专业术语（基于GB 50010等规范）
# ============================================

MATERIAL_TERMS = {
    # 混凝土
    "混凝土": ["混凝土", "砼", "C", "CONCRETE"],
    "C20": "C20混凝土（强度等级）",
    "C25": "C25混凝土（强度等级）",
    "C30": "C30混凝土（强度等级）",
    "C35": "C35混凝土（强度等级）",
    "C40": "C40混凝土（强度等级）",
    "C50": "C50混凝土（强度等级）",

    # 钢筋
    "钢筋": ["钢筋", "HPB", "HRB", "REBAR"],
    "HPB300": "HPB300（一级钢）",
    "HRB335": "HRB335（二级钢）",
    "HRB400": "HRB400（三级钢）",
    "HRB500": "HRB500（四级钢）",

    # 砌体
    "砖": ["砖", "BRICK"],
    "加气块": ["加气块", "AAC", "加气混凝土砌块"],
    "空心砖": ["空心砖", "HOLLOW BRICK"],
}

# ============================================
# 建筑楼层专业术语
# ============================================

FLOOR_TERMS = {
    "地下室": ["地下室", "地下", "B", "BASEMENT"],
    "地下一层": ["地下一层", "B1", "-1F"],
    "地下二层": ["地下二层", "B2", "-2F"],
    "地下三层": ["地下三层", "B3", "-3F"],
    "首层": ["首层", "一层", "1F", "GROUND FLOOR"],
    "二层": ["二层", "2F", "SECOND FLOOR"],
    "三层": ["三层", "3F", "THIRD FLOOR"],
    "屋顶层": ["屋顶层", "屋面", "RF", "ROOF"],
    "阁楼": ["阁楼", "ATTIC"],
    "夹层": ["夹层", "MEZZANINE"],
}

# ============================================
# 翻译规则和质量控制
# ============================================

class TranslationRules:
    """翻译规则类"""

    # 必须保留的专业术语（不翻译）
    PRESERVE_TERMS = [
        "KL", "KZ", "Q", "LL", "GL", "QL", "JL",  # 构件缩写
        "C20", "C25", "C30", "C35", "C40", "C50",  # 混凝土等级
        "HPB300", "HRB335", "HRB400", "HRB500",   # 钢筋等级
        "φ", "Φ", "ø", "∅",                        # 直径符号
        "±0.000",                                  # 标高基准
    ]

    # 尺寸单位映射
    UNIT_MAPPING = {
        "mm": "毫米",
        "cm": "厘米",
        "m": "米",
        "km": "千米",
        '"': "英寸",
        "'": "英尺",
    }

    # 中英文术语对照
    TERM_MAPPING = {
        "Frame Beam": "框架梁",
        "Frame Column": "框架柱",
        "Shear Wall": "剪力墙",
        "Floor Slab": "楼板",
        "Basement": "地下室",
    }

    @staticmethod
    def should_preserve(text: str) -> bool:
        """判断是否应保留原文"""
        return text in TranslationRules.PRESERVE_TERMS

    @staticmethod
    def normalize_component_name(text: str) -> str:
        """标准化构件名称"""
        # 移除多余空格
        text = " ".join(text.split())

        # 标准化常见缩写
        replacements = {
            "框梁": "框架梁",
            "框柱": "框架柱",
            "剪墙": "剪力墙",
        }

        for old, new in replacements.items():
            text = text.replace(old, new)

        return text


# ============================================
# 建筑规范尺寸数据库
# ============================================

class ConstructionStandards:
    """建筑规范标准尺寸数据库"""

    # GB 50011-2010 建筑抗震设计规范
    # 框架梁最小截面尺寸
    FRAME_BEAM_MIN = {
        "width": 200,   # 最小梁宽200mm
        "height": 250,  # 最小梁高250mm
        "width_height_ratio": (0.3, 1.0),  # 宽高比范围
    }

    # 框架柱最小截面尺寸
    FRAME_COLUMN_MIN = {
        "width": 300,    # 最小柱宽300mm（抗震等级一二级）
        "height": 300,   # 最小柱高300mm
        "diameter": 300, # 最小直径300mm（圆柱）
    }

    # GB 50009-2012 荷载规范
    # 楼板厚度标准
    SLAB_THICKNESS = {
        "residential": [100, 120, 150],  # 住宅楼板（常见）
        "office": [120, 150, 180],       # 办公楼板
        "parking": [150, 180, 200],      # 车库楼板
    }

    # 墙体厚度标准
    WALL_THICKNESS = {
        "shear_wall": [200, 250, 300, 350, 400],  # 剪力墙
        "infill_wall": [100, 120, 150, 180, 200], # 填充墙
        "exterior_wall": [200, 240, 300],         # 外墙
    }

    # 16G101-1 常用构件尺寸
    COMMON_SIZES = {
        "beam": {
            "200x300", "200x400", "200x450", "200x500",
            "250x400", "250x450", "250x500", "250x600",
            "300x500", "300x600", "300x700", "300x800",
            "350x600", "350x700", "350x800",
            "400x700", "400x800", "400x900",
        },
        "column": {
            "300x300", "350x350", "400x400", "450x450",
            "500x500", "550x550", "600x600", "700x700",
            "800x800", "400x600", "400x700", "500x700",
        },
    }

    # 层高标准（GB 50096）
    FLOOR_HEIGHT = {
        "residential": 2800,    # 住宅标准层高2.8m
        "residential_min": 2600, # 住宅最低层高2.6m
        "office": 3600,         # 办公楼标准层高3.6m
        "commercial": 3900,     # 商业建筑标准层高3.9m
        "industrial": 4200,     # 工业建筑标准层高4.2m
    }

    # 开间进深标准模数（GBJ 2-86）
    BAY_DEPTH = {
        "residential_bay": [2700, 3000, 3300, 3600, 3900, 4200],  # 住宅开间
        "residential_depth": [4200, 4500, 4800, 5100, 5400, 6000], # 住宅进深
        "modulus": 300,  # 建筑模数300mm
    }


# ============================================
# 专业术语匹配工具类
# ============================================

class TermMatcher:
    """专业术语匹配工具"""

    @staticmethod
    def match_component_type(text: str) -> Optional[str]:
        """匹配构件类型"""
        text_upper = text.upper()

        # 遍历所有术语
        for term_name, term in STRUCTURE_TERMS.items():
            if term.chinese in text or term.abbreviation in text_upper:
                return term.chinese
            for alias in term.aliases:
                if alias in text_upper:
                    return term.chinese

        for term_name, term in ARCHITECTURE_TERMS.items():
            if term.chinese in text or term.abbreviation in text_upper:
                return term.chinese
            for alias in term.aliases:
                if alias in text_upper:
                    return term.chinese

        return None

    @staticmethod
    def get_standard_abbreviation(component_name: str) -> Optional[str]:
        """获取标准缩写"""
        for term in {**STRUCTURE_TERMS, **ARCHITECTURE_TERMS}.values():
            if term.chinese == component_name:
                return term.abbreviation
        return None

    @staticmethod
    def validate_concrete_grade(text: str) -> bool:
        """验证混凝土强度等级"""
        import re
        # 匹配C20, C25, C30等
        pattern = r'C\d{2}'
        matches = re.findall(pattern, text.upper())
        if matches:
            grade = int(matches[0][1:])
            # 常见等级: C15, C20, C25, C30, C35, C40, C45, C50, C55, C60
            return 15 <= grade <= 60 and grade % 5 == 0
        return False


# ============================================
# 导出所有术语供其他模块使用
# ============================================

ALL_TERMS = {**STRUCTURE_TERMS, **ARCHITECTURE_TERMS}


def get_all_component_names() -> List[str]:
    """获取所有构件名称"""
    names = []
    for term in ALL_TERMS.values():
        names.append(term.chinese)
        names.extend(term.aliases)
    return list(set(names))


def get_all_abbreviations() -> List[str]:
    """获取所有缩写"""
    return [term.abbreviation for term in ALL_TERMS.values()]


if __name__ == "__main__":
    # 测试
    print("建筑专业术语库加载成功！")
    print(f"结构构件术语: {len(STRUCTURE_TERMS)} 个")
    print(f"建筑构件术语: {len(ARCHITECTURE_TERMS)} 个")
    print(f"所有构件名称: {len(get_all_component_names())} 个")

    # 测试术语匹配
    matcher = TermMatcher()
    test_texts = ["KL1 300×600", "KZ1 600×600", "剪力墙 200厚", "C30混凝土"]
    for text in test_texts:
        matched = matcher.match_component_type(text)
        print(f"  {text} -> {matched}")
