using System.Collections.Generic;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 工程建筑翻译配置
    /// 基于阿里云百炼qwen-mt-flash模型的最佳实践
    /// </summary>
    public static class EngineeringTranslationConfig
    {
        /// <summary>
        /// qwen-mt-flash模型的Token限制
        /// </summary>
        public const int MaxInputTokens = 8192;
        public const int MaxOutputTokens = 8192;

        /// <summary>
        /// 估算：平均每个中文字符约1.5个token，英文单词约1个token
        /// 为安全起见，使用更保守的估算：每字符2个token
        /// 因此单次翻译最多4000字符（8192 / 2）
        /// </summary>
        public const int MaxCharsPerBatch = 4000;

        /// <summary>
        /// 工程建筑领域提示词（英文，根据阿里云文档要求）
        ///
        /// 核心要求：
        /// 1. 明确这是工程建筑领域的图纸
        /// 2. 保留图号、编号、代号等标识符
        /// 3. 使用专业的工程术语
        /// 4. 保持专业性和准确性
        /// </summary>
        public static readonly string DomainPrompt =
            "This text is from engineering and architectural construction drawings. " +
            "It contains technical specifications, building materials, construction methods, structural engineering, and architectural design terminology. " +
            "IMPORTANT RULES: " +
            "1. DO NOT translate drawing numbers, part numbers, codes (e.g., 'No.', 'No.1', 'A-101', '#1', '编号', '图号'). " +
            "2. DO NOT translate measurement units (e.g., 'mm', 'cm', 'm', 'kg', 'MPa'). " +
            "3. DO NOT translate alphanumeric identifiers (e.g., 'B1', 'C-3', '1F', '2F'). " +
            "4. DO translate descriptive text, material names, and construction instructions using professional engineering terminology. " +
            "5. Maintain technical accuracy and use industry-standard translations. " +
            "Translate into professional construction engineering domain style.";

        /// <summary>
        /// 不应翻译的术语/模式规则
        ///
        /// 这些模式会被直接保留，不进行翻译：
        /// - 图号/编号前缀（No., #, 编号, 图号等）
        /// - 纯数字和字母组合的代号
        /// - 单位符号
        /// - 楼层标识（1F, 2F, B1等）
        /// </summary>
        public static readonly List<TermRule> PreserveTerms = new List<TermRule>
        {
            // 图号/编号前缀
            new TermRule { Source = "No.", Target = "No.", Pattern = @"No\.\s*\d+", Description = "图号前缀" },
            new TermRule { Source = "NO.", Target = "NO.", Pattern = @"NO\.\s*\d+", Description = "图号前缀大写" },
            new TermRule { Source = "#", Target = "#", Pattern = @"#\d+", Description = "井号编号" },
            new TermRule { Source = "编号", Target = "编号", Pattern = @"编号[:：]?\s*[\w\d-]+", Description = "编号标识" },
            new TermRule { Source = "图号", Target = "图号", Pattern = @"图号[:：]?\s*[\w\d-]+", Description = "图号标识" },

            // 楼层标识
            new TermRule { Source = "1F", Target = "1F", Pattern = @"\d+F", Description = "楼层标识" },
            new TermRule { Source = "B1", Target = "B1", Pattern = @"B\d+", Description = "地下层标识" },
            new TermRule { Source = "RF", Target = "RF", Pattern = @"RF", Description = "屋顶层" },

            // 轴线标识
            new TermRule { Source = "轴", Target = "Axis", Pattern = @"[A-Z①-⑳]\s*轴", Description = "轴线" },

            // 单位符号（常用工程单位）
            new TermRule { Source = "mm", Target = "mm", Description = "毫米" },
            new TermRule { Source = "cm", Target = "cm", Description = "厘米" },
            new TermRule { Source = "m", Target = "m", Description = "米" },
            new TermRule { Source = "m²", Target = "m²", Description = "平方米" },
            new TermRule { Source = "m³", Target = "m³", Description = "立方米" },
            new TermRule { Source = "kg", Target = "kg", Description = "千克" },
            new TermRule { Source = "t", Target = "t", Description = "吨" },
            new TermRule { Source = "MPa", Target = "MPa", Description = "兆帕" },
            new TermRule { Source = "kN", Target = "kN", Description = "千牛" },
            new TermRule { Source = "°C", Target = "°C", Description = "摄氏度" },
            new TermRule { Source = "‰", Target = "‰", Description = "千分号" },
            new TermRule { Source = "%", Target = "%", Description = "百分号" },
        };

        /// <summary>
        /// 工程建筑专业术语对照表
        ///
        /// 确保专业术语的准确翻译
        /// </summary>
        public static readonly List<ProfessionalTerm> ProfessionalTerms = new List<ProfessionalTerm>
        {
            // 结构工程
            new ProfessionalTerm { Chinese = "钢筋混凝土", English = "reinforced concrete" },
            new ProfessionalTerm { Chinese = "承重墙", English = "load-bearing wall" },
            new ProfessionalTerm { Chinese = "剪力墙", English = "shear wall" },
            new ProfessionalTerm { Chinese = "框架结构", English = "frame structure" },
            new ProfessionalTerm { Chinese = "地基", English = "foundation" },
            new ProfessionalTerm { Chinese = "基础", English = "foundation" },
            new ProfessionalTerm { Chinese = "桩基", English = "pile foundation" },
            new ProfessionalTerm { Chinese = "梁", English = "beam" },
            new ProfessionalTerm { Chinese = "柱", English = "column" },
            new ProfessionalTerm { Chinese = "板", English = "slab" },
            new ProfessionalTerm { Chinese = "楼板", English = "floor slab" },
            new ProfessionalTerm { Chinese = "屋面", English = "roof" },
            new ProfessionalTerm { Chinese = "抗震", English = "seismic resistance" },
            new ProfessionalTerm { Chinese = "抗震设计", English = "seismic design" },

            // 建筑材料
            new ProfessionalTerm { Chinese = "混凝土", English = "concrete" },
            new ProfessionalTerm { Chinese = "钢筋", English = "steel reinforcement" },
            new ProfessionalTerm { Chinese = "砖", English = "brick" },
            new ProfessionalTerm { Chinese = "砌块", English = "block" },
            new ProfessionalTerm { Chinese = "砂浆", English = "mortar" },
            new ProfessionalTerm { Chinese = "防水层", English = "waterproof layer" },
            new ProfessionalTerm { Chinese = "保温层", English = "thermal insulation layer" },
            new ProfessionalTerm { Chinese = "找平层", English = "leveling layer" },

            // 建筑构造
            new ProfessionalTerm { Chinese = "墙体", English = "wall" },
            new ProfessionalTerm { Chinese = "隔墙", English = "partition wall" },
            new ProfessionalTerm { Chinese = "外墙", English = "exterior wall" },
            new ProfessionalTerm { Chinese = "内墙", English = "interior wall" },
            new ProfessionalTerm { Chinese = "门窗", English = "doors and windows" },
            new ProfessionalTerm { Chinese = "楼梯", English = "staircase" },
            new ProfessionalTerm { Chinese = "电梯井", English = "elevator shaft" },
            new ProfessionalTerm { Chinese = "管井", English = "service shaft" },

            // 施工工艺
            new ProfessionalTerm { Chinese = "浇筑", English = "pouring" },
            new ProfessionalTerm { Chinese = "振捣", English = "vibration" },
            new ProfessionalTerm { Chinese = "养护", English = "curing" },
            new ProfessionalTerm { Chinese = "回填", English = "backfill" },
            new ProfessionalTerm { Chinese = "夯实", English = "compaction" },
            new ProfessionalTerm { Chinese = "支模", English = "formwork" },
            new ProfessionalTerm { Chinese = "拆模", English = "form removal" },

            // 图纸常用词
            new ProfessionalTerm { Chinese = "平面图", English = "floor plan" },
            new ProfessionalTerm { Chinese = "立面图", English = "elevation" },
            new ProfessionalTerm { Chinese = "剖面图", English = "section" },
            new ProfessionalTerm { Chinese = "详图", English = "detail drawing" },
            new ProfessionalTerm { Chinese = "节点详图", English = "node detail" },
            new ProfessionalTerm { Chinese = "轴线", English = "axis" },
            new ProfessionalTerm { Chinese = "标高", English = "elevation level" },
            new ProfessionalTerm { Chinese = "尺寸", English = "dimension" },
            new ProfessionalTerm { Chinese = "比例", English = "scale" },
            new ProfessionalTerm { Chinese = "说明", English = "notes" },
            new ProfessionalTerm { Chinese = "备注", English = "remarks" },
        };

        /// <summary>
        /// 转换为阿里云百炼API所需的terms格式
        /// </summary>
        public static List<object> GetApiTerms(string sourceLang, string targetLang)
        {
            var terms = new List<object>();

            // 如果是中译英
            if (sourceLang.Contains("Chinese") && targetLang.Contains("English"))
            {
                foreach (var term in ProfessionalTerms)
                {
                    terms.Add(new { source = term.Chinese, target = term.English });
                }
            }
            // 如果是英译中
            else if (sourceLang.Contains("English") && targetLang.Contains("Chinese"))
            {
                foreach (var term in ProfessionalTerms)
                {
                    terms.Add(new { source = term.English, target = term.Chinese });
                }
            }

            return terms;
        }
    }

    /// <summary>
    /// 术语保留规则
    /// </summary>
    public class TermRule
    {
        public string Source { get; set; } = "";
        public string Target { get; set; } = "";
        public string? Pattern { get; set; }  // 正则表达式模式（可选）
        public string? Description { get; set; }
    }

    /// <summary>
    /// 专业术语
    /// </summary>
    public class ProfessionalTerm
    {
        public string Chinese { get; set; } = "";
        public string English { get; set; } = "";
    }
}
