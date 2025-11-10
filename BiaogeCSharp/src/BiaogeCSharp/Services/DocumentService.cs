using BiaogeCSharp.Models;
using System;

namespace BiaogeCSharp.Services;

/// <summary>
/// 文档服务 - 管理当前打开的DWG文档
/// 实现IDisposable以正确释放DWG文档资源
/// </summary>
public class DocumentService : IDisposable
{
    private DwgDocument? _currentDocument;
    private bool _disposed = false;

    /// <summary>
    /// 当前打开的文档
    /// </summary>
    public DwgDocument? CurrentDocument
    {
        get => _currentDocument;
        set
        {
            // 释放旧文档资源
            _currentDocument?.Dispose();

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

    /// <summary>
    /// 释放文档资源
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _currentDocument?.Dispose();
                _currentDocument = null;
            }

            _disposed = true;
        }
    }
}
