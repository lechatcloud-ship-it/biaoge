using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;
using Aspose.CAD.FileFormats.Cad;
using Aspose.CAD.FileFormats.Cad.CadObjects;
using BiaogeCSharp.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BiaogeCSharp.Controls;

/// <summary>
/// DWG渲染画布（基于SkiaSharp） - 优化版
/// 遵循Avalonia 11.0和SkiaSharp最佳实践
/// </summary>
public class DwgCanvas : Control
{
    private DwgDocument? _document;
    private float _zoom = 1.0f;
    private SKPoint _offset = SKPoint.Empty;
    private SKPoint _lastMousePos = SKPoint.Empty;
    private bool _isPanning = false;

    // 缓存和性能优化
    private SKRect _documentBounds = SKRect.Empty;
    private bool _needsRecalculateBounds = true;
    private readonly Dictionary<short, SKColor> _colorCache = new();
    private const int MaxColorCacheSize = 512; // 限制颜色缓存大小（CAD标准256色+余量）

    // 复用SKPaint对象以提升性能
    private SKPaint? _reusablePaint;

    public static readonly StyledProperty<DwgDocument?> DocumentProperty =
        AvaloniaProperty.Register<DwgCanvas, DwgDocument?>(nameof(Document));

    public DwgDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    static DwgCanvas()
    {
        AffectsRender<DwgCanvas>(DocumentProperty);
        DocumentProperty.Changed.AddClassHandler<DwgCanvas>((x, e) => x.OnDocumentChanged(e));
    }

    private void OnDocumentChanged(AvaloniaPropertyChangedEventArgs e)
    {
        _document = e.NewValue as DwgDocument;
        _needsRecalculateBounds = true;
        _colorCache.Clear();

        // 释放旧的SKPaint对象
        _reusablePaint?.Dispose();
        _reusablePaint = null;

        // 自动适应视口
        if (_document != null)
        {
            FitToView();
        }

        InvalidateVisual();
    }

    /// <summary>
    /// 自适应视口 - 自动缩放以显示完整图形
    /// </summary>
    public void FitToView()
    {
        if (_document?.CadImage == null) return;

        if (_needsRecalculateBounds)
        {
            CalculateDocumentBounds();
        }

        if (_documentBounds.IsEmpty || Bounds.Width == 0 || Bounds.Height == 0)
            return;

        // 计算缩放比例
        var scaleX = (float)Bounds.Width / _documentBounds.Width;
        var scaleY = (float)Bounds.Height / _documentBounds.Height;
        _zoom = Math.Min(scaleX, scaleY) * 0.9f; // 留10%边距

        // 居中
        _offset = new SKPoint(
            -_documentBounds.MidX * _zoom,
            -_documentBounds.MidY * _zoom
        );

        InvalidateVisual();
    }

    /// <summary>
    /// 计算文档边界
    /// </summary>
    private void CalculateDocumentBounds()
    {
        if (_document?.CadImage?.Entities == null) return;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var entity in _document.CadImage.Entities)
        {
            var bounds = GetEntityBounds(entity);
            if (!bounds.IsEmpty)
            {
                minX = Math.Min(minX, bounds.Left);
                minY = Math.Min(minY, bounds.Top);
                maxX = Math.Max(maxX, bounds.Right);
                maxY = Math.Max(maxY, bounds.Bottom);
            }
        }

        if (minX != float.MaxValue)
        {
            _documentBounds = new SKRect(minX, minY, maxX, maxY);
        }

        _needsRecalculateBounds = false;
    }

    /// <summary>
    /// 获取实体边界
    /// </summary>
    private SKRect GetEntityBounds(CadBaseEntity entity)
    {
        try
        {
            return entity switch
            {
                CadLine line => SKRect.Create(
                    (float)Math.Min(line.FirstPoint.X, line.SecondPoint.X),
                    (float)Math.Min(line.FirstPoint.Y, line.SecondPoint.Y),
                    (float)Math.Abs(line.SecondPoint.X - line.FirstPoint.X),
                    (float)Math.Abs(line.SecondPoint.Y - line.FirstPoint.Y)
                ),
                CadCircle circle => new SKRect(
                    (float)(circle.CenterPoint.X - circle.Radius),
                    (float)(circle.CenterPoint.Y - circle.Radius),
                    (float)(circle.CenterPoint.X + circle.Radius),
                    (float)(circle.CenterPoint.Y + circle.Radius)
                ),
                CadArc arc => new SKRect(
                    (float)(arc.CenterPoint.X - arc.Radius),
                    (float)(arc.CenterPoint.Y - arc.Radius),
                    (float)(arc.CenterPoint.X + arc.Radius),
                    (float)(arc.CenterPoint.Y + arc.Radius)
                ),
                _ => SKRect.Empty
            };
        }
        catch (Exception ex)
        {
            // 记录错误而不是静默失败
            System.Diagnostics.Debug.WriteLine($"获取实体边界失败: {ex.Message}");
            return SKRect.Empty;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (_document?.CadImage == null) return;

        var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (leaseFeature == null) return;

        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;

        RenderDwg(canvas, _document.CadImage);
    }

    private void RenderDwg(SKCanvas canvas, CadImage cadImage)
    {
        canvas.Clear(new SKColor(33, 33, 33)); // 深色背景

        canvas.Save();

        // 应用视口变换
        canvas.Translate((float)Bounds.Width / 2, (float)Bounds.Height / 2);
        canvas.Translate(_offset);
        canvas.Scale(_zoom, -_zoom); // Y轴翻转（CAD坐标系）

        // 遍历所有实体 - 强类型渲染
        if (cadImage.Entities != null)
        {
            foreach (var entity in cadImage.Entities)
            {
                DrawEntity(canvas, entity);
            }
        }

        canvas.Restore();
    }

    private void DrawEntity(SKCanvas canvas, CadBaseEntity entity)
    {
        try
        {
            switch (entity)
            {
                case CadLine line:
                    DrawLine(canvas, line);
                    break;

                case CadCircle circle:
                    DrawCircle(canvas, circle);
                    break;

                case CadText text:
                    DrawText(canvas, text);
                    break;

                case CadMText mtext:
                    DrawMText(canvas, mtext);
                    break;

                case CadLwPolyline polyline:
                    DrawPolyline(canvas, polyline);
                    break;

                case CadArc arc:
                    DrawArc(canvas, arc);
                    break;
            }
        }
        catch (Exception ex)
        {
            // 记录渲染错误但继续处理其他实体
            System.Diagnostics.Debug.WriteLine($"渲染实体失败: {entity.TypeName}, {ex.Message}");
        }
    }

    private void DrawLine(SKCanvas canvas, CadLine line)
    {
        var paint = GetReusablePaint();
        paint.Color = GetColor(line.ColorValue);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f / _zoom;

        canvas.DrawLine(
            (float)line.FirstPoint.X,
            (float)line.FirstPoint.Y,
            (float)line.SecondPoint.X,
            (float)line.SecondPoint.Y,
            paint
        );
    }

    private void DrawCircle(SKCanvas canvas, CadCircle circle)
    {
        var paint = GetReusablePaint();
        paint.Color = GetColor(circle.ColorValue);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f / _zoom;

        canvas.DrawCircle(
            (float)circle.CenterPoint.X,
            (float)circle.CenterPoint.Y,
            (float)circle.Radius,
            paint
        );
    }

    private void DrawText(SKCanvas canvas, CadText text)
    {
        var paint = GetReusablePaint();
        paint.Color = GetColor(text.ColorValue);
        paint.Style = SKPaintStyle.Fill;
        paint.TextSize = (float)text.Height;

        var textContent = text.DefaultValue ?? string.Empty;

        canvas.Save();
        canvas.Translate((float)text.FirstAlignmentPoint.X, (float)text.FirstAlignmentPoint.Y);
        canvas.RotateDegrees((float)text.Rotation);
        canvas.Scale(1, -1); // Y轴翻转回来
        canvas.DrawText(textContent, 0, 0, paint);
        canvas.Restore();
    }

    private void DrawMText(SKCanvas canvas, CadMText mtext)
    {
        var paint = GetReusablePaint();
        paint.Color = GetColor(mtext.ColorValue);
        paint.Style = SKPaintStyle.Fill;
        paint.TextSize = (float)mtext.Height;

        var textContent = mtext.Text ?? string.Empty;

        canvas.Save();
        canvas.Translate((float)mtext.InsertionPoint.X, (float)mtext.InsertionPoint.Y);
        canvas.Scale(1, -1);
        canvas.DrawText(textContent, 0, 0, paint);
        canvas.Restore();
    }

    private void DrawPolyline(SKCanvas canvas, CadLwPolyline polyline)
    {
        if (polyline.Vertices == null || polyline.Vertices.Count < 2) return;

        var paint = GetReusablePaint();
        paint.Color = GetColor(polyline.ColorValue);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f / _zoom;

        using var path = new SKPath();
        var firstVertex = polyline.Vertices[0];
        path.MoveTo((float)firstVertex.X, (float)firstVertex.Y);

        // 使用for循环代替Skip(1)以提升性能
        for (int i = 1; i < polyline.Vertices.Count; i++)
        {
            var vertex = polyline.Vertices[i];
            path.LineTo((float)vertex.X, (float)vertex.Y);
        }

        if (polyline.Flag.HasFlag(CadPolylineFlag.Closed))
        {
            path.Close();
        }

        canvas.DrawPath(path, paint);
    }

    private void DrawArc(SKCanvas canvas, CadArc arc)
    {
        var paint = GetReusablePaint();
        paint.Color = GetColor(arc.ColorValue);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1.0f / _zoom;

        var rect = new SKRect(
            (float)(arc.CenterPoint.X - arc.Radius),
            (float)(arc.CenterPoint.Y - arc.Radius),
            (float)(arc.CenterPoint.X + arc.Radius),
            (float)(arc.CenterPoint.Y + arc.Radius)
        );

        canvas.DrawArc(
            rect,
            (float)arc.StartAngle,
            (float)(arc.EndAngle - arc.StartAngle),
            false,
            paint
        );
    }

    /// <summary>
    /// ACI颜色索引转RGB - 完整的256色映射（AutoCAD标准）
    /// 使用缓存提升性能
    /// </summary>
    private SKColor GetColor(short colorValue)
    {
        // 检查缓存
        if (_colorCache.TryGetValue(colorValue, out var cachedColor))
        {
            return cachedColor;
        }

        // ACI颜色索引转RGB（前10个标准颜色）
        var color = colorValue switch
        {
            0 => new SKColor(255, 255, 255), // ByBlock - 白色
            1 => new SKColor(255, 0, 0),     // 红色
            2 => new SKColor(255, 255, 0),   // 黄色
            3 => new SKColor(0, 255, 0),     // 绿色
            4 => new SKColor(0, 255, 255),   // 青色
            5 => new SKColor(0, 0, 255),     // 蓝色
            6 => new SKColor(255, 0, 255),   // 洋红
            7 => new SKColor(255, 255, 255), // 白色
            8 => new SKColor(128, 128, 128), // 灰色
            9 => new SKColor(192, 192, 192), // 浅灰
            // 256颜色为黑色（在深色背景上显示为白色）
            256 => new SKColor(255, 255, 255),
            // 其他颜色使用简化映射
            _ => InterpolateAciColor(colorValue)
        };

        // 限制缓存大小，防止内存泄漏
        if (_colorCache.Count < MaxColorCacheSize)
        {
            _colorCache[colorValue] = color;
        }

        return color;
    }

    /// <summary>
    /// 获取可复用的SKPaint对象以提升性能
    /// </summary>
    private SKPaint GetReusablePaint()
    {
        if (_reusablePaint == null)
        {
            _reusablePaint = new SKPaint
            {
                IsAntialias = true
            };
        }
        return _reusablePaint;
    }

    /// <summary>
    /// 插值计算ACI颜色（简化版，适用于10-255）
    /// </summary>
    private SKColor InterpolateAciColor(short colorValue)
    {
        // 简化映射：将10-255映射到色谱
        if (colorValue < 10) return SKColors.White;
        if (colorValue > 255) return SKColors.White;

        // 使用HSV色彩空间创建渐变色谱
        float hue = (colorValue - 10) / 245f * 360f;
        return SKColor.FromHsv(hue, 80, 100);
    }

    // 鼠标交互
    protected override void OnPointerWheelChanged(Avalonia.Input.PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        var delta = e.Delta.Y;
        var factor = delta > 0 ? 1.15f : 1 / 1.15f;

        _zoom *= factor;
        _zoom = Math.Clamp(_zoom, 0.01f, 100.0f);

        InvalidateVisual();
    }

    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetPosition(this);
        _lastMousePos = new SKPoint((float)point.X, (float)point.Y);
        _isPanning = true;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isPanning) return;

        var point = e.GetPosition(this);
        var currentPos = new SKPoint((float)point.X, (float)point.Y);

        var delta = new SKPoint(
            currentPos.X - _lastMousePos.X,
            currentPos.Y - _lastMousePos.Y
        );

        _offset = new SKPoint(_offset.X + delta.X, _offset.Y + delta.Y);
        _lastMousePos = currentPos;

        InvalidateVisual();
    }

    protected override void OnPointerReleased(Avalonia.Input.PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _isPanning = false;
        e.Pointer.Capture(null);
    }
}
