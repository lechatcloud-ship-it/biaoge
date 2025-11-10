using Aspose.CAD;
using Aspose.CAD.FileFormats.Cad;
using Aspose.CAD.FileFormats.Cad.CadObjects;
using Aspose.CAD.FileFormats.Cad.CadConsts;
using BiaogeCSharp.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BiaogeCSharp.Services;

/// <summary>
/// Aspose.CAD DWG解析器 - 使用官方最佳实践
/// 核心功能：解析DWG文件，提取文本，修改文本，保存文件
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
    /// 从CAD实体提取文本 - 使用TypeName和强类型转换（官方推荐）
    /// </summary>
    private string ExtractTextFromEntity(object entity)
    {
        try
        {
            if (!(entity is CadBaseEntity cadEntity))
                return string.Empty;

            // 使用TypeName属性检查实体类型（官方推荐方式）
            switch (cadEntity.TypeName)
            {
                case CadEntityTypeName.TEXT:
                    // CadText使用DefaultValue属性
                    if (entity is CadText cadText && !string.IsNullOrWhiteSpace(cadText.DefaultValue))
                    {
                        return cadText.DefaultValue.Trim();
                    }
                    break;

                case CadEntityTypeName.MTEXT:
                    // CadMText使用Text属性
                    if (entity is CadMText cadMText && !string.IsNullOrWhiteSpace(cadMText.Text))
                    {
                        return cadMText.Text.Trim();
                    }
                    break;

                case CadEntityTypeName.ATTRIB:
                    // CadAttrib（属性）也可能包含文本
                    if (entity is CadAttrib cadAttrib && !string.IsNullOrWhiteSpace(cadAttrib.DefaultValue))
                    {
                        return cadAttrib.DefaultValue.Trim();
                    }
                    break;

                case CadEntityTypeName.ATTDEF:
                    // CadAttDef（属性定义）
                    if (entity is CadAttDef cadAttDef && !string.IsNullOrWhiteSpace(cadAttDef.DefaultValue))
                    {
                        return cadAttDef.DefaultValue.Trim();
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "无法从实体提取文本");
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
    /// 获取实体的图层名 - 使用LayerName属性
    /// </summary>
    private string? GetEntityLayerName(object entity)
    {
        try
        {
            if (entity is CadBaseEntity cadEntity)
            {
                return cadEntity.LayerName ?? "0";
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "无法获取实体图层名");
        }

        return null;
    }

    /// <summary>
    /// 修改DWG文档中的文本（用于翻译） - 核心功能
    /// </summary>
    /// <param name="document">DWG文档</param>
    /// <param name="translations">翻译映射表（原文→译文）</param>
    /// <returns>修改的实体数量</returns>
    public int ApplyTranslations(DwgDocument document, Dictionary<string, string> translations)
    {
        _logger.LogInformation("开始应用翻译: {Count}条", translations.Count);

        int modifiedCount = 0;

        try
        {
            var cadImage = document.CadImage;

            foreach (var entity in cadImage.Entities)
            {
                if (!(entity is CadBaseEntity cadEntity))
                    continue;

                var originalText = ExtractTextFromEntity(entity);
                if (string.IsNullOrWhiteSpace(originalText))
                    continue;

                // 查找翻译
                if (!translations.TryGetValue(originalText, out var translatedText))
                    continue;

                // 应用翻译到对应的实体类型
                bool modified = false;

                switch (cadEntity.TypeName)
                {
                    case CadEntityTypeName.TEXT:
                        if (entity is CadText cadText)
                        {
                            cadText.DefaultValue = translatedText;
                            modified = true;
                        }
                        break;

                    case CadEntityTypeName.MTEXT:
                        if (entity is CadMText cadMText)
                        {
                            cadMText.Text = translatedText;
                            modified = true;
                        }
                        break;

                    case CadEntityTypeName.ATTRIB:
                        if (entity is CadAttrib cadAttrib)
                        {
                            cadAttrib.DefaultValue = translatedText;
                            modified = true;
                        }
                        break;

                    case CadEntityTypeName.ATTDEF:
                        if (entity is CadAttDef cadAttDef)
                        {
                            cadAttDef.DefaultValue = translatedText;
                            modified = true;
                        }
                        break;
                }

                if (modified)
                {
                    modifiedCount++;
                    _logger.LogDebug("文本已翻译: \"{Original}\" → \"{Translated}\"",
                        originalText, translatedText);
                }
            }

            _logger.LogInformation("翻译应用完成: {Count}个实体已修改", modifiedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "应用翻译失败");
            throw new Exception($"应用翻译失败: {ex.Message}", ex);
        }

        return modifiedCount;
    }

    /// <summary>
    /// 保存DWG文档（翻译后）
    /// </summary>
    /// <param name="document">DWG文档</param>
    /// <param name="outputPath">输出路径</param>
    public void SaveDocument(DwgDocument document, string outputPath)
    {
        _logger.LogInformation("保存DWG文档: {OutputPath}", outputPath);

        try
        {
            // 确保输出目录存在
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 保存文件
            document.CadImage.Save(outputPath);

            _logger.LogInformation("DWG文档保存成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存DWG文档失败");
            throw new Exception($"保存DWG文档失败: {ex.Message}", ex);
        }
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
