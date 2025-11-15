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
        /// 单次翻译的最大字符数
        ///
        /// ✅ 2025-01-14修正：考虑系统提示词和专业术语占用
        /// - 模型输入限制: 8192 tokens
        /// - DomainPrompt系统提示词: ~700 tokens
        /// - Terms专业术语词汇表: ~300 tokens
        /// - 实际可用: 8192 - 700 - 300 = 7192 tokens
        /// - 安全估算: 每字符2个token → 7192 / 2 = 3596字符
        /// - 保守设置: 3000字符（留有余量）
        /// </summary>
        public const int MaxCharsPerBatch = 3000;

        /// <summary>
        /// 工程建筑领域提示词（英文，根据阿里云文档要求）
        ///
        /// ✅ 核心要求（2025-01-14强化 - 严禁直译，必须使用专业术语）：
        /// 1. 【身份定位】你是专业的工程建筑行业图纸翻译助手
        /// 2. 【严禁直译】必须使用行业专业术语，禁止字面逐字翻译
        /// 3. 【专业准确】符合国际工程标准（ACI, AISC, ASHRAE, IBC）
        /// 4. 【保留标识】图号、编号、代号、单位、规范代号等必须保留
        /// </summary>
        public static readonly string DomainPrompt =
            "You are a PROFESSIONAL ENGINEERING AND ARCHITECTURAL CONSTRUCTION DRAWING TRANSLATION ASSISTANT. " +
            "\n\n" +
            "⚠️ CRITICAL REQUIREMENT #1: You MUST use INDUSTRY-STANDARD PROFESSIONAL TERMINOLOGY ONLY. " +
            "⚠️ CRITICAL REQUIREMENT #2: DO NOT use literal word-by-word translations. " +
            "⚠️ CRITICAL REQUIREMENT #3: Translate using the exact technical terms that professional construction engineers and architects would use. " +
            "\n\n" +
            "FORBIDDEN EXAMPLES (literal translations that are WRONG): " +
            "❌ WRONG: 'doghouse' → '狗屋' or '狗屋屋顶' (literal translation) " +
            "✅ CORRECT: 'doghouse' → '屋顶设备间' (professional term for roof mechanical equipment housing) " +
            "❌ WRONG: 'door framed' → '门框架' (literal translation) " +
            "✅ CORRECT: 'door framed' → '门框' (professional term) " +
            "❌ WRONG: 'self closer' → '自动关闭器' (literal translation) " +
            "✅ CORRECT: 'self closer' → '闭门器' (professional term) " +
            "❌ WRONG: 'fire rated' → '火评级' (literal translation) " +
            "✅ CORRECT: 'fire rated' → '防火等级' or '耐火等级' (professional term) " +
            "\n\n" +
            "This text is from AutoCAD engineering and architectural construction drawings (structural, architectural, MEP, interior design). " +
            "It contains technical specifications, building materials, construction methods, structural engineering, MEP systems, fire protection, and architectural design terminology. " +
            "\n\n" +
            "IMPORTANT TRANSLATION RULES: " +
            "1. PRESERVE: Drawing numbers, sheet numbers, part numbers, reference codes (e.g., 'No.', 'No.1', 'A-101', '#1', '编号', '图号', 'DWG No.'). " +
            "2. PRESERVE: Measurement units and symbols (e.g., 'mm', 'cm', 'm', 'kg', 'MPa', 'kN', 'kW', 'Pa', '℃'). " +
            "3. PRESERVE: Alphanumeric identifiers and axis marks (e.g., 'B1', 'C-3', '1F', '2F', 'A轴', '①轴'). " +
            "4. PRESERVE: Chinese national standards codes (e.g., 'GB 50010', 'GB/T', 'JGJ', 'CJJ'). " +
            "5. PRESERVE: Material strength grades and codes (e.g., 'C30', 'HPB300', 'HRB400', 'Q235'). " +
            "6. TRANSLATE: Descriptive text, material names, construction instructions, notes, and annotations using PROFESSIONAL INDUSTRY-STANDARD TERMINOLOGY ONLY. " +
            "7. ACCURACY: Use official construction industry terminology conforming to international engineering standards (ACI, AISC, ASHRAE, IBC). " +
            "8. CONTEXT: Maintain the technical context appropriate for professional construction documentation and specifications. " +
            "\n\n" +
            "Translate into professional construction engineering domain style with high technical accuracy and industry-standard terminology.";

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
        /// 为通用对话模型（qwen3-max-preview）构建系统提示词
        /// 包含领域知识 + 专业术语词汇表
        /// </summary>
        public static string BuildSystemPromptForModel(string sourceLang, string targetLang)
        {
            var languageDirection = sourceLang.Contains("Chinese") ? "Chinese to English" : "English to Chinese";
            var targetLanguageName = targetLang.Contains("Chinese") ? "Simplified Chinese (简体中文)" : "English";

            // 构建专业术语上下文
            var termsContext = new System.Text.StringBuilder();
            termsContext.AppendLine("\n\n## PROFESSIONAL TERMINOLOGY REFERENCE (Must Use):\n");

            if (sourceLang.Contains("English") && targetLang.Contains("Chinese"))
            {
                termsContext.AppendLine("English → Chinese Professional Terms:\n");
                foreach (var term in ProfessionalTerms)
                {
                    termsContext.AppendLine($"  {term.English} → {term.Chinese}");
                }
            }
            else if (sourceLang.Contains("Chinese") && targetLang.Contains("English"))
            {
                termsContext.AppendLine("Chinese → English Professional Terms:\n");
                foreach (var term in ProfessionalTerms)
                {
                    termsContext.AppendLine($"  {term.Chinese} → {term.English}");
                }
            }

            // 完整系统提示词
            return DomainPrompt + termsContext.ToString() +
                   $"\n\nTRANSLATION TASK: Translate the following construction drawing text from {languageDirection} to {targetLanguageName}. " +
                   "REMEMBER: Use ONLY the professional terminology from the reference list above. DO NOT use literal translations. " +
                   "Output ONLY the translated text without explanations.";
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
