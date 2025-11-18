using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using BiaogPlugin.Services;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// ✅ 扣减关系处理服务 - 实现GB 50854-2013规范的扣减规则
    ///
    /// 核心扣减规则：
    /// 1. 板扣减柱、墙占用的面积
    /// 2. 墙体扣减门窗洞口的体积
    /// 3. 梁柱交接：混凝土计入梁，梁不扣减柱（GB规范）
    /// 4. 墙体孔洞：单个孔洞面积>0.3m²时扣减
    ///
    /// 参考：
    /// - GB 50854-2013《建筑工程工程量清单计价规范》
    /// - GB 50500-2013《建设工程工程量清单计价规范》
    /// </summary>
    public class DeductionService
    {
        private const double DEDUCTION_TOLERANCE = 0.001;  // 扣减容差（1mm）
        private const double MIN_OPENING_AREA = 0.3;       // 最小扣减孔洞面积（m²）

        public DeductionService()
        {
        }

        /// <summary>
        /// 应用所有扣减规则到构件列表
        /// </summary>
        public void ApplyDeductions(List<ComponentRecognitionResult> components)
        {
            if (components == null || components.Count == 0)
            {
                return;
            }

            Log.Information("════════════════════════════════════════");
            Log.Information("开始应用扣减关系（GB 50854-2013规范）");
            Log.Information($"构件总数: {components.Count}");
            Log.Information("════════════════════════════════════════");

            // 按类型分组
            var columns = components.Where(c => c.Type.Contains("柱")).ToList();
            var beams = components.Where(c => c.Type.Contains("梁")).ToList();
            var slabs = components.Where(c => c.Type.Contains("板")).ToList();
            var walls = components.Where(c => c.Type.Contains("墙")).ToList();
            var doors = components.Where(c => c.Type.Contains("门")).ToList();
            var windows = components.Where(c => c.Type.Contains("窗")).ToList();

            Log.Debug($"分类统计: 柱{columns.Count}, 梁{beams.Count}, 板{slabs.Count}, " +
                     $"墙{walls.Count}, 门{doors.Count}, 窗{windows.Count}");

            int totalDeductions = 0;

            // 规则1：板扣减柱、墙占用面积
            totalDeductions += DeductColumnsAndWallsFromSlabs(slabs, columns, walls);

            // 规则2：墙体扣减门窗洞口
            totalDeductions += DeductOpeningsFromWalls(walls, doors, windows);

            // 规则3：梁柱交接处理（混凝土计入梁）
            // 注意：这不是扣减，而是确保梁柱交接处混凝土只计算一次（计入梁）
            totalDeductions += HandleBeamColumnJoints(beams, columns);

            Log.Information($"✅ 扣减处理完成：共应用{totalDeductions}次扣减");
            Log.Information("════════════════════════════════════════");
        }

        /// <summary>
        /// 规则1：板扣减柱、墙占用面积
        ///
        /// GB 50854-2013规定：
        /// - 现浇板按设计图示尺寸以体积计算
        /// - 扣除墙体所占体积（板算至墙侧）
        /// - 扣除单个面积>0.3m²的柱所占体积
        /// - 不扣除单个面积≤0.3m²的柱、垛、孔洞
        /// </summary>
        private int DeductColumnsAndWallsFromSlabs(
            List<ComponentRecognitionResult> slabs,
            List<ComponentRecognitionResult> columns,
            List<ComponentRecognitionResult> walls)
        {
            int deductionCount = 0;

            foreach (var slab in slabs)
            {
                if (slab.Area <= DEDUCTION_TOLERANCE)
                {
                    continue;
                }

                double totalDeductedArea = 0;

                // 扣减柱占用面积（GB规范：仅扣减截面积>0.3m²的柱）
                foreach (var column in columns)
                {
                    // 判断柱是否在板平面内（简化：同一图层或Z坐标相近）
                    if (IsSpatiallyRelated(slab, column))
                    {
                        // 柱截面积 = Length × Width
                        double columnArea = column.Length * column.Width;

                        // GB 50854-2013: 仅扣减单个面积>0.3m²的柱
                        if (columnArea > MIN_OPENING_AREA)
                        {
                            totalDeductedArea += columnArea * slab.Quantity;
                            deductionCount++;

                            Log.Debug($"  板[{slab.Type}]扣减柱[{column.Type}]面积: -{columnArea:F3}m² (>{MIN_OPENING_AREA}m²)");
                        }
                        else
                        {
                            Log.Debug($"  板[{slab.Type}]不扣减柱[{column.Type}]: 面积{columnArea:F3}m² ≤ {MIN_OPENING_AREA}m²");
                        }
                    }
                }

                // 扣减墙占用面积
                foreach (var wall in walls)
                {
                    if (IsSpatiallyRelated(slab, wall))
                    {
                        // 墙截面积 = 长度 × 厚度（墙的Width通常是厚度）
                        double wallArea = wall.Length * wall.Width;

                        if (wallArea > DEDUCTION_TOLERANCE)
                        {
                            totalDeductedArea += wallArea * slab.Quantity;
                            deductionCount++;

                            Log.Debug($"  板[{slab.Type}]扣减墙[{wall.Type}]面积: -{wallArea:F3}m²");
                        }
                    }
                }

                // 应用扣减
                if (totalDeductedArea > DEDUCTION_TOLERANCE)
                {
                    slab.Area = Math.Max(0, slab.Area - totalDeductedArea);
                    slab.Volume = Math.Max(0, slab.Volume - totalDeductedArea * slab.Height);

                    Log.Information($"✓ 板[{slab.Type}]总扣减面积: -{totalDeductedArea:F3}m², " +
                                  $"剩余面积: {slab.Area:F3}m²");
                }
            }

            return deductionCount;
        }

        /// <summary>
        /// 规则2：墙体扣减门窗洞口
        ///
        /// GB 50854-2013规定：
        /// - 墙体按设计图示尺寸以体积计算
        /// - 扣除门窗洞口所占体积
        /// - 不扣除单个面积≤0.3m²的孔洞
        /// - 门窗洞口侧壁面积并入墙体面积
        /// </summary>
        private int DeductOpeningsFromWalls(
            List<ComponentRecognitionResult> walls,
            List<ComponentRecognitionResult> doors,
            List<ComponentRecognitionResult> windows)
        {
            int deductionCount = 0;

            foreach (var wall in walls)
            {
                if (wall.Volume <= DEDUCTION_TOLERANCE)
                {
                    continue;
                }

                double totalDeductedVolume = 0;

                // 扣减门洞口
                foreach (var door in doors)
                {
                    if (IsSpatiallyRelated(wall, door))
                    {
                        // 门洞口体积 = 门面积 × 墙厚
                        double openingVolume = door.Area * wall.Width;

                        // GB规范：单个孔洞面积>0.3m²时才扣减
                        if (door.Area > MIN_OPENING_AREA && openingVolume > DEDUCTION_TOLERANCE)
                        {
                            totalDeductedVolume += openingVolume * door.Quantity;
                            deductionCount++;

                            Log.Debug($"  墙[{wall.Type}]扣减门[{door.Type}]体积: -{openingVolume:F3}m³ " +
                                    $"(面积{door.Area:F3}m² × 厚{wall.Width:F3}m)");
                        }
                    }
                }

                // 扣减窗洞口
                foreach (var window in windows)
                {
                    if (IsSpatiallyRelated(wall, window))
                    {
                        double openingVolume = window.Area * wall.Width;

                        if (window.Area > MIN_OPENING_AREA && openingVolume > DEDUCTION_TOLERANCE)
                        {
                            totalDeductedVolume += openingVolume * window.Quantity;
                            deductionCount++;

                            Log.Debug($"  墙[{wall.Type}]扣减窗[{window.Type}]体积: -{openingVolume:F3}m³");
                        }
                    }
                }

                // 应用扣减
                if (totalDeductedVolume > DEDUCTION_TOLERANCE)
                {
                    wall.Volume = Math.Max(0, wall.Volume - totalDeductedVolume);

                    Log.Information($"✓ 墙[{wall.Type}]总扣减体积: -{totalDeductedVolume:F3}m³, " +
                                  $"剩余体积: {wall.Volume:F3}m³");
                }
            }

            return deductionCount;
        }

        /// <summary>
        /// 规则3：梁柱交接处理
        ///
        /// GB 50854-2013规定：
        /// - 梁与柱连接时，梁长算至柱侧面
        /// - 梁与柱交接部分的混凝土计入梁体积内（不扣减）
        /// - 即：柱不扣减梁，梁也不扣减柱
        ///
        /// 注意：这里只是记录和验证，实际计算时已经按规范处理
        /// </summary>
        private int HandleBeamColumnJoints(
            List<ComponentRecognitionResult> beams,
            List<ComponentRecognitionResult> columns)
        {
            int jointCount = 0;

            foreach (var beam in beams)
            {
                foreach (var column in columns)
                {
                    // 判断梁柱是否相交
                    if (IsSpatiallyRelated(beam, column))
                    {
                        // 交接体积（用于记录，不扣减）
                        double jointVolume = Math.Min(beam.Width * beam.Height, column.Length * column.Width)
                                           * Math.Min(beam.Length, column.Height);

                        if (jointVolume > DEDUCTION_TOLERANCE)
                        {
                            jointCount++;

                            Log.Debug($"  检测到梁柱交接: 梁[{beam.Type}] × 柱[{column.Type}], " +
                                    $"交接体积约{jointVolume:F3}m³ (计入梁，不扣减)");
                        }
                    }
                }
            }

            if (jointCount > 0)
            {
                Log.Information($"✓ 检测到{jointCount}处梁柱交接（混凝土已计入梁，符合GB规范）");
            }

            return jointCount;
        }

        /// <summary>
        /// 判断两个构件是否在空间上相关（修正版）
        ///
        /// ❌ 修复前错误：同一图层直接返回true（1层和10层的柱子可能在同一图层，但相距很远！）
        /// ✅ 修复后正确：同一图层 AND 距离接近才返回true
        ///
        /// 判断依据（优先级从高到低）：
        /// 1. 同一图层 AND 平面距离<5m（大部分情况）
        /// 2. 不同图层 BUT 距离很近(<2m) AND Z坐标相近(<1m)（跨图层扣减）
        ///
        /// 注意：这是简化判断，完整实现需要3D几何相交检测（Bounding Box Intersection）
        /// </summary>
        private bool IsSpatiallyRelated(ComponentRecognitionResult c1, ComponentRecognitionResult c2)
        {
            // 预先计算距离和高程差（避免重复计算）
            double distance = c1.Position.DistanceTo(c2.Position);
            double zDiff = Math.Abs(c1.Position.Z - c2.Position.Z);

            // 判断1：同一图层 AND 距离接近（优先判断，覆盖大部分情况）
            // 原理：同一楼层的构件通常在同一图层，且距离较近
            if (c1.Layer == c2.Layer && distance < 5.0)
            {
                return true;
            }

            // 判断2：不同图层 BUT 距离很近 AND Z坐标相近（跨图层扣减）
            // 原理：某些情况下构件在不同图层，但实际上在空间中相交（例如跨图层的板和柱）
            // 使用更严格的条件：距离<2m（而非5m），确保只捕获真正相交的情况
            if (distance < 2.0 && zDiff < 1.0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 生成扣减关系报告（用于调试和验证）
        /// </summary>
        public string GenerateDeductionReport(List<ComponentRecognitionResult> components)
        {
            var report = new System.Text.StringBuilder();

            report.AppendLine("═══════════════════════════════════════════");
            report.AppendLine("         扣减关系处理报告");
            report.AppendLine("═══════════════════════════════════════════");
            report.AppendLine();

            // 统计扣减前后的工程量
            double totalArea = components.Sum(c => c.Area);
            double totalVolume = components.Sum(c => c.Volume);

            report.AppendLine($"扣减后总计：");
            report.AppendLine($"  - 总面积: {totalArea:F2}m²");
            report.AppendLine($"  - 总体积: {totalVolume:F3}m³");
            report.AppendLine();

            // 按类型汇总
            var grouped = components.GroupBy(c => c.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(),
                    TotalArea = g.Sum(c => c.Area),
                    TotalVolume = g.Sum(c => c.Volume)
                })
                .OrderByDescending(x => x.TotalVolume);

            report.AppendLine("按构件类型汇总：");
            foreach (var group in grouped)
            {
                report.AppendLine($"  {group.Type}:");
                report.AppendLine($"    数量: {group.Count}, " +
                                $"面积: {group.TotalArea:F2}m², " +
                                $"体积: {group.TotalVolume:F3}m³");
            }

            return report.ToString();
        }
    }
}
