using System;
using System.Collections.Generic;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// GB 50854-2013 项目编码生成器
    ///
    /// 编码规则：12位阿拉伯数字
    /// - 第1-2位：专业工程编码（01=土建工程）
    /// - 第3-4位：附录分类编码（03=混凝土及钢筋混凝土工程）
    /// - 第5-6位：分部工程编码（01=现浇混凝土柱，02=梁，03=板，04=墙...）
    /// - 第7-9位：清单项目编码（001-999）
    /// - 第10-12位：具体特征编码（001-999）
    ///
    /// 参考：GB 50854-2013《房屋建筑与装饰工程工程量计算规范》附录E
    /// </summary>
    public static class GBProjectCodeGenerator
    {
        /// <summary>
        /// 获取构件类型的项目编码
        /// </summary>
        public static string GetProjectCode(string componentType)
        {
            // 提取核心类型（去除强度等级、钢筋牌号等后缀）
            string coreType = ExtractCoreType(componentType);

            // 根据核心类型返回对应的项目编码
            if (_codeMapping.TryGetValue(coreType, out string code))
            {
                return code;
            }

            // 默认编码：010399999（其他混凝土构件）
            return "010399999000";
        }

        /// <summary>
        /// 获取构件类型的计量单位
        /// </summary>
        public static string GetMeasurementUnit(string componentType)
        {
            if (componentType.Contains("钢筋"))
            {
                return "t";  // 吨
            }
            else if (componentType.Contains("门") || componentType.Contains("窗"))
            {
                return "m²";  // 平方米
            }
            else if (componentType.Contains("墙") || componentType.Contains("板") || componentType.Contains("屋面") || componentType.Contains("防水") || componentType.Contains("保温"))
            {
                return "m²";  // 平方米
            }
            else if (componentType.Contains("柱") || componentType.Contains("梁") || componentType.Contains("楼梯") || componentType.Contains("基础"))
            {
                return "m³";  // 立方米
            }
            else if (componentType.Contains("预制"))
            {
                return "m³";  // 立方米
            }
            else
            {
                return "m³";  // 默认立方米
            }
        }

        /// <summary>
        /// 提取核心类型（去除强度等级）
        /// 例如："C30混凝土柱" → "柱"
        /// </summary>
        private static string ExtractCoreType(string componentType)
        {
            // 移除混凝土强度等级
            string result = System.Text.RegularExpressions.Regex.Replace(componentType, @"C\d{1,2}", "");

            // 移除钢筋牌号
            result = System.Text.RegularExpressions.Regex.Replace(result, @"HRB\d{3}", "");
            result = System.Text.RegularExpressions.Regex.Replace(result, @"HPB\d{3}", "");

            // 移除"混凝土"、"钢筋混凝土"等前缀
            result = result.Replace("钢筋混凝土", "").Replace("混凝土", "").Replace("现浇", "");

            // 去除空格
            result = result.Trim();

            return result;
        }

        /// <summary>
        /// GB 50854-2013 项目编码映射表
        ///
        /// 基于附录E：混凝土及钢筋混凝土工程
        /// 编码格式：010301001XXX
        ///   - 01：土建工程
        ///   - 03：混凝土及钢筋混凝土工程
        ///   - 01：现浇混凝土柱
        ///   - 001：具体项目
        ///   - XXX：特征编码
        /// </summary>
        private static readonly Dictionary<string, string> _codeMapping = new Dictionary<string, string>
        {
            // ===== 现浇混凝土构件（0103XX） =====

            // 柱（010301）
            ["柱"] = "010301001000",
            ["框架柱"] = "010301001001",
            ["构造柱"] = "010301001002",
            ["芯柱"] = "010301001003",

            // 梁（010302）
            ["梁"] = "010302001000",
            ["框架梁"] = "010302001001",
            ["连梁"] = "010302001002",
            ["圈梁"] = "010302001003",
            ["过梁"] = "010302001004",

            // 板（010303）
            ["板"] = "010303001000",
            ["有梁板"] = "010303001001",
            ["无梁板"] = "010303001002",
            ["平板"] = "010303001003",
            ["拱板"] = "010303001004",
            ["薄壳板"] = "010303001005",

            // 墙（010304）
            ["墙"] = "010304001000",
            ["剪力墙"] = "010304001001",
            ["挡土墙"] = "010304001002",

            // 楼梯（010305）
            ["楼梯"] = "010305001000",
            ["直梯"] = "010305001001",
            ["弧形梯"] = "010305001002",

            // 其他构件（010306-010310）
            ["阳台"] = "010306001000",
            ["雨篷"] = "010307001000",
            ["台阶"] = "010308001000",
            ["散水"] = "010309001000",
            ["栏板"] = "010310001000",

            // ===== 屋面及防水（0104XX） =====
            ["屋面板"] = "010401001000",
            ["现浇屋面板"] = "010401001001",
            ["防水层"] = "010402001000",
            ["保温层"] = "010403001000",

            // ===== 预制构件（0105XX） =====
            ["预制板"] = "010501001000",
            ["预制梁"] = "010502001000",
            ["预制柱"] = "010503001000",

            // ===== 基础工程（0102XX） =====
            ["基础"] = "010201001000",
            ["独立基础"] = "010201001001",
            ["条形基础"] = "010201002001",
            ["筏板基础"] = "010201003001",
            ["承台基础"] = "010201004001",
            ["桩基础"] = "010201005001",

            // ===== 钢筋工程（0106XX） =====
            ["钢筋"] = "010601001000",

            // ===== 门窗工程（0107XX） =====
            ["门"] = "010701001000",
            ["窗"] = "010702001000",
        };
    }
}
