using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace BiaogPlugin.Models
{
    /// <summary>
    /// AutoCAD标注（Dimension）实体详细数据模型
    /// ✅ 直接从Dimension实体提取精确的几何尺寸数据
    /// </summary>
    public class DimensionData
    {
        /// <summary>
        /// AutoCAD对象ID
        /// </summary>
        public ObjectId Id { get; set; }

        /// <summary>
        /// 标注类型
        /// </summary>
        public DimensionType DimensionType { get; set; }

        /// <summary>
        /// ✅ 实际测量值（从Dimension.Measurement属性获取）
        /// 这是AutoCAD计算的精确几何尺寸，单位为当前图纸单位
        /// </summary>
        public double Measurement { get; set; }

        /// <summary>
        /// 标注文本（可能包含前缀、后缀或自定义覆盖文本）
        /// </summary>
        public string DimensionText { get; set; } = string.Empty;

        /// <summary>
        /// 所在图层
        /// </summary>
        public string Layer { get; set; } = string.Empty;

        /// <summary>
        /// 文本位置
        /// </summary>
        public Point3d TextPosition { get; set; }

        /// <summary>
        /// ✅ 线性尺寸：第一个延伸线点（适用于AlignedDimension, RotatedDimension）
        /// </summary>
        public Point3d? XLine1Point { get; set; }

        /// <summary>
        /// ✅ 线性尺寸：第二个延伸线点（适用于AlignedDimension, RotatedDimension）
        /// </summary>
        public Point3d? XLine2Point { get; set; }

        /// <summary>
        /// ✅ 标注线位置（适用于AlignedDimension, RotatedDimension）
        /// </summary>
        public Point3d? DimLinePoint { get; set; }

        /// <summary>
        /// ✅ 旋转角度（适用于RotatedDimension）- 弧度
        /// </summary>
        public double? Rotation { get; set; }

        /// <summary>
        /// ✅ 圆心（适用于RadialDimension, DiametricDimension, ArcDimension）
        /// </summary>
        public Point3d? Center { get; set; }

        /// <summary>
        /// ✅ 弦点（适用于RadialDimension）
        /// </summary>
        public Point3d? ChordPoint { get; set; }

        /// <summary>
        /// ✅ 远弦点（适用于DiametricDimension）
        /// </summary>
        public Point3d? FarChordPoint { get; set; }

        /// <summary>
        /// ✅ 角度标注：第一条线起点（适用于LineAngularDimension2）
        /// </summary>
        public Point3d? XLine1Start { get; set; }

        /// <summary>
        /// ✅ 角度标注：第一条线终点（适用于LineAngularDimension2）
        /// </summary>
        public Point3d? XLine1End { get; set; }

        /// <summary>
        /// ✅ 角度标注：第二条线起点（适用于LineAngularDimension2）
        /// </summary>
        public Point3d? XLine2Start { get; set; }

        /// <summary>
        /// ✅ 角度标注：第二条线终点（适用于LineAngularDimension2）
        /// </summary>
        public Point3d? XLine2End { get; set; }

        /// <summary>
        /// ✅ 弧圆心（适用于ArcDimension）
        /// </summary>
        public Point3d? ArcPoint { get; set; }

        /// <summary>
        /// 所属空间名称（ModelSpace, Layout:xxx）
        /// </summary>
        public string? SpaceName { get; set; }

        /// <summary>
        /// 旋转角度（度数）
        /// </summary>
        public double? RotationDegrees => Rotation.HasValue ? Rotation.Value * 180.0 / Math.PI : null;

        /// <summary>
        /// ✅ 计算真实的线性距离（对于线性标注）
        /// 从两个延伸线点计算实际距离，用于验证Measurement的准确性
        /// </summary>
        public double? CalculatedLinearDistance
        {
            get
            {
                if (XLine1Point.HasValue && XLine2Point.HasValue)
                {
                    return XLine1Point.Value.DistanceTo(XLine2Point.Value);
                }
                return null;
            }
        }

        /// <summary>
        /// ✅ 获取半径（对于径向标注）
        /// </summary>
        public double? Radius
        {
            get
            {
                if (DimensionType == DimensionType.Radial && Center.HasValue && ChordPoint.HasValue)
                {
                    return Center.Value.DistanceTo(ChordPoint.Value);
                }
                return null;
            }
        }

        /// <summary>
        /// ✅ 获取直径（对于直径标注）
        /// </summary>
        public double? Diameter
        {
            get
            {
                if (DimensionType == DimensionType.Diametric)
                {
                    return Measurement; // 直径标注的Measurement就是直径值
                }
                return null;
            }
        }

        public override string ToString()
        {
            return $"[{DimensionType}] {Measurement:F3} - {DimensionText} (Layer: {Layer})";
        }
    }

    /// <summary>
    /// 标注类型枚举（对应AutoCAD的8种Dimension派生类）
    /// </summary>
    public enum DimensionType
    {
        /// <summary>
        /// 对齐标注（AlignedDimension）- 标注线平行于两点连线
        /// </summary>
        Aligned,

        /// <summary>
        /// 旋转标注（RotatedDimension）- 标注线按指定角度旋转
        /// </summary>
        Rotated,

        /// <summary>
        /// 直径标注（DiametricDimension）- 标注圆或弧的直径
        /// </summary>
        Diametric,

        /// <summary>
        /// 半径标注（RadialDimension）- 标注圆或弧的半径
        /// </summary>
        Radial,

        /// <summary>
        /// 大半径标注（RadialDimensionLarge）- 标注大半径圆弧
        /// </summary>
        RadialLarge,

        /// <summary>
        /// 角度标注-两线（LineAngularDimension2）- 标注两条线之间的角度
        /// </summary>
        LineAngular,

        /// <summary>
        /// 角度标注-三点（Point3AngularDimension）- 标注三点确定的角度
        /// </summary>
        Point3Angular,

        /// <summary>
        /// 弧长标注（ArcDimension）- 标注弧的长度
        /// </summary>
        Arc
    }
}
