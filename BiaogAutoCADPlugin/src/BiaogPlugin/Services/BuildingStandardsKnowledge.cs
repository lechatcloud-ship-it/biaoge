using System;
using System.Collections.Generic;
using System.Linq;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 建筑规范知识库
    /// 包含GB标准、材料规格、构件尺寸等专业知识
    /// 供AI助手提供准确的专业建议
    /// </summary>
    public static class BuildingStandardsKnowledge
    {
        #region GB国家标准规范

        /// <summary>
        /// 国家建筑标准规范清单
        /// </summary>
        public static readonly Dictionary<string, string> NationalStandards = new()
        {
            ["GB 50854-2013"] = "房屋建筑和市政基础设施工程施工图设计文件审查要点",
            ["GB 50010-2010"] = "混凝土结构设计规范（2015年版）",
            ["GB 50010-2021"] = "混凝土结构设计规范（最新版）",
            ["GB 50003-2011"] = "砌体结构设计规范（2016年版）",
            ["GB 50500-2013"] = "建设工程工程量清单计价规范（2013年版）",
            ["GB 1499-2017"] = "钢筋混凝土用钢 第2部分：热轧带肋钢筋",
            ["GB 1499-2018"] = "钢筋混凝土用钢 第2部分：热轧带肋钢筋（最新版）",
            ["GB 175-2007"] = "通用硅酸盐水泥",
            ["JGJ 1-2014"] = "装配式混凝土结构技术规程",
            ["JGJ 3-2010"] = "高层建筑混凝土结构技术规程（2011年版）",
            ["GB 50009-2012"] = "建筑结构荷载规范（2012年版）",
            ["GB 50011-2010"] = "建筑抗震设计规范（2016年版）"
        };

        #endregion

        #region 混凝土强度等级

        /// <summary>
        /// 混凝土强度等级（GB 50010-2010）
        /// </summary>
        public static class ConcreteGrades
        {
            /// <summary>
            /// 所有标准混凝土强度等级
            /// </summary>
            public static readonly List<string> AllGrades = new()
            {
                "C15", "C20", "C25", "C30", "C35", "C40",
                "C45", "C50", "C55", "C60", "C65", "C70",
                "C75", "C80"
            };

            /// <summary>
            /// 常用混凝土强度等级及其应用
            /// </summary>
            public static readonly Dictionary<string, string> CommonUsage = new()
            {
                ["C15"] = "基础垫层、地坪、道路",
                ["C20"] = "基础、地下室外墙、垫层",
                ["C25"] = "基础、梁、板、柱（非承重）",
                ["C30"] = "框架结构梁、板、柱（常用）",
                ["C35"] = "高层建筑梁、板、柱",
                ["C40"] = "高层建筑主要构件、预应力构件",
                ["C50"] = "超高层建筑、大跨度结构",
                ["C60"] = "超高层核心筒、特殊结构"
            };

            /// <summary>
            /// 不同构件的最低混凝土强度要求
            /// </summary>
            public static readonly Dictionary<string, string> MinimumRequirements = new()
            {
                ["框架柱"] = "C25（GB 50010-2010）",
                ["框架梁"] = "C25",
                ["楼板"] = "C20",
                ["剪力墙"] = "C25",
                ["基础"] = "C20",
                ["基础垫层"] = "C15",
                ["预应力构件"] = "C40"
            };

            /// <summary>
            /// 验证混凝土强度等级是否有效
            /// </summary>
            public static bool IsValid(string grade)
            {
                return AllGrades.Contains(grade.ToUpper());
            }

            /// <summary>
            /// 获取混凝土强度等级的数值（MPa）
            /// </summary>
            public static int GetStrength(string grade)
            {
                if (grade.StartsWith("C") && int.TryParse(grade.Substring(1), out int strength))
                {
                    return strength;
                }
                return 0;
            }

            /// <summary>
            /// 比较两个混凝土强度等级
            /// </summary>
            /// <returns>返回-1、0或1，类似CompareTo</returns>
            public static int Compare(string grade1, string grade2)
            {
                int strength1 = GetStrength(grade1);
                int strength2 = GetStrength(grade2);
                return strength1.CompareTo(strength2);
            }

            /// <summary>
            /// 检查强度等级是否满足最低要求
            /// </summary>
            public static bool MeetsMinimumRequirement(string componentType, string actualGrade)
            {
                if (!MinimumRequirements.ContainsKey(componentType))
                    return true; // 未定义的构件类型，不检查

                var minRequirement = MinimumRequirements[componentType];
                var minGrade = minRequirement.Split('（')[0]; // 提取 "C25" from "C25（GB 50010-2010）"

                return Compare(actualGrade, minGrade) >= 0;
            }
        }

        #endregion

        #region 钢筋型号规格

        /// <summary>
        /// 钢筋型号规格（GB 1499-2017/2018）
        /// </summary>
        public static class RebarGrades
        {
            /// <summary>
            /// 所有标准钢筋型号
            /// </summary>
            public static readonly List<string> AllGrades = new()
            {
                "HPB300",  // 光圆钢筋（一级钢）
                "HRB335",  // 热轧带肋钢筋（旧标准，新标准已取消）
                "HRB400",  // 热轧带肋钢筋（最常用）
                "HRB500",  // 热轧带肋钢筋（高强度）
                "RRB400",  // 余热处理钢筋
                "HRBF400", // 细晶粒热轧带肋钢筋
                "HRBF500"  // 细晶粒热轧带肋钢筋（高强度）
            };

            /// <summary>
            /// 钢筋型号的屈服强度（MPa）
            /// </summary>
            public static readonly Dictionary<string, int> YieldStrength = new()
            {
                ["HPB300"] = 300,
                ["HRB335"] = 335,
                ["HRB400"] = 400,
                ["HRB500"] = 500,
                ["RRB400"] = 400,
                ["HRBF400"] = 400,
                ["HRBF500"] = 500
            };

            /// <summary>
            /// 钢筋型号的应用说明
            /// </summary>
            public static readonly Dictionary<string, string> Usage = new()
            {
                ["HPB300"] = "板、墙的分布筋，构造筋，箍筋（光面圆钢）",
                ["HRB335"] = "已废止（GB 1499-2018取消此型号），建议使用HRB400",
                ["HRB400"] = "梁、柱、板的主筋和箍筋（最常用）",
                ["HRB500"] = "高层建筑、大跨度结构的主筋",
                ["RRB400"] = "预制构件、装配式结构",
                ["HRBF400"] = "抗震要求高的结构（细晶粒钢）",
                ["HRBF500"] = "抗震要求高的高层结构"
            };

            /// <summary>
            /// 常用钢筋直径（mm）
            /// </summary>
            public static readonly List<int> CommonDiameters = new()
            {
                6, 8, 10, 12, 14, 16, 18, 20, 22, 25, 28, 32, 36, 40
            };

            /// <summary>
            /// 验证钢筋型号是否有效
            /// </summary>
            public static bool IsValid(string grade)
            {
                return AllGrades.Contains(grade.ToUpper());
            }

            /// <summary>
            /// 检查钢筋型号是否已废止
            /// </summary>
            public static bool IsObsolete(string grade)
            {
                return grade.ToUpper() == "HRB335";
            }

            /// <summary>
            /// 获取替代的钢筋型号
            /// </summary>
            public static string GetReplacement(string obsoleteGrade)
            {
                if (obsoleteGrade.ToUpper() == "HRB335")
                    return "HRB400";
                return obsoleteGrade;
            }
        }

        #endregion

        #region 构件尺寸规范

        /// <summary>
        /// 构件尺寸规范（GB 50010-2010）
        /// </summary>
        public static class ComponentDimensions
        {
            /// <summary>
            /// 框架柱最小尺寸（mm）
            /// </summary>
            public const int MinimumColumnSize = 300;

            /// <summary>
            /// 框架梁最小宽度（mm）
            /// </summary>
            public const int MinimumBeamWidth = 200;

            /// <summary>
            /// 楼板最小厚度（mm）
            /// </summary>
            public static readonly Dictionary<string, int> MinimumSlabThickness = new()
            {
                ["非结构板"] = 80,
                ["结构楼板"] = 100,
                ["屋面板"] = 100,
                ["阳台板"] = 100
            };

            /// <summary>
            /// 承重墙最小厚度（mm）
            /// </summary>
            public static readonly Dictionary<string, int> MinimumWallThickness = new()
            {
                ["承重墙"] = 240,
                ["剪力墙"] = 200,
                ["隔墙（非承重）"] = 120,
                ["地下室外墙"] = 250
            };

            /// <summary>
            /// 梁高跨比（梁高 ≥ 跨度 / 比值）
            /// </summary>
            public static readonly Dictionary<string, int> BeamDepthSpanRatio = new()
            {
                ["简支梁"] = 12,
                ["连续梁"] = 15,
                ["悬臂梁"] = 8
            };

            /// <summary>
            /// 验证柱子尺寸是否满足最小要求
            /// </summary>
            public static (bool isValid, string message) ValidateColumnSize(int width, int depth)
            {
                if (width < MinimumColumnSize || depth < MinimumColumnSize)
                {
                    return (false, $"柱截面尺寸不满足最小要求（≥{MinimumColumnSize}mm）");
                }
                return (true, "柱截面尺寸符合规范");
            }

            /// <summary>
            /// 验证梁尺寸是否满足最小要求
            /// </summary>
            public static (bool isValid, string message) ValidateBeamSize(int width, int depth, double spanLength, string beamType = "简支梁")
            {
                // 检查宽度
                if (width < MinimumBeamWidth)
                {
                    return (false, $"梁宽不满足最小要求（≥{MinimumBeamWidth}mm）");
                }

                // 检查高跨比
                if (BeamDepthSpanRatio.ContainsKey(beamType))
                {
                    double minDepth = spanLength / BeamDepthSpanRatio[beamType] * 1000; // 转换为mm
                    if (depth < minDepth)
                    {
                        return (false, $"梁高不满足高跨比要求（应≥{minDepth:F0}mm，当前{depth}mm）");
                    }
                }

                // 检查宽高比
                double widthDepthRatio = (double)width / depth;
                if (widthDepthRatio < 0.25 || widthDepthRatio > 0.5)
                {
                    return (false, $"梁宽高比不合理（建议1:2~1:4，当前1:{depth / width:F1}）");
                }

                return (true, "梁截面尺寸符合规范");
            }

            /// <summary>
            /// 验证楼板厚度是否满足最小要求
            /// </summary>
            public static (bool isValid, string message) ValidateSlabThickness(int thickness, string slabType = "结构楼板")
            {
                if (!MinimumSlabThickness.ContainsKey(slabType))
                    slabType = "结构楼板";

                int minThickness = MinimumSlabThickness[slabType];
                if (thickness < minThickness)
                {
                    return (false, $"板厚不满足最小要求（{slabType}≥{minThickness}mm，当前{thickness}mm）");
                }

                return (true, "板厚符合规范");
            }
        }

        #endregion

        #region 材料用量估算

        /// <summary>
        /// 材料用量估算参数
        /// </summary>
        public static class MaterialQuantityEstimation
        {
            /// <summary>
            /// 混凝土构件含钢量（kg/m³）
            /// </summary>
            public static readonly Dictionary<string, (double min, double max, double typical)> SteelContentPerCubicMeter = new()
            {
                ["框架柱"] = (80, 120, 90),
                ["框架梁"] = (75, 110, 85),
                ["楼板"] = (45, 65, 55),
                ["剪力墙"] = (60, 90, 70),
                ["基础"] = (50, 80, 60)
            };

            /// <summary>
            /// 砌体材料用量（块/㎡，标准砖240×115×53mm）
            /// </summary>
            public static readonly Dictionary<string, int> BrickQuantityPerSquareMeter = new()
            {
                ["半砖墙（120mm）"] = 64,
                ["一砖墙（240mm）"] = 128,
                ["一砖半墙（370mm）"] = 192,
                ["加气混凝土砌块（600×200×200）"] = 8
            };

            /// <summary>
            /// 砂浆用量（m³砂浆/m³砌体）
            /// </summary>
            public static readonly Dictionary<string, double> MortarConsumption = new()
            {
                ["标准砖砌体"] = 0.25,
                ["加气混凝土砌块"] = 0.02
            };

            /// <summary>
            /// 估算构件的钢筋用量
            /// </summary>
            public static (double minSteel, double maxSteel, double typicalSteel) EstimateRebarQuantity(
                string componentType,
                double concreteVolume)
            {
                if (!SteelContentPerCubicMeter.ContainsKey(componentType))
                    componentType = "框架柱"; // 默认

                var (min, max, typical) = SteelContentPerCubicMeter[componentType];

                return (
                    concreteVolume * min / 1000.0,      // 转换为吨
                    concreteVolume * max / 1000.0,
                    concreteVolume * typical / 1000.0
                );
            }
        }

        #endregion

        #region CAD图层命名标准

        /// <summary>
        /// CAD图层命名标准（AIA + 中国建筑制图标准）
        /// </summary>
        public static class LayerNamingStandards
        {
            /// <summary>
            /// 建筑专业图层前缀
            /// </summary>
            public static readonly Dictionary<string, string> ArchitecturalLayers = new()
            {
                ["A-WALL"] = "墙体",
                ["A-DOOR"] = "门",
                ["A-WIND"] = "窗",
                ["A-GLAZ"] = "玻璃幕墙",
                ["A-FLOR"] = "楼板",
                ["A-CEIL"] = "吊顶",
                ["A-ROOF"] = "屋顶",
                ["A-STRS"] = "楼梯",
                ["A-ELEV"] = "电梯",
                ["A-FURN"] = "家具",
                ["A-ANNO"] = "标注说明",
                ["A-DIMS"] = "建筑尺寸"
            };

            /// <summary>
            /// 结构专业图层前缀
            /// </summary>
            public static readonly Dictionary<string, string> StructuralLayers = new()
            {
                ["S-GRID"] = "轴网",
                ["S-COLUMN"] = "柱",
                ["S-BEAM"] = "梁",
                ["S-SLAB"] = "板",
                ["S-WALL"] = "剪力墙",
                ["S-FOUND"] = "基础",
                ["S-BRAC"] = "支撑",
                ["S-ANNO"] = "结构标注"
            };

            /// <summary>
            /// 通用图层
            /// </summary>
            public static readonly Dictionary<string, string> CommonLayers = new()
            {
                ["0"] = "默认层",
                ["DEFPOINTS"] = "定义点（不打印）",
                ["DIM"] = "尺寸标注",
                ["TEXT"] = "文字说明",
                ["HATCH"] = "填充图案",
                ["TITLE"] = "图框标题栏"
            };

            /// <summary>
            /// 检查图层名称是否符合标准
            /// </summary>
            public static (bool isStandard, string category, string description) CheckLayerName(string layerName)
            {
                layerName = layerName.ToUpper();

                // 检查建筑专业图层
                if (ArchitecturalLayers.ContainsKey(layerName))
                {
                    return (true, "建筑专业", ArchitecturalLayers[layerName]);
                }

                // 检查结构专业图层
                if (StructuralLayers.ContainsKey(layerName))
                {
                    return (true, "结构专业", StructuralLayers[layerName]);
                }

                // 检查通用图层
                if (CommonLayers.ContainsKey(layerName))
                {
                    return (true, "通用图层", CommonLayers[layerName]);
                }

                // 非标准图层
                return (false, "非标准", "未按规范命名");
            }

            /// <summary>
            /// 建议的图层名称（如果输入了非标准名称）
            /// </summary>
            public static string SuggestLayerName(string currentName)
            {
                currentName = currentName.ToLower();

                // 简单的关键词匹配
                if (currentName.Contains("墙") || currentName.Contains("wall"))
                    return "A-WALL";
                if (currentName.Contains("门") || currentName.Contains("door"))
                    return "A-DOOR";
                if (currentName.Contains("窗") || currentName.Contains("wind") || currentName.Contains("window"))
                    return "A-WIND";
                if (currentName.Contains("柱") || currentName.Contains("column"))
                    return "S-COLUMN";
                if (currentName.Contains("梁") || currentName.Contains("beam"))
                    return "S-BEAM";
                if (currentName.Contains("板") || currentName.Contains("slab"))
                    return "S-SLAB";
                if (currentName.Contains("标注") || currentName.Contains("anno"))
                    return "A-ANNO";
                if (currentName.Contains("尺寸") || currentName.Contains("dim"))
                    return "DIM";

                return "A-MISC"; // 建筑杂项
            }
        }

        #endregion

        #region 构件命名规范

        /// <summary>
        /// 构件命名规范
        /// </summary>
        public static class ComponentNamingStandards
        {
            /// <summary>
            /// 构件编号前缀
            /// </summary>
            public static readonly Dictionary<string, string> ComponentPrefixes = new()
            {
                ["KZ"] = "框架柱",
                ["KL"] = "框架梁",
                ["LL"] = "连梁",
                ["GL"] = "过梁",
                ["JL"] = "基础梁",
                ["Q"] = "墙体",
                ["JQ"] = "剪力墙",
                ["B"] = "板",
                ["LB"] = "楼板",
                ["WB"] = "屋面板",
                ["YTB"] = "阳台板",
                ["JC"] = "基础",
                ["DJ"] = "独立基础",
                ["TJ"] = "条形基础",
                ["ZHJ"] = "桩基础"
            };

            /// <summary>
            /// 验证构件编号格式
            /// </summary>
            /// <param name="componentName">构件名称，如 "KZ-1"</param>
            /// <returns>是否符合规范</returns>
            public static (bool isValid, string prefix, int number) ParseComponentName(string componentName)
            {
                // 典型格式：KZ-1, KL-2, Q-3
                var parts = componentName.Split('-');
                if (parts.Length == 2)
                {
                    string prefix = parts[0].ToUpper();
                    if (ComponentPrefixes.ContainsKey(prefix) && int.TryParse(parts[1], out int number))
                    {
                        return (true, prefix, number);
                    }
                }

                return (false, "", 0);
            }

            /// <summary>
            /// 生成标准构件编号
            /// </summary>
            public static string GenerateComponentName(string componentType, int serialNumber)
            {
                // 查找对应的前缀
                var prefix = ComponentPrefixes.FirstOrDefault(kvp => kvp.Value == componentType).Key;
                if (string.IsNullOrEmpty(prefix))
                    prefix = "UN"; // Unknown

                return $"{prefix}-{serialNumber}";
            }
        }

        #endregion

        #region 质量检查规则

        /// <summary>
        /// 质量检查规则
        /// </summary>
        public static class QualityCheckRules
        {
            /// <summary>
            /// 问题严重等级
            /// </summary>
            public enum Severity
            {
                /// <summary>严重错误，必须修改</summary>
                Critical,
                /// <summary>重要警告，建议修改</summary>
                Warning,
                /// <summary>建议优化</summary>
                Info
            }

            /// <summary>
            /// 质量检查项
            /// </summary>
            public class CheckRule
            {
                public string RuleId { get; set; } = "";
                public string RuleName { get; set; } = "";
                public string Description { get; set; } = "";
                public Severity SeverityLevel { get; set; }
                public string Standard { get; set; } = ""; // 依据的规范
            }

            /// <summary>
            /// 所有质量检查规则
            /// </summary>
            public static readonly List<CheckRule> AllRules = new()
            {
                new CheckRule
                {
                    RuleId = "R001",
                    RuleName = "混凝土强度等级检查",
                    Description = "检查混凝土强度等级是否满足构件最低要求",
                    SeverityLevel = Severity.Critical,
                    Standard = "GB 50010-2010"
                },
                new CheckRule
                {
                    RuleId = "R002",
                    RuleName = "柱截面尺寸检查",
                    Description = "检查柱截面是否满足最小尺寸要求（≥300mm）",
                    SeverityLevel = Severity.Critical,
                    Standard = "GB 50010-2010"
                },
                new CheckRule
                {
                    RuleId = "R003",
                    RuleName = "梁高跨比检查",
                    Description = "检查梁高是否满足高跨比要求（≥跨度/12）",
                    SeverityLevel = Severity.Critical,
                    Standard = "GB 50010-2010"
                },
                new CheckRule
                {
                    RuleId = "R004",
                    RuleName = "楼板厚度检查",
                    Description = "检查楼板厚度是否满足最小要求（≥100mm）",
                    SeverityLevel = Severity.Warning,
                    Standard = "GB 50010-2010"
                },
                new CheckRule
                {
                    RuleId = "R005",
                    RuleName = "钢筋型号检查",
                    Description = "检查钢筋型号是否符合现行标准（HRB335已废止）",
                    SeverityLevel = Severity.Warning,
                    Standard = "GB 1499-2018"
                },
                new CheckRule
                {
                    RuleId = "R006",
                    RuleName = "图层命名检查",
                    Description = "检查图层命名是否符合AIA标准",
                    SeverityLevel = Severity.Info,
                    Standard = "AIA CAD Guidelines"
                },
                new CheckRule
                {
                    RuleId = "R007",
                    RuleName = "构件编号检查",
                    Description = "检查构件编号是否规范（如KZ-1、KL-2）",
                    SeverityLevel = Severity.Info,
                    Standard = "建筑制图规范"
                },
                new CheckRule
                {
                    RuleId = "R008",
                    RuleName = "墙体厚度检查",
                    Description = "检查承重墙厚度是否≥240mm",
                    SeverityLevel = Severity.Critical,
                    Standard = "GB 50003-2011"
                },
                new CheckRule
                {
                    RuleId = "R009",
                    RuleName = "文本高度统一性检查",
                    Description = "检查同类文本高度是否统一",
                    SeverityLevel = Severity.Info,
                    Standard = "制图规范"
                },
                new CheckRule
                {
                    RuleId = "R010",
                    RuleName = "材料规格格式检查",
                    Description = "检查材料规格格式是否规范（如C30、HRB400）",
                    SeverityLevel = Severity.Info,
                    Standard = "建筑制图规范"
                }
            };
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取知识库摘要（供AI理解）
        /// </summary>
        public static string GetKnowledgeSummary()
        {
            return $@"## 建筑规范知识库摘要

### 1. 国家标准规范
- 收录规范：{NationalStandards.Count}个GB/JGJ标准
- 核心规范：GB 50010-2010（混凝土）、GB 50854-2013（审查要点）

### 2. 混凝土强度等级
- 标准等级：{ConcreteGrades.AllGrades.Count}个（C15-C80）
- 常用等级：C20（基础）、C30（框架）、C40（高层）
- 最低要求：框架柱≥C25、楼板≥C20

### 3. 钢筋型号规格
- 标准型号：{RebarGrades.AllGrades.Count}个
- 常用型号：HRB400（最常用）、HRB500（高强）
- 废止型号：HRB335（已取消，改用HRB400）

### 4. 构件尺寸规范
- 柱最小尺寸：≥300mm
- 梁最小宽度：≥200mm，梁高≥跨度/12
- 板最小厚度：≥100mm（结构板）

### 5. CAD图层标准
- 建筑专业：{LayerNamingStandards.ArchitecturalLayers.Count}个标准图层（A-前缀）
- 结构专业：{LayerNamingStandards.StructuralLayers.Count}个标准图层（S-前缀）
- 符合：AIA CAD Layer Guidelines

### 6. 构件命名规范
- 标准前缀：{ComponentNamingStandards.ComponentPrefixes.Count}个（KZ=框架柱、KL=框架梁等）
- 命名格式：前缀-编号（如KZ-1、KL-2）

### 7. 质量检查规则
- 检查项：{QualityCheckRules.AllRules.Count}条规则
- 严重等级：Critical（必须修改）、Warning（建议修改）、Info（优化建议）
";
        }

        #endregion
    }
}
