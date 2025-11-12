using System;
using System.Threading.Tasks;
using Autodesk.AutoCAD.ApplicationServices;
using Serilog;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 命令全局异常处理器
    /// ✅ 优化：防止未捕获异常导致AutoCAD崩溃
    /// </summary>
    public static class CommandExceptionHandler
    {
        /// <summary>
        /// 安全执行异步命令（async void包装器）
        /// </summary>
        /// <param name="action">要执行的异步操作</param>
        /// <param name="commandName">命令名称（用于日志）</param>
        public static async void ExecuteSafely(Func<Task> action, string commandName)
        {
            try
            {
                await action();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception acEx)
            {
                // AutoCAD特定异常
                Log.Error(acEx, "AutoCAD命令执行失败: {CommandName}, ErrorStatus: {ErrorStatus}",
                    commandName, acEx.ErrorStatus);

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n[错误] {commandName} 执行失败: {acEx.Message}");
                    doc.Editor.WriteMessage("\n提示: 请查看日志文件以获取详细信息");
                }
            }
            catch (System.OperationCanceledException)
            {
                // 用户取消操作，这是正常情况
                Log.Information("用户取消命令: {CommandName}", commandName);

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n[提示] {commandName} 已取消");
                }
            }
            catch (Exception ex)
            {
                // 所有其他未预期异常
                Log.Fatal(ex, "命令执行时发生未处理的异常: {CommandName}", commandName);

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n[严重错误] {commandName} 执行失败");
                    doc.Editor.WriteMessage($"\n错误类型: {ex.GetType().Name}");
                    doc.Editor.WriteMessage($"\n错误信息: {ex.Message}");
                    doc.Editor.WriteMessage("\n提示: 请联系技术支持或查看日志文件");
                }

                // 可选：显示用户友好的错误对话框
                try
                {
                    System.Windows.MessageBox.Show(
                        $"命令 '{commandName}' 执行失败:\n\n{ex.Message}\n\n请查看日志文件以获取详细信息。",
                        "标哥插件错误",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error
                    );
                }
                catch
                {
                    // 即使显示对话框失败也不要再次抛出异常
                }
            }
        }

        /// <summary>
        /// 安全执行同步命令
        /// </summary>
        public static void ExecuteSafely(Action action, string commandName)
        {
            try
            {
                action();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception acEx)
            {
                Log.Error(acEx, "AutoCAD命令执行失败: {CommandName}, ErrorStatus: {ErrorStatus}",
                    commandName, acEx.ErrorStatus);

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n[错误] {commandName} 执行失败: {acEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "命令执行时发生未处理的异常: {CommandName}", commandName);

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n[严重错误] {commandName} 执行失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 安全执行并返回结果的异步操作
        /// </summary>
        public static async Task<T?> ExecuteSafelyWithResult<T>(Func<Task<T>> action, string commandName, T? defaultValue = default)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "命令执行失败: {CommandName}", commandName);

                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n[错误] {commandName} 执行失败: {ex.Message}");
                }

                return defaultValue;
            }
        }
    }
}
