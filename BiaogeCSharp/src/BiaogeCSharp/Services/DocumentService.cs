using BiaogeCSharp.Models;
using System;

namespace BiaogeCSharp.Services;

/// <summary>
/// 文档服务 - 管理当前打开的DWG文档
/// </summary>
public class DocumentService
{
    private DwgDocument? _currentDocument;

    /// <summary>
    /// 当前打开的文档
    /// </summary>
    public DwgDocument? CurrentDocument
    {
        get => _currentDocument;
        set
        {
            _currentDocument = value;
            DocumentChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// 文档改变事件
    /// </summary>
    public event EventHandler<DwgDocument?>? DocumentChanged;

    /// <summary>
    /// 检查是否有打开的文档
    /// </summary>
    public bool HasDocument => _currentDocument != null;
}
