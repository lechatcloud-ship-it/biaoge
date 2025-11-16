using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace BiaogPlugin.Models
{
    /// <summary>
    /// AutoCAD几何实体数据模型
    /// ✅ 存储从Polyline、Region、Solid3d、Hatch等几何实体提取的精确面积/体积数据
    /// </summary>
    public class GeometryEntity
    {
        /// <summary>
        /// AutoCAD对象ID
        /// </summary>
        public ObjectId Id { get; set; }

        /// <summary>
        /// 几何实体类型
        /// </summary>
        public GeometryType Type { get; set; }

        /// <summary>
        /// 所在图层
        /// </summary>
        public string Layer { get; set; } = string.Empty;

        /// <summary>
        /// 所属空间名称（ModelSpace, Layout:xxx, BlockDefinition）
        /// </summary>
        public string? SpaceName { get; set; }

        /// <summary>
        /// ✅ 面积（平方米）- 从AutoCAD API直接获取的精确值
        /// </summary>
        public double Area { get; set; }

        /// <summary>
        /// ✅ 体积（立方米）- 仅Solid3d有值
        /// </summary>
        public double Volume { get; set; }

        /// <summary>
        /// 长度（米）- 边界框X方向尺寸
        /// </summary>
        public double Length { get; set; }

        /// <summary>
        /// 宽度（米）- 边界框Y方向尺寸
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// 高度（米）- 边界框Z方向尺寸
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// 质心/形心
        /// </summary>
        public Point3d Centroid { get; set; }

        /// <summary>
        /// 周长（米）- 仅Region有值
        /// </summary>
        public double Perimeter { get; set; }

        /// <summary>
        /// 惯性矩 - 仅Region有值
        /// </summary>
        public Matrix2d? MomentOfInertia { get; set; }

        /// <summary>
        /// 半径（米）- 仅Circle/Arc有值
        /// </summary>
        public double? Radius { get; set; }

        /// <summary>
        /// 起始角度（弧度）- 仅Arc有值
        /// </summary>
        public double? StartAngle { get; set; }

        /// <summary>
        /// 结束角度（弧度）- 仅Arc有值
        /// </summary>
        public double? EndAngle { get; set; }

        /// <summary>
        /// 顶点数量 - 仅Polyline有值
        /// </summary>
        public int? NumberOfVertices { get; set; }

        /// <summary>
        /// 填充图案名称 - 仅Hatch有值
        /// </summary>
        public string? HatchPatternName { get; set; }

        /// <summary>
        /// 填充图案类型 - 仅Hatch有值
        /// </summary>
        public string? HatchPatternType { get; set; }

        /// <summary>
        /// 填充边界数量 - 仅Hatch有值
        /// </summary>
        public int? NumberOfLoops { get; set; }

        /// <summary>
        /// 质量属性数据 - 仅Solid3d有值
        /// </summary>
        public MassPropertiesData? MassProperties { get; set; }

        // ✅ 新增：Ellipse（椭圆）属性
        /// <summary>
        /// 长半轴半径（米）- 仅Ellipse有值
        /// </summary>
        public double? MajorRadius { get; set; }

        /// <summary>
        /// 短半轴半径（米）- 仅Ellipse有值
        /// </summary>
        public double? MinorRadius { get; set; }

        // ✅ 新增：Spline（样条曲线）属性
        /// <summary>
        /// 控制点数量 - 仅Spline有值
        /// </summary>
        public int? NumControlPoints { get; set; }

        /// <summary>
        /// 拟合点数量 - 仅Spline有值
        /// </summary>
        public int? NumFitPoints { get; set; }

        /// <summary>
        /// 样条次数（Degree）- 仅Spline有值
        /// </summary>
        public int? SplineDegree { get; set; }

        // ✅ 新增：Face（三维面）属性
        /// <summary>
        /// 是否为三角形面 - 仅Face有值
        /// </summary>
        public bool? IsTriangle { get; set; }

        // ✅ 新增：Surface（曲面）属性
        /// <summary>
        /// 曲面类型名称（DBSurface、NurbSurface等）- 仅Surface有值
        /// </summary>
        public string? SurfaceTypeName { get; set; }

        public override string ToString()
        {
            return $"[{Type}] Layer={Layer}, Area={Area:F2}m², Volume={Volume:F3}m³";
        }
    }

    /// <summary>
    /// 几何实体类型枚举
    /// </summary>
    public enum GeometryType
    {
        /// <summary>
        /// 轻量多段线（Polyline） - AutoCAD 2022最常用
        /// </summary>
        Polyline,

        /// <summary>
        /// 2D多段线（Polyline2d） - 老式多段线
        /// </summary>
        Polyline2d,

        /// <summary>
        /// 3D多段线（Polyline3d）
        /// </summary>
        Polyline3d,

        /// <summary>
        /// 区域（Region） - 布尔运算后的复杂区域
        /// </summary>
        Region,

        /// <summary>
        /// 三维实体（Solid3d） - 建筑构件核心类型
        /// </summary>
        Solid3d,

        /// <summary>
        /// 填充（Hatch） - 常用于表示建筑材料
        /// </summary>
        Hatch,

        /// <summary>
        /// 圆（Circle）
        /// </summary>
        Circle,

        /// <summary>
        /// 圆弧（Arc）
        /// </summary>
        Arc,

        // ✅ 新增：高级几何类型（AutoCAD 2022完整支持）

        /// <summary>
        /// 椭圆（Ellipse） - 支持完整椭圆和椭圆弧
        /// </summary>
        Ellipse,

        /// <summary>
        /// 样条曲线（Spline） - NURBS曲线，支持闭合样条
        /// </summary>
        Spline,

        /// <summary>
        /// 三维面（Face） - 3D三角形或四边形面
        /// </summary>
        Face,

        /// <summary>
        /// 曲面（Surface） - DBSurface、NurbSurface、PlaneSurface等
        /// </summary>
        Surface
    }

    /// <summary>
    /// 质量属性数据（从Solid3d.MassProperties提取）
    /// </summary>
    public class MassPropertiesData
    {
        /// <summary>
        /// 体积（立方米）
        /// </summary>
        public double Volume { get; set; }

        /// <summary>
        /// 质量（千克）- 体积 × 密度
        /// </summary>
        public double Mass { get; set; }

        /// <summary>
        /// 质心
        /// </summary>
        public Point3d Centroid { get; set; }

        /// <summary>
        /// 惯性矩
        /// </summary>
        public Matrix3d MomentOfInertia { get; set; }

        /// <summary>
        /// 惯性积
        /// </summary>
        public Matrix3d ProductOfInertia { get; set; }

        /// <summary>
        /// 主惯性矩
        /// </summary>
        public Vector3d PrincipalMoments { get; set; }

        /// <summary>
        /// 回转半径
        /// </summary>
        public Vector3d Radii { get; set; }
    }
}
