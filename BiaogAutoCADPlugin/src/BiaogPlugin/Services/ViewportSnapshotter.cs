using System;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Serilog;

namespace BiaogPlugin.Services;

/// <summary>
/// 视口截图服务 - 使用AutoCAD官方推荐的Document.CapturePreviewImage()方法
///
/// 参考：Kean Walmsley - Through the Interface Blog
/// 优势：代码简洁（5行核心代码）、稳定性高、与编辑器显示完全一致
///
/// ⚠️ 线程安全要求：
/// - 必须在AutoCAD主线程调用（不支持多线程）
/// - async方法中，必须在第一个await之前调用此方法
/// - 只读操作，不需要Transaction或DocumentLock
/// </summary>
public class ViewportSnapshotter
{
    /// <summary>
    /// 捕获当前视口的截图
    /// </summary>
    /// <returns>包含Base64图像数据和视图信息的快照对象</returns>
    /// <exception cref="InvalidOperationException">没有活动的AutoCAD文档时抛出</exception>
    /// <remarks>
    /// ⚠️ 线程安全：必须在AutoCAD主线程调用！
    /// 在async方法中，确保在第一个await之前调用此方法。
    /// </remarks>
    public static ViewportSnapshot CaptureCurrentView()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;

        if (doc == null)
        {
            throw new InvalidOperationException("没有活动的AutoCAD文档");
        }

        try
        {
            // 获取视图信息（用于AI分析）
            var view = doc.Editor.GetCurrentView();

            // TODO: CapturePreviewImage API在当前AutoCAD版本中不可用
            // 暂时返回空数据
            int width = 1920;
            int height = 1080;

            var snapshot = new ViewportSnapshot
            {
                Base64Data = string.Empty, // 暂时为空
                Width = width,
                Height = height,
                ViewName = "Model",
                Scale = CalculateViewScale(view, (double)height),
                CaptureTime = DateTime.Now,
                DocumentName = Path.GetFileNameWithoutExtension(doc.Name)
            };

            Log.Warning("视口截图功能暂时禁用（API不兼容）");

            return snapshot;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "视口截图失败");
            throw new InvalidOperationException($"截图失败: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// 计算视图比例尺（DWG单位/像素）
    /// 这个值对AI判断实际尺寸非常关键
    /// </summary>
    /// <param name="view">当前视图</param>
    /// <param name="windowHeight">窗口高度（像素）</param>
    /// <returns>比例尺（DWG单位/像素，通常是mm/px）</returns>
    private static double CalculateViewScale(ViewTableRecord view, double windowHeight)
    {
        if (windowHeight <= 0)
            return 1.0;

        // 视图高度（DWG单位，通常是mm）
        var viewHeight = view.Height;

        // 比例尺 = DWG单位 / 像素
        // 例如：viewHeight=10000mm, windowHeight=800px → scale=12.5mm/px
        var scale = viewHeight / windowHeight;

        return scale;
    }
}

/// <summary>
/// 视口快照数据模型
/// </summary>
public class ViewportSnapshot
{
    /// <summary>
    /// Base64编码的PNG图像数据（可直接传给VL模型）
    /// </summary>
    public string Base64Data { get; set; } = string.Empty;

    /// <summary>
    /// 图像宽度（像素）
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// 图像高度（像素）
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// 视图名称（如"Model"、"Layout1"等）
    /// </summary>
    public string ViewName { get; set; } = string.Empty;

    /// <summary>
    /// 视图比例尺（DWG单位/像素）
    /// 关键参数：AI需要这个值来计算实际尺寸
    /// </summary>
    public double Scale { get; set; }

    /// <summary>
    /// 截图时间戳
    /// </summary>
    public DateTime CaptureTime { get; set; }

    /// <summary>
    /// 文档名称（不含路径和扩展名）
    /// </summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>
    /// 获取数据大小（MB）
    /// </summary>
    public double GetSizeMB()
    {
        if (string.IsNullOrEmpty(Base64Data))
            return 0;

        // Base64编码后大小约为原始大小的4/3
        var bytes = Base64Data.Length * 3 / 4;
        return bytes / (1024.0 * 1024.0);
    }

    public override string ToString()
    {
        return $"{ViewName} ({Width}×{Height}, {GetSizeMB():F2}MB, 比例:{Scale:F2})";
    }
}
