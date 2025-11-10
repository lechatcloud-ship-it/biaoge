using Aspose.CAD;
using Aspose.CAD.FileFormats.Cad;
using BiaogeCSharp.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;

namespace BiaogeCSharp.Services;

/// <summary>
/// Aspose.CAD DWG解析器
/// </summary>
public class AsposeDwgParser
{
    private readonly ILogger<AsposeDwgParser> _logger;

    public AsposeDwgParser(ILogger<AsposeDwgParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 解析DWG文件
    /// </summary>
    public DwgDocument Parse(string filePath)
    {
        _logger.LogInformation("开始解析DWG文件: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"文件不存在: {filePath}");
        }

        try
        {
            // 使用Aspose.CAD加载DWG文件
            var cadImage = (CadImage)Image.Load(filePath);

            var document = new DwgDocument
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                CadImage = cadImage
            };

            // 解析图层信息
            if (cadImage.Layers != null)
            {
                foreach (var layer in cadImage.Layers)
                {
                    document.Layers.Add(new LayerInfo
                    {
                        Name = layer.Name ?? "0",
                        IsVisible = !layer.IsOff,
                        IsLocked = layer.IsLocked
                    });
                }
            }

            // 元数据
            document.Metadata["Version"] = cadImage.DxfVersion?.ToString() ?? "Unknown";
            document.Metadata["Width"] = cadImage.Width;
            document.Metadata["Height"] = cadImage.Height;

            _logger.LogInformation(
                "DWG解析完成: {EntityCount}个实体, {LayerCount}个图层",
                document.EntityCount,
                document.Layers.Count
            );

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DWG解析失败: {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// 从DWG文档提取所有文本
    /// </summary>
    public List<string> ExtractTexts(DwgDocument document)
    {
        var texts = new List<string>();

        _logger.LogInformation("开始提取DWG文本");

        try
        {
            if (document.CadImage?.Entities != null)
            {
                foreach (var entity in document.CadImage.Entities)
                {
                    var text = ExtractTextFromEntity(entity);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        texts.Add(text);
                    }
                }
            }

            _logger.LogInformation("提取完成: {Count}条文本", texts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文本提取失败");
        }

        return texts;
    }

    /// <summary>
    /// 从CAD实体提取文本
    /// </summary>
    private string ExtractTextFromEntity(object entity)
    {
        try
        {
            var type = entity.GetType();

            // 尝试获取Text属性（适用于CadText, CadMText等）
            var textProperty = type.GetProperty("Text") ?? type.GetProperty("DefaultValue");
            if (textProperty != null)
            {
                var text = textProperty.GetValue(entity)?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }

            // 尝试获取TextValue属性
            var textValueProperty = type.GetProperty("TextValue");
            if (textValueProperty != null)
            {
                var text = textValueProperty.GetValue(entity)?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "无法从实体提取文本: {EntityType}", entity.GetType().Name);
        }

        return string.Empty;
    }

    /// <summary>
    /// 按图层提取文本
    /// </summary>
    public Dictionary<string, List<string>> ExtractTextsByLayer(DwgDocument document)
    {
        var textsByLayer = new Dictionary<string, List<string>>();

        _logger.LogInformation("按图层提取文本");

        try
        {
            if (document.CadImage?.Entities != null)
            {
                foreach (var entity in document.CadImage.Entities)
                {
                    // 获取图层名
                    var layerName = GetEntityLayerName(entity) ?? "0";

                    // 提取文本
                    var text = ExtractTextFromEntity(entity);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (!textsByLayer.ContainsKey(layerName))
                        {
                            textsByLayer[layerName] = new List<string>();
                        }

                        textsByLayer[layerName].Add(text);
                    }
                }
            }

            _logger.LogInformation("提取完成: {LayerCount}个图层", textsByLayer.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "按图层提取文本失败");
        }

        return textsByLayer;
    }

    /// <summary>
    /// 获取实体的图层名
    /// </summary>
    private string? GetEntityLayerName(object entity)
    {
        try
        {
            var type = entity.GetType();
            var layerProperty = type.GetProperty("LayerName") ?? type.GetProperty("Layer");

            if (layerProperty != null)
            {
                return layerProperty.GetValue(entity)?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "无法获取实体图层名");
        }

        return null;
    }

    /// <summary>
    /// 获取文本实体的位置信息
    /// </summary>
    public List<TextEntity> ExtractTextEntitiesWithPosition(DwgDocument document)
    {
        var textEntities = new List<TextEntity>();

        _logger.LogInformation("提取带位置的文本实体");

        try
        {
            if (document.CadImage?.Entities != null)
            {
                foreach (var entity in document.CadImage.Entities)
                {
                    var text = ExtractTextFromEntity(entity);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var textEntity = new TextEntity
                        {
                            Text = text,
                            LayerName = GetEntityLayerName(entity) ?? "0",
                            Position = GetEntityPosition(entity)
                        };

                        textEntities.Add(textEntity);
                    }
                }
            }

            _logger.LogInformation("提取完成: {Count}个文本实体", textEntities.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提取文本实体失败");
        }

        return textEntities;
    }

    /// <summary>
    /// 获取实体位置
    /// </summary>
    private (double X, double Y, double Z) GetEntityPosition(object entity)
    {
        try
        {
            var type = entity.GetType();

            // 尝试获取InsertionPoint或Position属性
            var positionProperty = type.GetProperty("InsertionPoint") ?? type.GetProperty("Position");

            if (positionProperty != null)
            {
                var position = positionProperty.GetValue(entity);
                if (position != null)
                {
                    var posType = position.GetType();
                    var x = (double)(posType.GetProperty("X")?.GetValue(position) ?? 0.0);
                    var y = (double)(posType.GetProperty("Y")?.GetValue(position) ?? 0.0);
                    var z = (double)(posType.GetProperty("Z")?.GetValue(position) ?? 0.0);

                    return (x, y, z);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "无法获取实体位置");
        }

        return (0, 0, 0);
    }
}

/// <summary>
/// 文本实体数据
/// </summary>
public class TextEntity
{
    public string Text { get; set; } = string.Empty;
    public string LayerName { get; set; } = string.Empty;
    public (double X, double Y, double Z) Position { get; set; }
}
