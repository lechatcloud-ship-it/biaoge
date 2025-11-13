using System.Numerics;
using Autodesk.AutoCAD.DatabaseServices;

namespace BiaogPlugin.Models
{
    /// <summary>
    /// DWG文本实体数据模型（简化版）
    /// 用于图层翻译和快速文本提取
    /// </summary>
    public class DwgTextEntity
    {
        /// <summary>
        /// AutoCAD对象ID（用于后续更新）
        /// </summary>
        public ObjectId ObjectId { get; set; }

        /// <summary>
        /// 文本内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 文本类型（DBText/MText/AttributeReference）
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 所在图层
        /// </summary>
        public string Layer { get; set; } = string.Empty;

        /// <summary>
        /// 位置坐标
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// 是否可翻译（自动计算）
        /// </summary>
        public bool IsTranslatable
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Content))
                    return false;

                // 如果全是数字和符号，不需要翻译
                bool hasLetter = false;
                foreach (char c in Content)
                {
                    if (char.IsLetter(c))
                    {
                        hasLetter = true;
                        break;
                    }
                }

                return hasLetter && Content.Trim().Length >= 2;
            }
        }

        public override string ToString()
        {
            return $"[{Type}] {Content} (Layer: {Layer}, Pos: {Position})";
        }
    }
}
