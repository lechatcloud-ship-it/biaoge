using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;

namespace BiaogPlugin.Models
{
    /// <summary>
    /// DWG文本实体数据模型
    /// 统一表示所有类型的文本实体（DBText、MText、AttributeReference等）
    /// </summary>
    public class TextEntity
    {
        /// <summary>
        /// AutoCAD对象ID（用于后续更新）
        /// </summary>
        public ObjectId Id { get; set; }

        /// <summary>
        /// 文本类型
        /// </summary>
        public TextEntityType Type { get; set; }

        /// <summary>
        /// 文本内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// 位置坐标
        /// </summary>
        public Point3d Position { get; set; }

        /// <summary>
        /// 所在图层
        /// </summary>
        public string Layer { get; set; } = string.Empty;

        /// <summary>
        /// 文本高度
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// 旋转角度（弧度）
        /// </summary>
        public double Rotation { get; set; }

        /// <summary>
        /// 颜色索引
        /// </summary>
        public short ColorIndex { get; set; }

        /// <summary>
        /// 宽度（仅MText）
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// 属性标签（仅AttributeDefinition和AttributeReference）
        /// </summary>
        public string? Tag { get; set; }

        /// <summary>
        /// 所属块名称（仅AttributeReference）
        /// </summary>
        public string? BlockName { get; set; }

        /// <summary>
        /// 文本样式名称
        /// </summary>
        public string? TextStyle { get; set; }

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

        /// <summary>
        /// 角度（度数）
        /// </summary>
        public double RotationDegrees => Rotation * 180.0 / Math.PI;

        public override string ToString()
        {
            return $"[{Type}] {Content} (Layer: {Layer}, Pos: {Position})";
        }
    }

    /// <summary>
    /// 文本实体类型枚举
    /// </summary>
    public enum TextEntityType
    {
        /// <summary>
        /// 单行文本（DBText）
        /// </summary>
        DBText,

        /// <summary>
        /// 多行文本（MText）
        /// </summary>
        MText,

        /// <summary>
        /// 属性定义（AttributeDefinition）
        /// </summary>
        AttributeDefinition,

        /// <summary>
        /// 属性参照（AttributeReference - 块中的属性）
        /// </summary>
        AttributeReference
    }

    /// <summary>
    /// 文本更新请求
    /// </summary>
    public class TextUpdateRequest
    {
        /// <summary>
        /// 要更新的文本实体ID
        /// </summary>
        public ObjectId ObjectId { get; set; }

        /// <summary>
        /// 原始内容
        /// </summary>
        public string OriginalContent { get; set; } = string.Empty;

        /// <summary>
        /// 新内容（翻译后的文本）
        /// </summary>
        public string NewContent { get; set; } = string.Empty;

        /// <summary>
        /// 图层（用于日志）
        /// </summary>
        public string? Layer { get; set; }

        /// <summary>
        /// 实体类型（用于日志）
        /// </summary>
        public TextEntityType? EntityType { get; set; }

        /// <summary>
        /// 是否需要更新（内容发生变化）
        /// </summary>
        public bool NeedsUpdate => OriginalContent != NewContent;

        public override string ToString()
        {
            return $"{OriginalContent} → {NewContent}";
        }
    }
}
