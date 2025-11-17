using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Serilog;
using BiaogPlugin.Models;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// ✅ 算量诊断服务 - 彻底解决面积为0的问题
    ///
    /// 用户反馈："改了上百次，面积始终显示是0，就很过分"
    ///
    /// 诊断目标：
    /// 1. 检查图纸中是否有可提取面积的几何实体（Polyline/Hatch/Region/Solid3d）
    /// 2. 检查构件识别是否成功
    /// 3. 检查几何实体与构件的匹配是否成功
    /// 4. 输出详细的诊断报告，帮助定位问题
    /// </summary>
    public class QuantityDiagnosticService
    {
        private readonly GeometryExtractor _geometryExtractor;
        private readonly ComponentRecognizer _componentRecognizer;

        public QuantityDiagnosticService()
        {
            var bailianClient = ServiceLocator.Get<BailianApiClient>();
            _geometryExtractor = new GeometryExtractor();
            _componentRecognizer = new ComponentRecognizer(bailianClient);
        }

        /// <summary>
        /// 运行完整的算量诊断并生成详细报告
        /// </summary>
        public string RunFullDiagnostic()
        {
            var report = new StringBuilder();
            report.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
            report.AppendLine("║         标哥算量功能诊断报告 - 面积为0问题排查           ║");
            report.AppendLine("╚═══════════════════════════════════════════════════════════════╝");
            report.AppendLine();
            report.AppendLine($"诊断时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                report.AppendLine("❌ 错误：没有活动的AutoCAD文档");
                return report.ToString();
            }

            report.AppendLine($"当前图纸: {doc.Name}");
            report.AppendLine();

            // =======================================================================
            // 步骤1: 检查图纸中的文本实体（用于构件识别）
            // =======================================================================
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("【步骤1】检查文本实体（DBText/MText/Dimension等）");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var textExtractor = new DwgTextExtractor();
            var textEntities = textExtractor.ExtractAllText();

            report.AppendLine($"✅ 提取到文本实体总数: {textEntities.Count}个");
            report.AppendLine();
            report.AppendLine("文本类型分布:");
            var textStats = textEntities.GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count);

            foreach (var stat in textStats)
            {
                report.AppendLine($"  - {stat.Type}: {stat.Count}个");
            }
            report.AppendLine();

            // 显示前10个文本实体内容样例
            report.AppendLine("文本内容样例（前10个）:");
            int sampleCount = 0;
            foreach (var text in textEntities.Take(10))
            {
                sampleCount++;
                var preview = text.Content.Length > 40 ? text.Content.Substring(0, 40) + "..." : text.Content;
                report.AppendLine($"  {sampleCount}. [{text.Type}] {preview}（图层：{text.Layer}）");
            }
            report.AppendLine();

            // =======================================================================
            // 步骤2: 检查图纸中的几何实体（Polyline/Hatch/Region/Solid3d）
            // =======================================================================
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("【步骤2】检查几何实体（Polyline/Hatch/Region/Solid3d）");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            List<GeometryEntity> geometries;
            try
            {
                geometries = _geometryExtractor.ExtractAllGeometry();
                report.AppendLine($"✅ 提取到几何实体总数: {geometries.Count}个");
                report.AppendLine();

                if (geometries.Count == 0)
                {
                    report.AppendLine("⚠️ 警告：图纸中没有找到任何可计算面积的几何实体！");
                    report.AppendLine();
                    report.AppendLine("可能原因：");
                    report.AppendLine("  1. 图纸只有文字和线条，没有闭合的Polyline/Hatch等");
                    report.AppendLine("  2. 构件轮廓使用Line绘制，需要转换为Polyline");
                    report.AppendLine("  3. 图层被关闭或冻结");
                    report.AppendLine();
                    report.AppendLine("建议解决方案：");
                    report.AppendLine("  - 使用AutoCAD的BOUNDARY命令将构件轮廓转为闭合Polyline");
                    report.AppendLine("  - 使用HATCH命令为构件添加填充");
                    report.AppendLine("  - 确保构件图层处于打开状态");
                    report.AppendLine();
                }
                else
                {
                    // 几何类型分布
                    report.AppendLine("几何实体类型分布:");
                    var geometryStats = geometries.GroupBy(g => g.Type)
                        .Select(g => new
                        {
                            Type = g.Key,
                            Count = g.Count(),
                            TotalArea = g.Sum(x => x.Area),
                            TotalVolume = g.Sum(x => x.Volume)
                        })
                        .OrderByDescending(x => x.Count);

                    foreach (var stat in geometryStats)
                    {
                        report.AppendLine($"  - {stat.Type}: {stat.Count}个 " +
                            $"(总面积: {stat.TotalArea:F2}m², 总体积: {stat.TotalVolume:F3}m³)");
                    }
                    report.AppendLine();

                    // 按图层分组
                    report.AppendLine("几何实体按图层分布:");
                    var geometryByLayer = geometries.GroupBy(g => g.Layer)
                        .Select(g => new
                        {
                            Layer = g.Key,
                            Count = g.Count(),
                            TotalArea = g.Sum(x => x.Area)
                        })
                        .OrderByDescending(x => x.Count)
                        .Take(10);

                    foreach (var stat in geometryByLayer)
                    {
                        report.AppendLine($"  - 图层[{stat.Layer}]: {stat.Count}个 (总面积: {stat.TotalArea:F2}m²)");
                    }
                    report.AppendLine();

                    // 显示面积最大的前5个几何实体
                    report.AppendLine("面积最大的前5个几何实体:");
                    var topGeometries = geometries.OrderByDescending(g => g.Area).Take(5);
                    int geoIndex = 0;
                    foreach (var geo in topGeometries)
                    {
                        geoIndex++;
                        report.AppendLine($"  {geoIndex}. [{geo.Type}] " +
                            $"面积={geo.Area:F2}m², 图层={geo.Layer}, " +
                            $"尺寸={geo.Length:F2}×{geo.Width:F2}m");
                    }
                    report.AppendLine();
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"❌ 几何实体提取失败：{ex.Message}");
                Log.Error(ex, "几何实体提取失败");
                return report.ToString();
            }

            // =======================================================================
            // 步骤3: 运行构件识别
            // =======================================================================
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("【步骤3】运行构件识别（基于文本解析）");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            List<ComponentRecognitionResult> components;
            try
            {
                components = _componentRecognizer.RecognizeFromTextEntitiesAsync(textEntities, useAiVerification: false).Result;
                report.AppendLine($"✅ 识别到构件总数: {components.Count}个");
                report.AppendLine();

                if (components.Count == 0)
                {
                    report.AppendLine("⚠️ 警告：没有识别到任何构件！");
                    report.AppendLine();
                    report.AppendLine("可能原因：");
                    report.AppendLine("  1. 图纸文字不符合构件命名规范（如：C30混凝土柱、HRB400钢筋）");
                    report.AppendLine("  2. 文字使用了非标准术语");
                    report.AppendLine("  3. 文字被过滤掉了（图层关闭等）");
                    report.AppendLine();
                }
                else
                {
                    // 构件类型分布
                    report.AppendLine("构件类型分布:");
                    var componentStats = components.GroupBy(c => c.Type)
                        .Select(g => new
                        {
                            Type = g.Key,
                            Count = g.Count(),
                            TotalArea = g.Sum(x => x.Area),
                            TotalVolume = g.Sum(x => x.Volume)
                        })
                        .OrderByDescending(x => x.Count);

                    foreach (var stat in componentStats)
                    {
                        report.AppendLine($"  - {stat.Type}: {stat.Count}个 " +
                            $"(总面积: {stat.TotalArea:F2}m², 总体积: {stat.TotalVolume:F3}m³)");
                    }
                    report.AppendLine();

                    // 显示前5个构件详情
                    report.AppendLine("前5个构件详情:");
                    int compIndex = 0;
                    foreach (var comp in components.Take(5))
                    {
                        compIndex++;
                        report.AppendLine($"  {compIndex}. {comp.Type}");
                        report.AppendLine($"     原始文本: {comp.OriginalText}");
                        report.AppendLine($"     尺寸: L={comp.Length:F2}m, W={comp.Width:F2}m, H={comp.Height:F2}m");
                        report.AppendLine($"     面积: {comp.Area:F2}m², 体积: {comp.Volume:F3}m³");
                        report.AppendLine($"     置信度: {comp.Confidence:P}, 状态: {comp.Status}");
                        report.AppendLine();
                    }

                    // =======================================================================
                    // 步骤4: 分析面积为0的构件
                    // =======================================================================
                    report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                    report.AppendLine("【步骤4】分析面积为0的构件");
                    report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

                    var zeroAreaComponents = components.Where(c => c.Area == 0 && c.Volume == 0).ToList();
                    report.AppendLine($"面积和体积均为0的构件数量: {zeroAreaComponents.Count}个 " +
                        $"(占比: {(100.0 * zeroAreaComponents.Count / components.Count):F1}%)");
                    report.AppendLine();

                    if (zeroAreaComponents.Count > 0)
                    {
                        report.AppendLine("⚠️ 发现问题：部分构件的面积/体积为0！");
                        report.AppendLine();
                        report.AppendLine("详细原因分析（前10个）:");

                        int zeroIndex = 0;
                        foreach (var comp in zeroAreaComponents.Take(10))
                        {
                            zeroIndex++;
                            report.AppendLine($"  {zeroIndex}. {comp.Type} - \"{comp.OriginalText}\"");

                            var reasons = new List<string>();

                            // 原因1：尺寸缺失
                            if (comp.Length == 0 && comp.Width == 0 && comp.Height == 0)
                            {
                                reasons.Add("文本中没有尺寸信息（如300×600）");
                            }
                            else if (comp.Length > 0 && comp.Width == 0)
                            {
                                reasons.Add($"只有长度{comp.Length:F2}m，缺少宽度");
                            }
                            else if (comp.Length > 0 && comp.Width > 0 && comp.Height == 0)
                            {
                                reasons.Add($"有长宽（{comp.Length:F2}×{comp.Width:F2}m），但没有匹配到几何实体");
                            }

                            // 原因2：没有匹配到几何实体
                            var nearbyGeometries = geometries.Where(g =>
                                g.Layer == comp.Layer &&
                                comp.Position.DistanceTo(g.Centroid) < 10.0 // 10米范围内
                            ).ToList();

                            if (nearbyGeometries.Count == 0)
                            {
                                reasons.Add($"图层[{comp.Layer}]附近没有几何实体（10m范围内）");
                            }
                            else
                            {
                                reasons.Add($"图层[{comp.Layer}]附近有{nearbyGeometries.Count}个几何实体，但匹配失败");
                                report.AppendLine($"     附近几何实体:");
                                foreach (var geo in nearbyGeometries.Take(3))
                                {
                                    double dist = comp.Position.DistanceTo(geo.Centroid);
                                    report.AppendLine($"       - [{geo.Type}] 距离={dist:F2}m, 面积={geo.Area:F2}m²");
                                }
                            }

                            foreach (var reason in reasons)
                            {
                                report.AppendLine($"     原因: {reason}");
                            }
                            report.AppendLine();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                report.AppendLine($"❌ 构件识别失败：{ex.Message}");
                Log.Error(ex, "构件识别失败");
                return report.ToString();
            }

            // =======================================================================
            // 步骤5: 总结和建议
            // =======================================================================
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("【步骤5】诊断总结和建议");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            var hasGeometry = geometries.Count > 0;
            var hasComponents = components.Count > 0;
            var hasZeroAreaIssue = components.Any(c => c.Area == 0 && c.Volume == 0);

            if (!hasGeometry)
            {
                report.AppendLine("❌ 关键问题：图纸中没有可计算面积的几何实体");
                report.AppendLine();
                report.AppendLine("解决方案：");
                report.AppendLine("  1. 【推荐】使用AutoCAD的BOUNDARY命令将构件轮廓转为闭合Polyline");
                report.AppendLine("     - 命令: BOUNDARY");
                report.AppendLine("     - 点击构件内部区域");
                report.AppendLine("     - 生成闭合Polyline边界");
                report.AppendLine();
                report.AppendLine("  2. 使用HATCH命令为构件添加填充");
                report.AppendLine("     - 命令: HATCH");
                report.AppendLine("     - 选择构件边界");
                report.AppendLine("     - 插件可提取Hatch面积");
                report.AppendLine();
                report.AppendLine("  3. 确保构件图层处于打开和解冻状态");
                report.AppendLine();
            }
            else if (!hasComponents)
            {
                report.AppendLine("❌ 关键问题：文本识别失败，没有识别到构件");
                report.AppendLine();
                report.AppendLine("解决方案：");
                report.AppendLine("  1. 确保图纸文字使用标准构件命名");
                report.AppendLine("     - 正确示例: C30混凝土柱、HRB400钢筋、300×600梁");
                report.AppendLine("     - 错误示例: 柱子、钢筋、梁（太模糊）");
                report.AppendLine();
                report.AppendLine("  2. 检查文字图层是否被关闭");
                report.AppendLine();
            }
            else if (hasZeroAreaIssue)
            {
                report.AppendLine("⚠️ 主要问题：构件识别成功，但面积/体积为0");
                report.AppendLine();
                report.AppendLine("解决方案：");
                report.AppendLine("  1. 文本中添加尺寸信息");
                report.AppendLine("     - 格式: 300×600×2400, 240厚, Φ20");
                report.AppendLine();
                report.AppendLine("  2. 使用BOUNDARY/HATCH为构件创建几何实体");
                report.AppendLine("     - 几何实体与文本需在同一图层或相邻位置（<5m）");
                report.AppendLine();
                report.AppendLine("  3. 调整匹配参数");
                report.AppendLine("     - 当前匹配距离阈值: 5m");
                report.AppendLine("     - 如果标注距离构件较远，需要放宽阈值");
                report.AppendLine();
            }
            else
            {
                report.AppendLine("✅ 诊断通过：算量功能正常");
                report.AppendLine();
                report.AppendLine($"  - 几何实体: {geometries.Count}个");
                report.AppendLine($"  - 识别构件: {components.Count}个");
                report.AppendLine($"  - 总面积: {components.Sum(c => c.Area):F2}m²");
                report.AppendLine($"  - 总体积: {components.Sum(c => c.Volume):F3}m³");
                report.AppendLine();
            }

            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("诊断完成");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            return report.ToString();
        }
    }
}
