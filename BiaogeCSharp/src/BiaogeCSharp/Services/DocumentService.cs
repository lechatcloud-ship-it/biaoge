using BiaogeCSharp.Models;
using System;

namespace BiaogeCSharp.Services;

/// <summary>
/// 文档服务 - 管理当前打开的DWG文档
/// 实现IDisposable以正确释放DWG文档资源
/// 线程安全的文档管理
/// </summary>
public class DocumentService : IDisposable
{
    private readonly object _documentLock = new();
    private DwgDocument? _currentDocument;
    private bool _disposed = false;

    /// <summary>
    /// 当前打开的文档（线程安全）
    /// </summary>
    public DwgDocument? CurrentDocument
    {
        get
        {
            lock (_documentLock)
            {
                return _currentDocument;
            }
        }
        set
        {
            DwgDocument? oldDoc = null;

            lock (_documentLock)
            {
                if (_currentDocument == value) return;
                oldDoc = _currentDocument;
                _currentDocument = value;
            }

            // 在锁外释放资源和触发事件，避免死锁
            oldDoc?.Dispose();
            DocumentChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// 文档改变事件
    /// </summary>
    public event EventHandler<DwgDocument?>? DocumentChanged;

    /// <summary>
    /// 检查是否有打开的文档（线程安全）
    /// </summary>
    public bool HasDocument
    {
        get
        {
            lock (_documentLock)
            {
                return _currentDocument != null;
            }
        }
    }

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
                lock (_documentLock)
                {
                    _currentDocument?.Dispose();
                    _currentDocument = null;
                }

                // 清理事件订阅，防止内存泄漏
                DocumentChanged = null;
            }

            _disposed = true;
        }
    }
}
