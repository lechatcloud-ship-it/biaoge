using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BiaogInstaller
{
    public partial class InstallerForm : Form
    {
        private Label titleLabel;
        private Label subtitleLabel;
        private GroupBox versionGroupBox;
        private TextBox versionTextBox;
        private GroupBox infoGroupBox;
        private RichTextBox infoTextBox;
        private Button installButton;
        private Button uninstallButton;
        private ProgressBar progressBar;
        private Label statusLabel;
        private Label installStatusLabel;

        private List<AutoCADVersion> detectedVersions;
        private const string TargetInstallDir = @"C:\ProgramData\Autodesk\ApplicationPlugins\BiaogPlugin.bundle";

        public InstallerForm()
        {
            InitializeComponent();
            CheckAdminPrivileges();
            DetectAutoCADVersions();
            CheckInstallationStatus();
        }

        private void InitializeComponent()
        {
            // 窗体设置 - 现代简洁设计（参考VSCode/Notion风格）
            this.Text = "标哥AutoCAD插件 - 安装程序";
            this.Size = new Size(700, 850);  // 调整为适中尺寸
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(248, 249, 250);  // 更柔和的灰色背景

            // DpiUnaware模式：让Windows自动放大窗口
            this.AutoScaleMode = AutoScaleMode.Font;

            // 标题区域 - 渐变背景
            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(700, 110),
                BackColor = Color.FromArgb(99, 102, 241)  // Indigo色
            };
            this.Controls.Add(headerPanel);

            // 标题
            titleLabel = new Label
            {
                Text = "标哥AutoCAD插件",
                Font = new Font("微软雅黑", 22, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(45, 28)
            };
            headerPanel.Controls.Add(titleLabel);

            // 副标题
            subtitleLabel = new Label
            {
                Text = "建筑工程CAD AI智能翻译与算量工具",
                Font = new Font("微软雅黑", 11),
                ForeColor = Color.FromArgb(224, 231, 255),
                AutoSize = true,
                Location = new Point(45, 68)
            };
            headerPanel.Controls.Add(subtitleLabel);

            // 检测到的版本 - 简洁卡片（增加高度支持多版本）
            versionGroupBox = new GroupBox
            {
                Text = "  检测到的 AutoCAD 版本",
                Location = new Point(35, 135),
                Size = new Size(630, 170),
                Font = new Font("微软雅黑", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                FlatStyle = FlatStyle.Flat
            };
            this.Controls.Add(versionGroupBox);

            versionTextBox = new TextBox
            {
                Location = new Point(18, 32),
                Size = new Size(594, 125),
                Multiline = true,
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10),
                BackColor = Color.FromArgb(250, 251, 252),
                BorderStyle = BorderStyle.None,
                ForeColor = Color.FromArgb(51, 65, 85)
            };
            versionGroupBox.Controls.Add(versionTextBox);

            // 安装信息 - 优化卡片
            infoGroupBox = new GroupBox
            {
                Text = "  安装信息",
                Location = new Point(35, 320),
                Size = new Size(630, 320),
                Font = new Font("微软雅黑", 10, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                FlatStyle = FlatStyle.Flat
            };
            this.Controls.Add(infoGroupBox);

            // 使用RichTextBox以获得更好的多行文本控制
            infoTextBox = new RichTextBox
            {
                Location = new Point(18, 32),
                Size = new Size(594, 273),
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Font = new Font("微软雅黑", 10),
                BackColor = Color.FromArgb(250, 251, 252),
                BorderStyle = BorderStyle.None,
                ForeColor = Color.FromArgb(51, 65, 85),
                DetectUrls = false  // 禁用URL检测避免格式问题
            };

            // 使用明确的文本设置，确保正确换行
            infoTextBox.Text =
                "系统要求\r\n" +
                "• AutoCAD 2018-2024 (Windows 64位)\r\n" +
                "• 安装位置（所有版本统一）\r\n" +
                "  C:\\ProgramData\\Autodesk\\ApplicationPlugins\\\r\n" +
                "• 所有版本自动加载，无需重复安装\r\n" +
                "\r\n" +
                "核心功能\r\n" +
                "• AI智能翻译 - 支持中英日韩等8种语言\r\n" +
                "• 构件识别算量 - 自动识别并计算工程量\r\n" +
                "• Excel导出 - 生成专业工程量清单\r\n" +
                "• 图层翻译 - 批量翻译图层名称\r\n" +
                "• AI助手 - 智能对话和图纸分析\r\n" +
                "\r\n" +
                "安装后操作\r\n" +
                "1. 重启 AutoCAD (任意2018-2024版本)\r\n" +
                "2. 插件会自动加载到顶部工具栏\r\n" +
                "3. 查看【标哥工具】选项卡\r\n" +
                "4. 运行 BIAOGE_SETTINGS 配置API密钥\r\n" +
                "\r\n" +
                "技术支持\r\n" +
                "GitHub: github.com/lechatcloud-ship-it/biaoge\r\n" +
                "文档: 查看安装目录下的使用说明";

            infoGroupBox.Controls.Add(infoTextBox);

            // 安装状态标签
            installStatusLabel = new Label
            {
                Location = new Point(35, 655),
                Size = new Size(630, 28),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("微软雅黑", 10),
                ForeColor = Color.FromArgb(100, 116, 139)
            };
            this.Controls.Add(installStatusLabel);

            // 进度条
            progressBar = new ProgressBar
            {
                Location = new Point(35, 690),
                Size = new Size(630, 7),
                Visible = false,
                Style = ProgressBarStyle.Continuous
            };
            this.Controls.Add(progressBar);

            // 状态标签
            statusLabel = new Label
            {
                Location = new Point(35, 702),
                Size = new Size(630, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("微软雅黑", 9),
                ForeColor = Color.FromArgb(148, 163, 184),
                Visible = false
            };
            this.Controls.Add(statusLabel);

            // 按钮设置 - 给底部留足够空间
            var buttonY = 740;
            var buttonSpacing = 22;
            var buttonWidth = 250;
            var buttonHeight = 48;

            // 计算居中位置
            var totalWidth = buttonWidth * 2 + buttonSpacing;
            var startX = (this.ClientSize.Width - totalWidth) / 2;

            // 安装按钮 - 现代设计
            installButton = new Button
            {
                Text = "开始安装",
                Location = new Point(startX, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                Font = new Font("微软雅黑", 12, FontStyle.Bold),
                BackColor = Color.FromArgb(99, 102, 241),  // Indigo
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabIndex = 0
            };
            installButton.FlatAppearance.BorderSize = 0;
            installButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(79, 70, 229);
            installButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(67, 56, 202);
            // 禁用状态样式
            installButton.FlatAppearance.CheckedBackColor = Color.FromArgb(203, 213, 225);
            installButton.EnabledChanged += (s, e) =>
            {
                installButton.BackColor = installButton.Enabled
                    ? Color.FromArgb(99, 102, 241)
                    : Color.FromArgb(203, 213, 225);
                installButton.ForeColor = installButton.Enabled
                    ? Color.White
                    : Color.FromArgb(148, 163, 184);
                installButton.Cursor = installButton.Enabled ? Cursors.Hand : Cursors.Default;
            };
            installButton.Click += InstallButton_Click;
            this.Controls.Add(installButton);

            // 卸载按钮 - 次要按钮样式
            uninstallButton = new Button
            {
                Text = "卸载插件",
                Location = new Point(startX + buttonWidth + buttonSpacing, buttonY),
                Size = new Size(buttonWidth, buttonHeight),
                Font = new Font("微软雅黑", 12),  // 与安装按钮字体一致
                BackColor = Color.White,
                ForeColor = Color.FromArgb(71, 85, 105),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                TabIndex = 1
            };
            uninstallButton.FlatAppearance.BorderSize = 1;
            uninstallButton.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
            uninstallButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(248, 250, 252);
            // 禁用状态样式
            uninstallButton.EnabledChanged += (s, e) =>
            {
                uninstallButton.BackColor = uninstallButton.Enabled
                    ? Color.White
                    : Color.FromArgb(248, 250, 252);
                uninstallButton.ForeColor = uninstallButton.Enabled
                    ? Color.FromArgb(71, 85, 105)
                    : Color.FromArgb(203, 213, 225);
                uninstallButton.FlatAppearance.BorderColor = uninstallButton.Enabled
                    ? Color.FromArgb(226, 232, 240)
                    : Color.FromArgb(241, 245, 249);
                uninstallButton.Cursor = uninstallButton.Enabled ? Cursors.Hand : Cursors.Default;
            };
            uninstallButton.Click += UninstallButton_Click;
            this.Controls.Add(uninstallButton);
        }

        private void CheckAdminPrivileges()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

            if (!isAdmin)
            {
                MessageBox.Show(
                    "本程序需要管理员权限才能安装插件。\n\n请右键选择\"以管理员身份运行\"。",
                    "需要管理员权限",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning
                );
                Application.Exit();
            }
        }

        private void DetectAutoCADVersions()
        {
            detectedVersions = new List<AutoCADVersion>();

            // 版本映射表 (R版本号 -> 年份)
            var versionMap = new Dictionary<string, int>
            {
                { "R22", 2018 },
                { "R23", 2019 },
                { "R24", 2020 },
                { "R25", 2021 },
                { "R26", 2022 },
                { "R27", 2023 },
                { "R28", 2024 }
            };

            try
            {
                // 方法1: 通过注册表检测（最佳实践）
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var autocadKey = baseKey.OpenSubKey(@"SOFTWARE\Autodesk\AutoCAD"))
                {
                    if (autocadKey != null)
                    {
                        // 枚举所有版本子键 (例如 "R23.1", "R24.0" 等)
                        foreach (var versionKey in autocadKey.GetSubKeyNames())
                        {
                            // 提取主版本号 "R23" 从 "R23.1"
                            string majorVersion = versionKey.Split('.')[0];

                            if (versionMap.TryGetValue(majorVersion, out int year))
                            {
                                using (var versionSubKey = autocadKey.OpenSubKey(versionKey))
                                {
                                    if (versionSubKey != null)
                                    {
                                        // 读取 CurVer 获取当前产品ID (例如 "ACAD-D001:409")
                                        string curVer = versionSubKey.GetValue("CurVer") as string;
                                        if (!string.IsNullOrEmpty(curVer))
                                        {
                                            using (var productKey = versionSubKey.OpenSubKey(curVer))
                                            {
                                                if (productKey != null)
                                                {
                                                    // 读取 AcadLocation 获取 acad.exe 路径
                                                    string acadLocation = productKey.GetValue("AcadLocation") as string;
                                                    if (!string.IsNullOrEmpty(acadLocation) && File.Exists(acadLocation))
                                                    {
                                                        string installPath = Path.GetDirectoryName(acadLocation);

                                                        // 避免重复添加同一年份的版本
                                                        if (!detectedVersions.Any(v => v.Year == year && v.Path == installPath))
                                                        {
                                                            detectedVersions.Add(new AutoCADVersion
                                                            {
                                                                Year = year,
                                                                Path = installPath,
                                                                Version = versionKey
                                                            });
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // 注册表访问失败时，使用降级方案
            }

            // 方法2: 降级方案 - 扫描常见盘符（如果注册表未找到）
            if (!detectedVersions.Any())
            {
                FallbackDetection();
            }

            // 更新显示
            if (detectedVersions.Any())
            {
                versionTextBox.Text = $"✓ 检测到 {detectedVersions.Count} 个 AutoCAD 版本\r\n\r\n";
                foreach (var v in detectedVersions.OrderBy(x => x.Year))
                {
                    versionTextBox.Text += $"  • AutoCAD {v.Year} ({v.Version})\r\n";
                    versionTextBox.Text += $"    {v.Path}\r\n\r\n";
                }
                versionTextBox.Text += "═══════════════════════════════\r\n";
                versionTextBox.Text += "插件将安装到统一位置：\r\n";
                versionTextBox.Text += "C:\\ProgramData\\Autodesk\\ApplicationPlugins\\\r\n\r\n";
                versionTextBox.Text += "所有检测到的版本都会自动加载插件！\r\n";
                versionTextBox.Text += "无需为每个版本单独安装。";
                versionTextBox.ForeColor = Color.Green;
            }
            else
            {
                versionTextBox.Text = "⚠ 未检测到 AutoCAD 安装\r\n\r\n" +
                    "您可以继续安装插件，但需要先安装 AutoCAD 2018-2024 才能使用。\r\n\r\n" +
                    "插件会安装到：\r\n" +
                    "C:\\ProgramData\\Autodesk\\ApplicationPlugins\\\r\n\r\n" +
                    "安装 AutoCAD 后，插件会在启动时自动加载。";
                versionTextBox.ForeColor = Color.Orange;
            }
        }

        private void FallbackDetection()
        {
            // 降级方案：扫描常见盘符的默认安装路径
            var commonDrives = new[] { "C", "D", "E", "F" };
            var commonPaths = new[]
            {
                @"Program Files\Autodesk\AutoCAD {0}",
                @"Program Files (x86)\Autodesk\AutoCAD {0}",
                @"Autodesk\AutoCAD {0}"
            };

            for (int year = 2018; year <= 2024; year++)
            {
                foreach (var drive in commonDrives)
                {
                    foreach (var pathTemplate in commonPaths)
                    {
                        string path = $@"{drive}:\{string.Format(pathTemplate, year)}";
                        if (Directory.Exists(path))
                        {
                            string dllPath = Path.Combine(path, "acdbmgd.dll");
                            if (File.Exists(dllPath))
                            {
                                // 避免重复
                                if (!detectedVersions.Any(v => v.Year == year && v.Path == path))
                                {
                                    detectedVersions.Add(new AutoCADVersion
                                    {
                                        Year = year,
                                        Path = path,
                                        Version = $"R{year - 1996}.0"
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        private void CheckInstallationStatus()
        {
            bool isInstalled = Directory.Exists(TargetInstallDir);

            if (isInstalled)
            {
                installStatusLabel.Text = "✓ 插件已安装";
                installStatusLabel.ForeColor = Color.Green;
                installButton.Enabled = false;
                uninstallButton.Enabled = true;
            }
            else
            {
                installStatusLabel.Text = "插件未安装";
                installStatusLabel.ForeColor = Color.Gray;
                installButton.Enabled = true;
                uninstallButton.Enabled = false;
            }

            // 手动更新按钮样式
            UpdateButtonStyles();
        }

        private void UpdateButtonStyles()
        {
            // 更新安装按钮样式
            if (installButton.Enabled)
            {
                installButton.BackColor = Color.FromArgb(99, 102, 241);
                installButton.ForeColor = Color.White;
                installButton.Cursor = Cursors.Hand;
            }
            else
            {
                installButton.BackColor = Color.FromArgb(203, 213, 225);  // 灰色
                installButton.ForeColor = Color.FromArgb(148, 163, 184);  // 浅灰文字
                installButton.Cursor = Cursors.Default;
            }

            // 更新卸载按钮样式
            if (uninstallButton.Enabled)
            {
                uninstallButton.BackColor = Color.White;
                uninstallButton.ForeColor = Color.FromArgb(71, 85, 105);
                uninstallButton.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
                uninstallButton.Cursor = Cursors.Hand;
            }
            else
            {
                uninstallButton.BackColor = Color.FromArgb(248, 250, 252);  // 浅灰背景
                uninstallButton.ForeColor = Color.FromArgb(203, 213, 225);  // 浅灰文字
                uninstallButton.FlatAppearance.BorderColor = Color.FromArgb(241, 245, 249);
                uninstallButton.Cursor = Cursors.Default;
            }
        }

        private async void InstallButton_Click(object sender, EventArgs e)
        {
            installButton.Enabled = false;
            uninstallButton.Enabled = false;
            UpdateButtonStyles();  // 立即更新按钮样式为灰色
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            statusLabel.Visible = true;

            try
            {
                // 确定源目录和目标目录
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                string bundleSource = Path.Combine(currentDir, "BiaogPlugin.bundle");

                // 检查源文件
                statusLabel.Text = "正在检查插件文件...";
                await System.Threading.Tasks.Task.Delay(500);

                if (!Directory.Exists(bundleSource))
                {
                    throw new Exception($"找不到插件文件！\n\n请确保 BiaogPlugin.bundle 文件夹在同一目录下。\n\n当前目录：{currentDir}\n期望位置：{bundleSource}");
                }

                // 删除旧版本
                statusLabel.Text = "正在删除旧版本...";
                await System.Threading.Tasks.Task.Delay(500);

                if (Directory.Exists(TargetInstallDir))
                {
                    Directory.Delete(TargetInstallDir, true);
                }

                // 复制文件
                statusLabel.Text = "正在复制插件文件...";
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 0;

                CopyDirectory(bundleSource, TargetInstallDir, (progress) =>
                {
                    progressBar.Value = Math.Min(progress, 100);
                });

                // 验证安装
                statusLabel.Text = "正在验证安装...";
                await System.Threading.Tasks.Task.Delay(500);

                string dllPath = Path.Combine(TargetInstallDir, @"Contents\2021\BiaogPlugin.dll");
                if (!File.Exists(dllPath))
                {
                    throw new Exception("安装验证失败：找不到主 DLL 文件。");
                }

                // 完成
                progressBar.Value = 100;
                statusLabel.Text = "安装成功！";
                statusLabel.ForeColor = Color.Green;

                await System.Threading.Tasks.Task.Delay(500);

                MessageBox.Show(
                    $"标哥AutoCAD插件 安装成功！\n\n" +
                    $"安装位置：{TargetInstallDir}\n\n" +
                    "下一步操作：\n" +
                    "1. 重启 AutoCAD\n" +
                    "2. 插件会自动加载\n" +
                    "3. 运行 BIAOGE_HELP 查看所有命令\n" +
                    "4. 运行 BIAOGE_SETTINGS 配置 API 密钥\n\n" +
                    "常用命令：\n" +
                    "  BIAOGE_TRANSLATE_ZH - 翻译为中文\n" +
                    "  BIAOGE_AI - AI助手\n" +
                    "  BIAOGE_CALCULATE - 算量",
                    "安装成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                CheckInstallationStatus();
                progressBar.Visible = false;
                statusLabel.Visible = false;
            }
            catch (Exception ex)
            {
                progressBar.Visible = false;
                statusLabel.Visible = false;
                CheckInstallationStatus();

                MessageBox.Show(
                    $"安装失败：\n\n{ex.Message}\n\n请确保：\n" +
                    "1. 以管理员身份运行此程序\n" +
                    "2. BiaogPlugin.bundle 文件夹在同一目录\n" +
                    "3. 没有其他程序占用文件",
                    "安装失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private async void UninstallButton_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "确定要卸载标哥AutoCAD插件吗？\n\n" +
                "卸载后需要重启 AutoCAD 才能完全生效。",
                "确认卸载",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result != DialogResult.Yes)
                return;

            installButton.Enabled = false;
            uninstallButton.Enabled = false;
            UpdateButtonStyles();  // 立即更新按钮样式为灰色
            progressBar.Visible = true;
            progressBar.Style = ProgressBarStyle.Marquee;
            statusLabel.Visible = true;

            try
            {
                statusLabel.Text = "正在卸载插件...";
                await System.Threading.Tasks.Task.Delay(500);

                if (!Directory.Exists(TargetInstallDir))
                {
                    throw new Exception("插件未安装，无需卸载。");
                }

                // 删除插件目录
                Directory.Delete(TargetInstallDir, true);

                // 验证卸载
                statusLabel.Text = "正在验证卸载...";
                await System.Threading.Tasks.Task.Delay(500);

                if (Directory.Exists(TargetInstallDir))
                {
                    throw new Exception("卸载验证失败：插件目录仍然存在。");
                }

                // 完成
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 100;
                statusLabel.Text = "卸载成功！";
                statusLabel.ForeColor = Color.Green;

                await System.Threading.Tasks.Task.Delay(500);

                MessageBox.Show(
                    "标哥AutoCAD插件 卸载成功！\n\n" +
                    "请重启 AutoCAD 以完全生效。",
                    "卸载成功",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                CheckInstallationStatus();
                progressBar.Visible = false;
                statusLabel.Visible = false;
            }
            catch (Exception ex)
            {
                progressBar.Visible = false;
                statusLabel.Visible = false;
                CheckInstallationStatus();

                MessageBox.Show(
                    $"卸载失败：\n\n{ex.Message}\n\n请确保：\n" +
                    "1. 以管理员身份运行此程序\n" +
                    "2. 已关闭所有 AutoCAD 窗口\n" +
                    "3. 没有其他程序占用文件",
                    "卸载失败",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir, Action<int> progressCallback)
        {
            Directory.CreateDirectory(targetDir);

            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            int totalFiles = files.Length;
            int copiedFiles = 0;

            foreach (string file in files)
            {
                string relativePath = file.Substring(sourceDir.Length + 1);
                string targetFile = Path.Combine(targetDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
                File.Copy(file, targetFile, true);

                copiedFiles++;
                int progress = (int)((double)copiedFiles / totalFiles * 100);
                progressCallback?.Invoke(progress);
            }
        }
    }

    public class AutoCADVersion
    {
        public int Year { get; set; }
        public string Path { get; set; } = "";
        public string Version { get; set; } = "";
    }
}
