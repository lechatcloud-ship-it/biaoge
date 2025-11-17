using System.Collections.Generic;
using System.Linq;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 工程建筑翻译配置
    /// 基于阿里云百炼qwen-flash模型的最佳实践
    /// </summary>
    public static class EngineeringTranslationConfig
    {
        /// <summary>
        /// Qwen-MT-Flash专用翻译模型的Token限制（2025最新官方规格）
        /// </summary>
        public const int MaxInputTokens = 8192;   // qwen-mt-flash输入限制
        public const int MaxOutputTokens = 8192;  // qwen-mt-flash输出限制

        /// <summary>
        /// 单次翻译的最大字符数
        ///
        /// ✅ P0修复：修正为qwen-mt-flash实际限制（8K上下文，NOT 1M）
        /// qwen-mt-flash性能参数（官方文档）：
        /// - 最大输入长度: 8192 tokens
        /// - 最大输出长度: 8192 tokens
        /// - 总上下文: 16384 tokens
        /// - RPM: 1000 QPS（单条并发）
        /// - 成功率: 99.8%
        ///
        /// 批次大小计算（2025-11-17优化后）：
        /// - DomainPrompt: ~50 tokens ("Construction and Engineering Documentation")
        /// - Terms术语表: ~80 tokens (20条核心术语，已优化)
        /// - TM翻译记忆: ~120 tokens (5个核心示例，已优化)
        /// - 系统参数总计: ~250 tokens（大幅减少！）
        /// - 实际可用输入: 8192 - 250 = 7942 tokens
        ///
        /// Token估算规则：
        /// - 中文字符: 1字符 ≈ 1.5 tokens (包括标点)
        /// - 英文/数字: 1字符 ≈ 0.25 tokens
        /// - 混合文本: 保守估计 1字符 ≈ 1.0 token
        /// - 安全裕度: 留1000 tokens给API响应和误差
        ///
        /// 计算：(7942 - 1000) / 1.0 = 6942字符
        /// 优化设置: 6500字符（留442 tokens安全余量）
        ///
        /// ⚠️ 注意：1M上下文是qwen-flash/qwen-plus通用对话模型的能力，NOT qwen-mt-flash
        /// </summary>
        public const int MaxCharsPerBatch = 6500;  // ✅ 优化后：从3500提升到6500

        /// <summary>
        /// 工程建筑领域提示词（英文，符合阿里云百炼Prompt Engineering最佳实践）
        ///
        /// ✅ 2025-11-15升级：基于阿里云百炼官方Prompt Engineering最佳实践
        /// 框架结构：背景(Background) + 目的(Purpose) + 风格(Style) + 受众(Audience) + 输出(Output)
        /// 参考：https://help.aliyun.com/zh/model-studio/prompt-engineering-guide
        ///
        /// 核心优化：
        /// 1. 【明确背景】专业建筑工程图纸翻译，AutoCAD/BIM环境
        /// 2. 【清晰目的】国际工程标准合规，专业术语精准，保留技术标识
        /// 3. 【专业风格】工程技术文档风格，简洁准确，避免口语化
        /// 4. 【目标受众】建筑师、工程师、施工人员等专业读者
        /// 5. 【输出规范】保持原文格式，技术代码/编号不变，术语标准化
        /// 6. 【Few-shot示例】提供高质量翻译示例引导模型
        /// </summary>
        /// <summary>
        /// ✅ P0紧急修复：根据阿里云百炼官方文档，domains应该是简短的领域提示
        /// 官方文档：domains (optional): Domain hint string (English only) for contextual guidance
        /// 错误：之前使用了完整的系统提示词（包含 # Background、# Purpose等），导致模型回显
        /// 正确：简短的领域描述，不超过一句话
        /// </summary>
        public static readonly string DomainPrompt = "Construction and Engineering Documentation";

        /// <summary>
        /// 不应翻译的术语/模式规则
        ///
        /// 这些模式会被直接保留，不进行翻译：
        /// - 图号/编号前缀（No., #, 编号, 图号等）
        /// - 中国国家标准代号（GB, JGJ, CJJ等）
        /// - 材料强度等级（C30, Q235等）
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
            new TermRule { Source = "DWG No.", Target = "DWG No.", Pattern = @"DWG\s+No\.\s*[\w\d-]+", Description = "图纸编号" },

            // 中国国家标准代号 (Chinese National Standards)
            new TermRule { Source = "GB", Target = "GB", Pattern = @"GB[/\s]*[TZ]?\s*\d+", Description = "国家标准" },
            new TermRule { Source = "JGJ", Target = "JGJ", Pattern = @"JGJ[/\s]*[TZ]?\s*\d+", Description = "建筑工程行业标准" },
            new TermRule { Source = "CJJ", Target = "CJJ", Pattern = @"CJJ[/\s]*[TZ]?\s*\d+", Description = "城市建设行业标准" },
            new TermRule { Source = "GBJ", Target = "GBJ", Pattern = @"GBJ\s*\d+", Description = "旧国家标准" },

            // 混凝土强度等级 (Concrete Strength Grades)
            new TermRule { Source = "C30", Target = "C30", Pattern = @"C\d{1,2}\.?\d?", Description = "混凝土强度等级" },

            // 钢材牌号 (Steel Grades)
            new TermRule { Source = "Q235", Target = "Q235", Pattern = @"Q\d{3}[A-Z]?", Description = "钢材牌号" },
            new TermRule { Source = "HPB300", Target = "HPB300", Pattern = @"HPB\d{3}", Description = "钢筋牌号" },
            new TermRule { Source = "HRB400", Target = "HRB400", Pattern = @"HRB\d{3}[A-Z]?", Description = "钢筋牌号" },
            new TermRule { Source = "HRB500", Target = "HRB500", Pattern = @"HRB\d{3}[A-Z]?", Description = "钢筋牌号" },

            // 楼层标识
            new TermRule { Source = "1F", Target = "1F", Pattern = @"\d+F", Description = "楼层标识" },
            new TermRule { Source = "B1", Target = "B1", Pattern = @"B\d+", Description = "地下层标识" },
            new TermRule { Source = "RF", Target = "RF", Pattern = @"RF", Description = "屋顶层" },
            new TermRule { Source = "LF", Target = "LF", Pattern = @"LF", Description = "夹层" },

            // 轴线标识
            new TermRule { Source = "轴", Target = "Axis", Pattern = @"[A-Z①-⑳]\s*轴", Description = "轴线" },

            // 长度单位
            new TermRule { Source = "mm", Target = "mm", Description = "毫米" },
            new TermRule { Source = "cm", Target = "cm", Description = "厘米" },
            new TermRule { Source = "m", Target = "m", Description = "米" },
            new TermRule { Source = "km", Target = "km", Description = "千米" },

            // 面积/体积单位
            new TermRule { Source = "m²", Target = "m²", Description = "平方米" },
            new TermRule { Source = "m³", Target = "m³", Description = "立方米" },
            new TermRule { Source = "L", Target = "L", Description = "升" },

            // 力学单位
            new TermRule { Source = "MPa", Target = "MPa", Description = "兆帕" },
            new TermRule { Source = "kN", Target = "kN", Description = "千牛" },
            new TermRule { Source = "N", Target = "N", Description = "牛" },
            new TermRule { Source = "kN/m", Target = "kN/m", Description = "千牛每米" },
            new TermRule { Source = "kN·m", Target = "kN·m", Description = "千牛米" },

            // 质量单位
            new TermRule { Source = "kg", Target = "kg", Description = "千克" },
            new TermRule { Source = "t", Target = "t", Description = "吨" },
            new TermRule { Source = "g", Target = "g", Description = "克" },

            // 温度单位
            new TermRule { Source = "°C", Target = "°C", Description = "摄氏度" },
            new TermRule { Source = "℃", Target = "℃", Description = "摄氏度" },
            new TermRule { Source = "K", Target = "K", Description = "开尔文" },

            // 电气/机电单位
            new TermRule { Source = "kW", Target = "kW", Description = "千瓦" },
            new TermRule { Source = "kVA", Target = "kVA", Description = "千伏安" },
            new TermRule { Source = "kV", Target = "kV", Description = "千伏" },
            new TermRule { Source = "V", Target = "V", Description = "伏特" },
            new TermRule { Source = "A", Target = "A", Description = "安培" },
            new TermRule { Source = "Hz", Target = "Hz", Description = "赫兹" },
            new TermRule { Source = "W", Target = "W", Description = "瓦" },

            // 暖通/流体单位
            new TermRule { Source = "Pa", Target = "Pa", Description = "帕" },
            new TermRule { Source = "kPa", Target = "kPa", Description = "千帕" },
            new TermRule { Source = "m³/h", Target = "m³/h", Description = "立方米每小时" },
            new TermRule { Source = "L/s", Target = "L/s", Description = "升每秒" },

            // 百分比/比例
            new TermRule { Source = "‰", Target = "‰", Description = "千分号" },
            new TermRule { Source = "%", Target = "%", Description = "百分号" },
            new TermRule { Source = "φ", Target = "φ", Description = "直径符号" },
            new TermRule { Source = "Ø", Target = "Ø", Description = "直径符号" },
        };

        /// <summary>
        /// 工程建筑专业术语对照表
        ///
        /// 确保专业术语的准确翻译
        /// 涵盖：结构、建筑、MEP、消防、装修、施工工艺、图纸等各专业
        /// </summary>
        public static readonly List<ProfessionalTerm> ProfessionalTerms = new List<ProfessionalTerm>
        {
            // ========== 结构工程 (Structural Engineering) ==========
            // 结构体系
            new ProfessionalTerm { Chinese = "钢筋混凝土", English = "reinforced concrete" },
            new ProfessionalTerm { Chinese = "预应力混凝土", English = "prestressed concrete" },
            new ProfessionalTerm { Chinese = "承重墙", English = "load-bearing wall" },
            new ProfessionalTerm { Chinese = "剪力墙", English = "shear wall" },
            new ProfessionalTerm { Chinese = "框架结构", English = "frame structure" },
            new ProfessionalTerm { Chinese = "框剪结构", English = "frame-shear wall structure" },
            new ProfessionalTerm { Chinese = "钢结构", English = "steel structure" },
            new ProfessionalTerm { Chinese = "混合结构", English = "hybrid structure" },
            new ProfessionalTerm { Chinese = "砌体结构", English = "masonry structure" },

            // 基础
            new ProfessionalTerm { Chinese = "地基", English = "foundation" },
            new ProfessionalTerm { Chinese = "基础", English = "foundation" },
            new ProfessionalTerm { Chinese = "桩基", English = "pile foundation" },
            new ProfessionalTerm { Chinese = "筏板基础", English = "raft foundation" },
            new ProfessionalTerm { Chinese = "独立基础", English = "isolated footing" },
            new ProfessionalTerm { Chinese = "条形基础", English = "strip footing" },
            new ProfessionalTerm { Chinese = "承台", English = "pile cap" },
            new ProfessionalTerm { Chinese = "基坑", English = "excavation" },
            new ProfessionalTerm { Chinese = "地下室", English = "basement" },

            // 结构构件
            new ProfessionalTerm { Chinese = "梁", English = "beam" },
            new ProfessionalTerm { Chinese = "主梁", English = "main beam" },
            new ProfessionalTerm { Chinese = "次梁", English = "secondary beam" },
            new ProfessionalTerm { Chinese = "连梁", English = "coupling beam" },
            new ProfessionalTerm { Chinese = "圈梁", English = "ring beam" },
            new ProfessionalTerm { Chinese = "柱", English = "column" },
            new ProfessionalTerm { Chinese = "框架柱", English = "frame column" },
            new ProfessionalTerm { Chinese = "板", English = "slab" },
            new ProfessionalTerm { Chinese = "楼板", English = "floor slab" },
            new ProfessionalTerm { Chinese = "屋面板", English = "roof slab" },
            new ProfessionalTerm { Chinese = "悬挑板", English = "cantilever slab" },
            new ProfessionalTerm { Chinese = "女儿墙", English = "parapet" },
            new ProfessionalTerm { Chinese = "构造柱", English = "structural column" },
            new ProfessionalTerm { Chinese = "系梁", English = "tie beam" },

            // 抗震设计
            new ProfessionalTerm { Chinese = "抗震", English = "seismic resistance" },
            new ProfessionalTerm { Chinese = "抗震设计", English = "seismic design" },
            new ProfessionalTerm { Chinese = "抗震等级", English = "seismic grade" },
            new ProfessionalTerm { Chinese = "抗震烈度", English = "seismic intensity" },
            new ProfessionalTerm { Chinese = "延性", English = "ductility" },
            new ProfessionalTerm { Chinese = "剪力", English = "shear force" },
            new ProfessionalTerm { Chinese = "弯矩", English = "bending moment" },
            new ProfessionalTerm { Chinese = "轴力", English = "axial force" },

            // ========== 建筑材料 (Building Materials) ==========
            // 混凝土
            new ProfessionalTerm { Chinese = "混凝土", English = "concrete" },
            new ProfessionalTerm { Chinese = "商品混凝土", English = "ready-mix concrete" },
            new ProfessionalTerm { Chinese = "细石混凝土", English = "fine aggregate concrete" },
            new ProfessionalTerm { Chinese = "水泥", English = "cement" },
            new ProfessionalTerm { Chinese = "外加剂", English = "admixture" },

            // 钢材
            new ProfessionalTerm { Chinese = "钢筋", English = "steel reinforcement" },
            new ProfessionalTerm { Chinese = "受力钢筋", English = "main reinforcement" },
            new ProfessionalTerm { Chinese = "箍筋", English = "stirrup" },
            new ProfessionalTerm { Chinese = "分布筋", English = "distribution bar" },
            new ProfessionalTerm { Chinese = "构造钢筋", English = "structural steel" },
            new ProfessionalTerm { Chinese = "型钢", English = "section steel" },
            new ProfessionalTerm { Chinese = "钢板", English = "steel plate" },

            // 砌体材料
            new ProfessionalTerm { Chinese = "砖", English = "brick" },
            new ProfessionalTerm { Chinese = "多孔砖", English = "perforated brick" },
            new ProfessionalTerm { Chinese = "空心砖", English = "hollow brick" },
            new ProfessionalTerm { Chinese = "砌块", English = "block" },
            new ProfessionalTerm { Chinese = "加气混凝土砌块", English = "AAC block" },
            new ProfessionalTerm { Chinese = "砂浆", English = "mortar" },
            new ProfessionalTerm { Chinese = "水泥砂浆", English = "cement mortar" },
            new ProfessionalTerm { Chinese = "混合砂浆", English = "mixed mortar" },

            // 防水保温
            new ProfessionalTerm { Chinese = "防水层", English = "waterproof layer" },
            new ProfessionalTerm { Chinese = "防水卷材", English = "waterproof membrane" },
            new ProfessionalTerm { Chinese = "防水涂料", English = "waterproof coating" },
            new ProfessionalTerm { Chinese = "保温层", English = "thermal insulation layer" },
            new ProfessionalTerm { Chinese = "保温材料", English = "thermal insulation material" },
            new ProfessionalTerm { Chinese = "岩棉", English = "rock wool" },
            new ProfessionalTerm { Chinese = "挤塑板", English = "XPS board" },
            new ProfessionalTerm { Chinese = "聚苯板", English = "EPS board" },
            new ProfessionalTerm { Chinese = "找平层", English = "leveling layer" },
            new ProfessionalTerm { Chinese = "找坡层", English = "slope layer" },

            // ========== 建筑构造 (Building Construction) ==========
            // 墙体
            new ProfessionalTerm { Chinese = "墙体", English = "wall" },
            new ProfessionalTerm { Chinese = "隔墙", English = "partition wall" },
            new ProfessionalTerm { Chinese = "外墙", English = "exterior wall" },
            new ProfessionalTerm { Chinese = "内墙", English = "interior wall" },
            new ProfessionalTerm { Chinese = "幕墙", English = "curtain wall" },
            new ProfessionalTerm { Chinese = "玻璃幕墙", English = "glass curtain wall" },
            new ProfessionalTerm { Chinese = "石材幕墙", English = "stone curtain wall" },
            new ProfessionalTerm { Chinese = "填充墙", English = "infill wall" },

            // 门窗
            new ProfessionalTerm { Chinese = "门窗", English = "doors and windows" },
            new ProfessionalTerm { Chinese = "窗", English = "window" },
            new ProfessionalTerm { Chinese = "门", English = "door" },
            new ProfessionalTerm { Chinese = "防火门", English = "fire door" },
            new ProfessionalTerm { Chinese = "防盗门", English = "security door" },
            new ProfessionalTerm { Chinese = "铝合金门窗", English = "aluminum window" },
            new ProfessionalTerm { Chinese = "塑钢门窗", English = "PVC window" },
            new ProfessionalTerm { Chinese = "推拉窗", English = "sliding window" },
            new ProfessionalTerm { Chinese = "平开窗", English = "casement window" },

            // 垂直交通
            new ProfessionalTerm { Chinese = "楼梯", English = "staircase" },
            new ProfessionalTerm { Chinese = "楼梯间", English = "stairwell" },
            new ProfessionalTerm { Chinese = "电梯", English = "elevator" },
            new ProfessionalTerm { Chinese = "电梯井", English = "elevator shaft" },
            new ProfessionalTerm { Chinese = "扶梯", English = "escalator" },
            new ProfessionalTerm { Chinese = "坡道", English = "ramp" },
            new ProfessionalTerm { Chinese = "踏步", English = "tread" },
            new ProfessionalTerm { Chinese = "踢面", English = "riser" },
            new ProfessionalTerm { Chinese = "休息平台", English = "landing" },

            // 其他构造
            new ProfessionalTerm { Chinese = "管井", English = "service shaft" },
            new ProfessionalTerm { Chinese = "风井", English = "air shaft" },
            new ProfessionalTerm { Chinese = "烟道", English = "flue" },
            new ProfessionalTerm { Chinese = "屋面", English = "roof" },
            new ProfessionalTerm { Chinese = "平屋面", English = "flat roof" },
            new ProfessionalTerm { Chinese = "坡屋面", English = "pitched roof" },
            new ProfessionalTerm { Chinese = "屋顶设备间", English = "doghouse" },  // ✅ 建筑术语：屋顶上的小型设备间/机房/楼梯间等突出结构
            new ProfessionalTerm { Chinese = "屋顶设备间", English = "dog house" }, // ✅ 同上，支持空格写法
            new ProfessionalTerm { Chinese = "屋顶机房", English = "roof penthouse" },
            new ProfessionalTerm { Chinese = "屋顶机房", English = "penthouse" },
            new ProfessionalTerm { Chinese = "天沟", English = "gutter" },
            new ProfessionalTerm { Chinese = "雨水管", English = "downspout" },
            new ProfessionalTerm { Chinese = "阳台", English = "balcony" },
            new ProfessionalTerm { Chinese = "飘窗", English = "bay window" },
            new ProfessionalTerm { Chinese = "雨篷", English = "canopy" },
            new ProfessionalTerm { Chinese = "吊顶", English = "suspended ceiling" },
            new ProfessionalTerm { Chinese = "地面", English = "floor" },

            // ========== 机电系统 (MEP Systems) ==========
            // 给排水 (Plumbing)
            new ProfessionalTerm { Chinese = "给水", English = "water supply" },
            new ProfessionalTerm { Chinese = "排水", English = "drainage" },
            new ProfessionalTerm { Chinese = "污水", English = "sewage" },
            new ProfessionalTerm { Chinese = "雨水", English = "rainwater" },
            new ProfessionalTerm { Chinese = "给水管", English = "water supply pipe" },
            new ProfessionalTerm { Chinese = "排水管", English = "drain pipe" },
            new ProfessionalTerm { Chinese = "水泵", English = "pump" },
            new ProfessionalTerm { Chinese = "水箱", English = "water tank" },
            new ProfessionalTerm { Chinese = "阀门", English = "valve" },
            new ProfessionalTerm { Chinese = "卫生间", English = "bathroom" },
            new ProfessionalTerm { Chinese = "地漏", English = "floor drain" },

            // 暖通空调 (HVAC)
            new ProfessionalTerm { Chinese = "暖通", English = "HVAC" },
            new ProfessionalTerm { Chinese = "空调", English = "air conditioning" },
            new ProfessionalTerm { Chinese = "通风", English = "ventilation" },
            new ProfessionalTerm { Chinese = "新风系统", English = "fresh air system" },
            new ProfessionalTerm { Chinese = "排烟系统", English = "smoke exhaust system" },
            new ProfessionalTerm { Chinese = "风管", English = "air duct" },
            new ProfessionalTerm { Chinese = "风口", English = "air outlet" },
            new ProfessionalTerm { Chinese = "冷却塔", English = "cooling tower" },
            new ProfessionalTerm { Chinese = "冷水机组", English = "chiller" },
            new ProfessionalTerm { Chinese = "风机", English = "fan" },

            // 电气 (Electrical)
            new ProfessionalTerm { Chinese = "配电", English = "power distribution" },
            new ProfessionalTerm { Chinese = "配电箱", English = "distribution box" },
            new ProfessionalTerm { Chinese = "开关", English = "switch" },
            new ProfessionalTerm { Chinese = "插座", English = "socket" },
            new ProfessionalTerm { Chinese = "照明", English = "lighting" },
            new ProfessionalTerm { Chinese = "灯具", English = "luminaire" },
            new ProfessionalTerm { Chinese = "电缆", English = "cable" },
            new ProfessionalTerm { Chinese = "线缆", English = "wire" },
            new ProfessionalTerm { Chinese = "桥架", English = "cable tray" },
            new ProfessionalTerm { Chinese = "配电柜", English = "switchgear" },
            new ProfessionalTerm { Chinese = "变压器", English = "transformer" },
            new ProfessionalTerm { Chinese = "发电机", English = "generator" },
            new ProfessionalTerm { Chinese = "弱电", English = "low voltage" },
            new ProfessionalTerm { Chinese = "强电", English = "power" },

            // ========== 消防系统 (Fire Protection) ==========
            new ProfessionalTerm { Chinese = "消防", English = "fire protection" },
            new ProfessionalTerm { Chinese = "消火栓", English = "fire hydrant" },
            new ProfessionalTerm { Chinese = "喷淋系统", English = "sprinkler system" },
            new ProfessionalTerm { Chinese = "喷头", English = "sprinkler head" },
            new ProfessionalTerm { Chinese = "消防水泵", English = "fire pump" },
            new ProfessionalTerm { Chinese = "消防水池", English = "fire water tank" },
            new ProfessionalTerm { Chinese = "火灾报警", English = "fire alarm" },
            new ProfessionalTerm { Chinese = "烟感探测器", English = "smoke detector" },
            new ProfessionalTerm { Chinese = "防火卷帘", English = "fire shutter" },
            new ProfessionalTerm { Chinese = "防火分区", English = "fire compartment" },
            new ProfessionalTerm { Chinese = "疏散通道", English = "evacuation route" },
            new ProfessionalTerm { Chinese = "安全出口", English = "emergency exit" },

            // ========== 装饰装修 (Interior Finishing) ==========
            new ProfessionalTerm { Chinese = "装修", English = "finishing" },
            new ProfessionalTerm { Chinese = "装饰", English = "decoration" },
            new ProfessionalTerm { Chinese = "地板", English = "flooring" },
            new ProfessionalTerm { Chinese = "地砖", English = "floor tile" },
            new ProfessionalTerm { Chinese = "墙砖", English = "wall tile" },
            new ProfessionalTerm { Chinese = "石材", English = "stone" },
            new ProfessionalTerm { Chinese = "涂料", English = "paint" },
            new ProfessionalTerm { Chinese = "乳胶漆", English = "latex paint" },
            new ProfessionalTerm { Chinese = "腻子", English = "putty" },
            new ProfessionalTerm { Chinese = "壁纸", English = "wallpaper" },
            new ProfessionalTerm { Chinese = "踢脚线", English = "baseboard" },
            new ProfessionalTerm { Chinese = "吊顶龙骨", English = "ceiling frame" },
            new ProfessionalTerm { Chinese = "石膏板", English = "gypsum board" },
            new ProfessionalTerm { Chinese = "隔音", English = "sound insulation" },

            // ========== 施工工艺 (Construction Methods) ==========
            new ProfessionalTerm { Chinese = "浇筑", English = "pouring" },
            new ProfessionalTerm { Chinese = "振捣", English = "vibration" },
            new ProfessionalTerm { Chinese = "养护", English = "curing" },
            new ProfessionalTerm { Chinese = "回填", English = "backfill" },
            new ProfessionalTerm { Chinese = "夯实", English = "compaction" },
            new ProfessionalTerm { Chinese = "支模", English = "formwork" },
            new ProfessionalTerm { Chinese = "拆模", English = "form removal" },
            new ProfessionalTerm { Chinese = "绑扎", English = "tying" },
            new ProfessionalTerm { Chinese = "焊接", English = "welding" },
            new ProfessionalTerm { Chinese = "螺栓连接", English = "bolted connection" },
            new ProfessionalTerm { Chinese = "吊装", English = "hoisting" },
            new ProfessionalTerm { Chinese = "抹灰", English = "plastering" },
            new ProfessionalTerm { Chinese = "砌筑", English = "masonry" },
            new ProfessionalTerm { Chinese = "粉刷", English = "rendering" },

            // ========== 图纸常用词 (Drawing Terms) ==========
            new ProfessionalTerm { Chinese = "平面图", English = "floor plan" },
            new ProfessionalTerm { Chinese = "立面图", English = "elevation" },
            new ProfessionalTerm { Chinese = "剖面图", English = "section" },
            new ProfessionalTerm { Chinese = "详图", English = "detail drawing" },
            new ProfessionalTerm { Chinese = "节点详图", English = "node detail" },
            new ProfessionalTerm { Chinese = "大样图", English = "enlarged detail" },
            new ProfessionalTerm { Chinese = "总平面图", English = "site plan" },
            new ProfessionalTerm { Chinese = "结构布置图", English = "structural layout" },
            new ProfessionalTerm { Chinese = "配筋图", English = "reinforcement plan" },
            new ProfessionalTerm { Chinese = "轴线", English = "axis" },
            new ProfessionalTerm { Chinese = "轴网", English = "grid" },
            new ProfessionalTerm { Chinese = "标高", English = "elevation level" },
            new ProfessionalTerm { Chinese = "尺寸", English = "dimension" },
            new ProfessionalTerm { Chinese = "比例", English = "scale" },
            new ProfessionalTerm { Chinese = "说明", English = "notes" },
            new ProfessionalTerm { Chinese = "备注", English = "remarks" },
            new ProfessionalTerm { Chinese = "图例", English = "legend" },
            new ProfessionalTerm { Chinese = "材料表", English = "material list" },
            new ProfessionalTerm { Chinese = "工程量清单", English = "bill of quantities" },

            // ========== 质量控制 (Quality Control) ==========
            new ProfessionalTerm { Chinese = "检验批", English = "inspection lot" },
            new ProfessionalTerm { Chinese = "隐蔽工程", English = "concealed work" },
            new ProfessionalTerm { Chinese = "验收", English = "acceptance" },
            new ProfessionalTerm { Chinese = "质量检验", English = "quality inspection" },
            new ProfessionalTerm { Chinese = "强度", English = "strength" },
            new ProfessionalTerm { Chinese = "试块", English = "test specimen" },
            new ProfessionalTerm { Chinese = "合格", English = "qualified" },
            new ProfessionalTerm { Chinese = "不合格", English = "unqualified" },
        };

        /// <summary>
        /// 翻译记忆库（Translation Memory）- 高质量平行语料示例
        ///
        /// ✅ 基于阿里云百炼官方最佳实践：
        /// 作用：引导模型模仿示例的翻译风格、术语选择、格式保持
        /// 参考：https://help.aliyun.com/zh/model-studio/machine-translation
        ///
        /// 选择标准：
        /// 1. 典型的AutoCAD图纸标注场景
        /// 2. 覆盖常见专业领域（结构、建筑、MEP）
        /// 3. 展示正确的术语使用和格式保持
        /// 4. 包含技术标识保留的最佳实践
        /// </summary>
        public static readonly List<TranslationMemoryPair> TranslationMemory = new List<TranslationMemoryPair>
        {
            // 中译英示例
            new TranslationMemoryPair
            {
                Source = "300×600钢筋混凝土梁，混凝土强度等级C30，钢筋HRB400",
                Target = "300×600 reinforced concrete beam, C30 concrete grade, HRB400 steel reinforcement",
                Category = "结构构件"
            },
            new TranslationMemoryPair
            {
                Source = "框架柱KZ1，截面尺寸600×600，混凝土C35，纵筋12Φ25",
                Target = "Frame column KZ1, section size 600×600, C35 concrete, longitudinal reinforcement 12Φ25",
                Category = "结构构件"
            },
            new TranslationMemoryPair
            {
                Source = "屋面板厚度120mm，C30细石混凝土浇筑，双向配筋Φ8@200",
                Target = "Roof slab thickness 120mm, C30 fine aggregate concrete pouring, two-way reinforcement Φ8@200",
                Category = "结构构件"
            },
            new TranslationMemoryPair
            {
                Source = "外墙为200厚加气混凝土砌块，MU5强度等级，M5混合砂浆砌筑",
                Target = "Exterior wall: 200mm thick AAC block, MU5 strength grade, M5 mixed mortar masonry",
                Category = "建筑构造"
            },
            new TranslationMemoryPair
            {
                Source = "卫生间防水采用1.5mm厚聚氨酯防水涂料，上翻300mm",
                Target = "Bathroom waterproofing: 1.5mm thick polyurethane waterproof coating, turned up 300mm",
                Category = "建筑构造"
            },
            new TranslationMemoryPair
            {
                Source = "消火栓系统设计压力0.35MPa，流量40L/s，按GB 50974-2014执行",
                Target = "Fire hydrant system design pressure 0.35MPa, flow rate 40L/s, per GB 50974-2014",
                Category = "消防系统"
            },
            new TranslationMemoryPair
            {
                Source = "给水管采用PPR管，DN25，公称压力1.6MPa",
                Target = "Water supply pipe: PPR pipe, DN25, nominal pressure 1.6MPa",
                Category = "给排水"
            },
            new TranslationMemoryPair
            {
                Source = "配电箱AL-1，电源进线3×95+1×50，TN-S系统",
                Target = "Distribution box AL-1, power supply cable 3×95+1×50, TN-S system",
                Category = "电气系统"
            },
            new TranslationMemoryPair
            {
                Source = "新风机组风量5000m³/h，余压200Pa，电机功率2.2kW",
                Target = "Fresh air unit air volume 5000m³/h, residual pressure 200Pa, motor power 2.2kW",
                Category = "暖通空调"
            },
            new TranslationMemoryPair
            {
                Source = "详见节点详图No.SD-102，轴网定位A/1轴交点",
                Target = "Refer to detail drawing No.SD-102, grid location at Axis A/1 intersection",
                Category = "图纸标注"
            },

            // 英译中示例
            new TranslationMemoryPair
            {
                Source = "Beam B1, section 400×700, C30 concrete, top reinforcement 6Φ20, bottom reinforcement 4Φ18",
                Target = "梁B1，截面400×700，C30混凝土，上部配筋6Φ20，下部配筋4Φ18",
                Category = "结构构件"
            },
            new TranslationMemoryPair
            {
                Source = "Shear wall thickness 200mm, vertical reinforcement Φ12@200 double layer",
                Target = "剪力墙厚度200mm，竖向钢筋Φ12@200双层双向",
                Category = "结构构件"
            },
            new TranslationMemoryPair
            {
                Source = "Floor slab: 100mm thickness, C25 concrete, reinforcement Φ8@150 both ways",
                Target = "楼板：厚度100mm，C25混凝土，配筋Φ8@150双向",
                Category = "结构构件"
            },
            new TranslationMemoryPair
            {
                Source = "Exterior wall finish: Stone cladding with 20mm thick granite, dry-hung system",
                Target = "外墙饰面：20mm厚花岗岩石材干挂系统",
                Category = "建筑装饰"
            },
            new TranslationMemoryPair
            {
                Source = "Sprinkler system design per NFPA 13, upright sprinkler heads at 3.0m spacing",
                Target = "喷淋系统按NFPA 13设计，直立型喷头间距3.0m",
                Category = "消防系统"
            },
            new TranslationMemoryPair
            {
                Source = "HVAC duct: Galvanized steel sheet, 400×300mm, thickness 0.8mm",
                Target = "空调风管：镀锌钢板，400×300mm，板厚0.8mm",
                Category = "暖通空调"
            },
            new TranslationMemoryPair
            {
                Source = "Lighting fixture: LED panel light, 600×600mm, 36W, CRI≥80, CCT 4000K",
                Target = "照明灯具：LED面板灯，600×600mm，36W，显色指数≥80，色温4000K",
                Category = "电气照明"
            },
            new TranslationMemoryPair
            {
                Source = "Foundation: Pile cap thickness 1200mm, C35 concrete, reinforcement mesh Φ20@150",
                Target = "基础：承台厚度1200mm，C35混凝土，钢筋网Φ20@150",
                Category = "基础工程"
            },
            new TranslationMemoryPair
            {
                Source = "Refer to structural layout plan for beam and column locations",
                Target = "梁柱位置详见结构布置图",
                Category = "图纸引用"
            },
            new TranslationMemoryPair
            {
                Source = "See detail A for waterproofing construction at roof parapet",
                Target = "屋面女儿墙防水构造详见详图A",
                Category = "图纸引用"
            }
        };

        /// <summary>
        /// 转换为阿里云百炼API所需的tm_list格式
        /// </summary>
        public static List<object> GetApiTranslationMemory(string sourceLang, string targetLang)
        {
            var tmList = new List<object>();

            // 根据翻译方向选择合适的示例
            bool isEnToZh = sourceLang.Contains("English") && targetLang.Contains("Chinese");
            bool isZhToEn = sourceLang.Contains("Chinese") && targetLang.Contains("English");

            foreach (var pair in TranslationMemory)
            {
                if (isEnToZh && pair.Source.All(c => c < 128 || char.IsPunctuation(c))) // 英文源文本
                {
                    tmList.Add(new { source = pair.Source, target = pair.Target });
                }
                else if (isZhToEn && pair.Source.Any(c => c > 128)) // 中文源文本
                {
                    tmList.Add(new { source = pair.Source, target = pair.Target });
                }
            }

            // ✅ 限制在10个示例以内（避免context过长）
            return tmList.Take(10).ToList();
        }

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

        /// <summary>
        /// 为通用对话模型（qwen-flash/qwen-plus）构建系统提示词
        /// ✅ v1.0.9增强：XML结构化格式 + 错误/正确示例对比 + 强调纯净输出
        /// 基于阿里云百炼Prompt Engineering最佳实践
        /// </summary>
        /// <summary>
        /// ✅ P0紧急修复：构建简洁的系统提示词（遵循阿里云百炼最佳实践）
        ///
        /// **问题根源**：
        /// - 旧版使用XML格式（<system>、<role>、<task>等标签）
        /// - 导致模型返回整个系统提示词，而不是翻译结果
        /// - 用户反馈："满屏幕的大模型系统提示词"
        ///
        /// **修复方案**：
        /// - 使用简洁的纯文本格式（无XML标签）
        /// - 遵循阿里云百炼官方Prompt Engineering最佳实践
        /// - 参考：https://help.aliyun.com/zh/model-studio/prompt-engineering-guide
        ///
        /// **效果**：
        /// - 模型直接输出翻译结果，无额外内容
        /// - 降低token消耗（简洁提示词）
        /// - 提高翻译准确性（明确指令）
        /// </summary>
        public static string BuildSystemPromptForModel(string sourceLang, string targetLang)
        {
            var isToEnglish = targetLang.Contains("English");

            if (isToEnglish)
            {
                // 中文 → 英文（简洁版，无XML标签）
                return @"你是CAD/BIM工程图纸专业翻译。严格遵守：
1. 使用标准工程术语（参考ACI, AISC, ASHRAE, IBC）
2. 保留图号、规范代号（GB, JGJ, ACI）、材料牌号（C30, Q235, HRB400）、单位、轴线
3. 直接输出译文，不加任何前缀、后缀、解释

示例：
用户：主梁（ML-1）C30混凝土
翻译：Main Beam (ML-1) C30 Concrete

用户：详见详图No.SD-102，A/1轴交点
翻译：Refer to Detail Drawing No.SD-102, Axis A/1 Intersection

用户：消火栓系统设计压力0.35MPa，流量40L/s，按GB 50974-2014执行
翻译：Fire Hydrant System Design Pressure 0.35MPa, Flow Rate 40L/s, per GB 50974-2014";
            }
            else
            {
                // 任意语言 → 中文（简洁版，无XML标签）
                return @"你是CAD/BIM工程图纸专业翻译。严格遵守：
1. 使用标准工程术语（符合中国建筑规范）
2. 保留图号、规范代号（GB, JGJ, ACI）、材料牌号（C30, Q235, HRB400）、单位、轴线
3. 直接输出译文，不加任何前缀、后缀、解释

示例：
用户：Main Beam (ML-1) C30 Concrete
翻译：主梁（ML-1）C30混凝土

用户：Refer to Detail Drawing No.SD-102, Axis A/1 Intersection
翻译：详见详图No.SD-102，A/1轴交点

用户：Fire Hydrant System Design Pressure 0.35MPa, Flow Rate 40L/s, per GB 50974-2014
翻译：消火栓系统设计压力0.35MPa，流量40L/s，按GB 50974-2014执行";
            }
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

    /// <summary>
    /// 翻译记忆对（Translation Memory Pair）
    /// 用于阿里云百炼tm_list参数，提供高质量翻译示例
    /// </summary>
    public class TranslationMemoryPair
    {
        /// <summary>
        /// 源语言文本
        /// </summary>
        public string Source { get; set; } = "";

        /// <summary>
        /// 目标语言文本
        /// </summary>
        public string Target { get; set; } = "";

        /// <summary>
        /// 类别标签（用于筛选相关示例）
        /// </summary>
        public string Category { get; set; } = "";
    }
}
