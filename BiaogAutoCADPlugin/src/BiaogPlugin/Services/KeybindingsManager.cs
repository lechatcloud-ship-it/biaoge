using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Serilog;

namespace BiaogPlugin.Services
{
    /// <summary>
    /// 快捷键管理器 - 管理AutoCAD快捷键配置
    /// </summary>
    public class KeybindingsManager
    {
        /// <summary>
        /// 推荐的快捷键配置（AutoCAD acad.pgp格式）
        /// </summary>
        public static class RecommendedKeybindings
        {
            // 翻译功能
            public const string TranslateAll = "BT";              // BT = Biaoge Translate
            public const string TranslateSelected = "BTS";        // BTS = Biaoge Translate Selected
            public const string TranslateZH = "BTZ";              // BTZ = Biaoge Translate Chinese (推荐)
            public const string TranslateEN = "BTE";              // BTE = Biaoge Translate English

            // AI助手
            public const string AIAssistant = "BAI";              // BAI = Biaoge AI
            public const string AIVoice = "BAV";                  // BAV = Biaoge AI Voice

            // 算量功能
            public const string Calculate = "BC";                 // BC = Biaoge Calculate
            public const string QuickRecognize = "BQ";            // BQ = Biaoge Quick recognize
            public const string ExportExcel = "BE";               // BE = Biaoge Export

            // 面板Toggle（快捷键激活/隐藏面板）
            public const string ToggleTranslate = "BTT";          // BTT = Biaoge Toggle Translate
            public const string ToggleCalculate = "BCT";          // BCT = Biaoge toggle Calculate
            public const string ToggleAI = "BAT";                 // BAT = Biaoge toggle AI

            // 设置和工具
            public const string Settings = "BS";                  // BS = Biaoge Settings
            public const string Help = "BH";                      // BH = Biaoge Help
        }

        /// <summary>
        /// 生成acad.pgp格式的快捷键配置
        /// </summary>
        public static string GeneratePgpConfig()
        {
            var sb = new StringBuilder();

            sb.AppendLine("; ============================================");
            sb.AppendLine("; 标哥插件 (Biaoge Plugin) - 推荐快捷键配置");
            sb.AppendLine("; 生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("; ============================================");
            sb.AppendLine();
            sb.AppendLine("; 使用说明:");
            sb.AppendLine("; 1. 复制以下内容到您的 acad.pgp 文件中");
            sb.AppendLine("; 2. acad.pgp 位置: C:\\Users\\<用户名>\\AppData\\Roaming\\Autodesk\\AutoCAD <版本>\\<语言>\\Support\\acad.pgp");
            sb.AppendLine("; 3. 在AutoCAD中输入 REINIT 命令，选择 PGP file，点击确定重新加载");
            sb.AppendLine("; 4. 或者重启AutoCAD");
            sb.AppendLine();
            sb.AppendLine("; === 翻译功能 ===");
            sb.AppendLine($"{RecommendedKeybindings.TranslateAll},         *BIAOGE_TRANSLATE         ; 全图翻译");
            sb.AppendLine($"{RecommendedKeybindings.TranslateSelected},    *BIAOGE_TRANSLATE_SELECTED ; 框选翻译");
            sb.AppendLine($"{RecommendedKeybindings.TranslateZH},          *BIAOGE_TRANSLATE_ZH       ; 快速翻译为中文（推荐）");
            sb.AppendLine($"{RecommendedKeybindings.TranslateEN},          *BIAOGE_TRANSLATE_EN       ; 快速翻译为英语");
            sb.AppendLine();
            sb.AppendLine("; === AI助手 ===");
            sb.AppendLine($"{RecommendedKeybindings.AIAssistant},          *BIAOGE_AI                 ; 启动AI助手");
            sb.AppendLine();
            sb.AppendLine("; === 算量功能 ===");
            sb.AppendLine($"{RecommendedKeybindings.Calculate},            *BIAOGE_CALCULATE          ; 打开算量面板");
            sb.AppendLine($"{RecommendedKeybindings.QuickRecognize},       *BIAOGE_QUICKCOUNT         ; 快速统计构件");
            sb.AppendLine($"{RecommendedKeybindings.ExportExcel},          *BIAOGE_EXPORTEXCEL        ; 导出Excel清单");
            sb.AppendLine();
            sb.AppendLine("; === 面板快捷键（Toggle显示/隐藏）===");
            sb.AppendLine($"{RecommendedKeybindings.ToggleTranslate},      *BIAOGE_TOGGLE_TRANSLATE   ; 切换翻译面板");
            sb.AppendLine($"{RecommendedKeybindings.ToggleCalculate},      *BIAOGE_TOGGLE_CALCULATE   ; 切换算量面板");
            sb.AppendLine($"{RecommendedKeybindings.ToggleAI},             *BIAOGE_TOGGLE_AI          ; 切换AI助手面板");
            sb.AppendLine();
            sb.AppendLine("; === 设置和工具 ===");
            sb.AppendLine($"{RecommendedKeybindings.Settings},             *BIAOGE_SETTINGS           ; 打开设置");
            sb.AppendLine($"{RecommendedKeybindings.Help},                 *BIAOGE_HELP               ; 显示帮助");
            sb.AppendLine();
            sb.AppendLine("; ============================================");
            sb.AppendLine("; 温馨提示:");
            sb.AppendLine("; - 所有快捷键以 'B' (Biaoge) 开头，避免与AutoCAD标准快捷键冲突");
            sb.AppendLine("; - 您可以修改左侧的快捷键字母，但不要修改右侧的命令名称");
            sb.AppendLine("; - 修改后需要运行 REINIT 命令重新加载");
            sb.AppendLine("; ============================================");

            return sb.ToString();
        }

        /// <summary>
        /// 保存快捷键配置到文件
        /// </summary>
        public static string SavePgpConfigToDesktop()
        {
            try
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var fileName = $"biaoge_keybindings_{DateTime.Now:yyyyMMdd_HHmmss}.pgp";
                var filePath = Path.Combine(desktopPath, fileName);

                var content = GeneratePgpConfig();
                File.WriteAllText(filePath, content, Encoding.UTF8);

                Log.Information($"快捷键配置已保存到: {filePath}");
                return filePath;
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "保存快捷键配置失败");
                throw;
            }
        }

        /// <summary>
        /// 获取AutoCAD的Support文件夹路径
        /// </summary>
        public static string GetAutoCADSupportPath()
        {
            try
            {
                // 尝试从AutoCAD应用程序获取Support路径
                var supportPath = (string)Autodesk.AutoCAD.ApplicationServices.Application.GetSystemVariable("SUPPORTPATH");

                // Support路径是用分号分隔的多个路径，取第一个
                var paths = supportPath.Split(';');
                if (paths.Length > 0 && Directory.Exists(paths[0]))
                {
                    return paths[0];
                }

                // 如果失败，使用默认路径
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var defaultPath = Path.Combine(userProfile, "AppData", "Roaming", "Autodesk");

                return defaultPath;
            }
            catch (System.Exception ex)
            {
                Log.Error(ex, "获取AutoCAD Support路径失败");
                return string.Empty;
            }
        }

        /// <summary>
        /// 尝试自动安装快捷键到acad.pgp（带备份）
        /// </summary>
        public static bool TryInstallKeybindings(out string message)
        {
            try
            {
                // 查找acad.pgp文件
                var supportPath = GetAutoCADSupportPath();
                if (string.IsNullOrEmpty(supportPath))
                {
                    message = "无法找到AutoCAD Support文件夹";
                    return false;
                }

                // 搜索所有可能的acad.pgp位置
                var pgpPaths = Directory.GetFiles(supportPath, "acad.pgp", SearchOption.AllDirectories);
                if (pgpPaths.Length == 0)
                {
                    message = $"在 {supportPath} 中未找到 acad.pgp 文件";
                    return false;
                }

                var pgpPath = pgpPaths[0];

                // 备份原文件
                var backupPath = pgpPath + $".backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                File.Copy(pgpPath, backupPath, overwrite: false);

                // 读取原文件内容
                var originalContent = File.ReadAllText(pgpPath, Encoding.Default);

                // 检查是否已经安装
                if (originalContent.Contains("标哥插件") || originalContent.Contains("Biaoge Plugin"))
                {
                    message = "快捷键配置已存在，无需重复安装";
                    return true;
                }

                // 追加新配置
                var newConfig = "\n\n" + GeneratePgpConfig();
                File.AppendAllText(pgpPath, newConfig, Encoding.Default);

                message = $"快捷键配置已安装到: {pgpPath}\n备份文件: {backupPath}\n\n请在AutoCAD中输入 REINIT 命令重新加载PGP文件";
                Log.Information($"快捷键自动安装成功: {pgpPath}");
                return true;
            }
            catch (System.Exception ex)
            {
                message = $"自动安装失败: {ex.Message}\n建议手动复制配置文件";
                Log.Error(ex, "自动安装快捷键失败");
                return false;
            }
        }

        /// <summary>
        /// 获取快捷键使用指南
        /// </summary>
        public static string GetKeybindingsGuide()
        {
            var sb = new StringBuilder();

            sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
            sb.AppendLine("║  标哥插件 - 快捷键配置指南                              ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine("【推荐快捷键】（所有快捷键以 'B' 开头，避免冲突）");
            sb.AppendLine();
            sb.AppendLine("翻译功能:");
            sb.AppendLine($"  {RecommendedKeybindings.TranslateAll,-6} → 全图翻译             (BIAOGE_TRANSLATE)");
            sb.AppendLine($"  {RecommendedKeybindings.TranslateSelected,-6} → 框选翻译             (BIAOGE_TRANSLATE_SELECTED)");
            sb.AppendLine($"  {RecommendedKeybindings.TranslateZH,-6} → 快速翻译为中文（推荐）(BIAOGE_TRANSLATE_ZH)");
            sb.AppendLine($"  {RecommendedKeybindings.TranslateEN,-6} → 快速翻译为英语       (BIAOGE_TRANSLATE_EN)");
            sb.AppendLine();
            sb.AppendLine("AI助手:");
            sb.AppendLine($"  {RecommendedKeybindings.AIAssistant,-6} → 启动AI助手       (BIAOGE_AI)");
            sb.AppendLine();
            sb.AppendLine("算量功能:");
            sb.AppendLine($"  {RecommendedKeybindings.Calculate,-6} → 打开算量面板     (BIAOGE_CALCULATE)");
            sb.AppendLine($"  {RecommendedKeybindings.QuickRecognize,-6} → 快速统计构件     (BIAOGE_QUICKCOUNT)");
            sb.AppendLine($"  {RecommendedKeybindings.ExportExcel,-6} → 导出Excel清单    (BIAOGE_EXPORTEXCEL)");
            sb.AppendLine();
            sb.AppendLine("面板快捷键（Toggle显示/隐藏）:");
            sb.AppendLine($"  {RecommendedKeybindings.ToggleTranslate,-6} → 切换翻译面板     (BIAOGE_TOGGLE_TRANSLATE)");
            sb.AppendLine($"  {RecommendedKeybindings.ToggleCalculate,-6} → 切换算量面板     (BIAOGE_TOGGLE_CALCULATE)");
            sb.AppendLine($"  {RecommendedKeybindings.ToggleAI,-6} → 切换AI助手面板    (BIAOGE_TOGGLE_AI)");
            sb.AppendLine();
            sb.AppendLine("设置和工具:");
            sb.AppendLine($"  {RecommendedKeybindings.Settings,-6} → 打开设置         (BIAOGE_SETTINGS)");
            sb.AppendLine($"  {RecommendedKeybindings.Help,-6} → 显示帮助         (BIAOGE_HELP)");
            sb.AppendLine();
            sb.AppendLine("【安装方法】");
            sb.AppendLine();
            sb.AppendLine("方法1: 自动安装（推荐）");
            sb.AppendLine("  运行命令: BIAOGE_INSTALL_KEYS");
            sb.AppendLine("  自动备份并添加快捷键到 acad.pgp");
            sb.AppendLine();
            sb.AppendLine("方法2: 手动安装");
            sb.AppendLine("  1. 运行命令: BIAOGE_EXPORT_KEYS");
            sb.AppendLine("  2. 在桌面找到生成的 .pgp 文件");
            sb.AppendLine("  3. 打开您的 acad.pgp 文件");
            sb.AppendLine("  4. 复制 .pgp 文件内容到 acad.pgp 末尾");
            sb.AppendLine("  5. 保存文件");
            sb.AppendLine("  6. 在AutoCAD中输入 REINIT 命令重新加载");
            sb.AppendLine();
            sb.AppendLine("【acad.pgp 文件位置】");
            sb.AppendLine();
            sb.AppendLine("  %USERPROFILE%\\AppData\\Roaming\\Autodesk\\AutoCAD <版本>\\<语言>\\Support\\acad.pgp");
            sb.AppendLine();
            sb.AppendLine("【注意事项】");
            sb.AppendLine();
            sb.AppendLine("  ✓ 修改后需要运行 REINIT 命令或重启AutoCAD");
            sb.AppendLine("  ✓ 自动安装会创建备份文件（.backup_yyyyMMdd_HHmmss）");
            sb.AppendLine("  ✓ 可以自定义快捷键字母，但不要修改命令名称");
            sb.AppendLine("  ✓ 所有快捷键以 'B' 开头，不会与AutoCAD标准快捷键冲突");

            return sb.ToString();
        }

        /// <summary>
        /// 获取快捷键映射表（用于UI显示）
        /// </summary>
        public static Dictionary<string, (string shortcut, string description)> GetKeybindingsMap()
        {
            return new Dictionary<string, (string, string)>
            {
                // 翻译
                ["BIAOGE_TRANSLATE"] = (RecommendedKeybindings.TranslateAll, "全图翻译"),
                ["BIAOGE_TRANSLATE_SELECTED"] = (RecommendedKeybindings.TranslateSelected, "框选翻译"),
                ["BIAOGE_TRANSLATE_ZH"] = (RecommendedKeybindings.TranslateZH, "快速翻译为中文（推荐）"),
                ["BIAOGE_TRANSLATE_EN"] = (RecommendedKeybindings.TranslateEN, "快速翻译为英语"),

                // AI助手
                ["BIAOGE_AI"] = (RecommendedKeybindings.AIAssistant, "启动AI助手"),

                // 算量
                ["BIAOGE_CALCULATE"] = (RecommendedKeybindings.Calculate, "打开算量面板"),
                ["BIAOGE_QUICKCOUNT"] = (RecommendedKeybindings.QuickRecognize, "快速统计构件"),
                ["BIAOGE_EXPORTEXCEL"] = (RecommendedKeybindings.ExportExcel, "导出Excel清单"),

                // 面板Toggle
                ["BIAOGE_TOGGLE_TRANSLATE"] = (RecommendedKeybindings.ToggleTranslate, "切换翻译面板"),
                ["BIAOGE_TOGGLE_CALCULATE"] = (RecommendedKeybindings.ToggleCalculate, "切换算量面板"),
                ["BIAOGE_TOGGLE_AI"] = (RecommendedKeybindings.ToggleAI, "切换AI助手面板"),

                // 设置和工具
                ["BIAOGE_SETTINGS"] = (RecommendedKeybindings.Settings, "打开设置"),
                ["BIAOGE_HELP"] = (RecommendedKeybindings.Help, "显示帮助")
            };
        }
    }
}
