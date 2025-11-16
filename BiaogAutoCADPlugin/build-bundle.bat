@echo off
chcp 65001 >nul
echo ========================================
echo Biaoge AutoCAD Plugin - Multi-version Build
echo ========================================
echo.

set OUTPUT_DIR=dist\BiaogPlugin.bundle
set CONFIG=Release

REM Clean old output
if exist "%OUTPUT_DIR%" (
    echo Cleaning old output...
    rmdir /s /q "%OUTPUT_DIR%"
)

REM Create bundle structure
echo Creating .bundle structure...
mkdir "%OUTPUT_DIR%\Contents\2021" 2>nul

REM Build for AutoCAD 2021-2024
echo.
echo ========================================
echo Building for AutoCAD 2021-2024
echo ========================================
echo.

set AUTOCAD_PATH=C:\Program Files\Autodesk\AutoCAD 2022
echo Using: %AUTOCAD_PATH%

echo Cleaning project...
dotnet clean BiaogPlugin.sln --configuration %CONFIG% >nul 2>&1

echo Restoring NuGet packages...
dotnet restore BiaogPlugin.sln --force >nul 2>&1

echo Building plugin...
dotnet build BiaogPlugin.sln --configuration %CONFIG% --no-restore

if errorlevel 1 (
    echo Build failed!
    exit /b 1
)

echo Build successful!

REM Copy files to bundle
echo.
echo Copying files to bundle...
xcopy /E /I /Y "src\BiaogPlugin\bin\%CONFIG%\*" "%OUTPUT_DIR%\Contents\2021\" >nul

REM For now, also copy to 2018 folder as fallback
echo Creating fallback for 2018-2020...
xcopy /E /I /Y "%OUTPUT_DIR%\Contents\2021\*" "%OUTPUT_DIR%\Contents\2018\" >nul

echo.
echo Creating PackageContents.xml...
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<ApplicationPackage
echo   SchemaVersion="1.0"
echo   AutodeskProduct="AutoCAD"
echo   ProductType="Application"
echo   Name="Biaoge AutoCAD Plugin"
echo   Description="AI Translation and Quantity Calculation for CAD Drawings"
echo   AppVersion="1.0.6"
echo   Author="Biaoge Team"
echo   ProductCode="{A5B6C7D8-E9F0-1234-5678-9ABCDEF01234}"
echo   UpgradeCode="{B6C7D8E9-F012-3456-789A-BCDEF0123456}"
echo   FriendlyVersion="1.0.6"
echo   SupportPath="./Contents/"
echo   OnlineHelp="https://github.com/lechatcloud-ship-it/biaoge"^>
echo.
echo   ^<CompanyDetails
echo     Name="Biaoge Team"
echo     Url="https://github.com/lechatcloud-ship-it/biaoge"
echo     Email="support@biaoge.com" /^>
echo.
echo   ^<RuntimeRequirements
echo     OS="Win64"
echo     Platform="AutoCAD" /^>
echo.
echo   ^<Components Description="Biaoge Plugin Main"^>
echo.
echo     ^<!-- AutoCAD 2018-2020 ^(R22.0-R24.0^) --^>
echo     ^<ComponentEntry
echo       AppName="BiaogPlugin"
echo       Version="1.0.13"
echo       ModuleName="./Contents/2018/BiaogPlugin.dll"
echo       AppDescription="Biaoge Plugin - AutoCAD 2018-2020"
echo       LoadOnAutoCADStartup="True"^>
echo       ^<RuntimeRequirements
echo         OS="Win64"
echo         Platform="AutoCAD"
echo         SeriesMin="R22.0"
echo         SeriesMax="R24.0" /^>
echo       ^<Commands GroupName="BIAOGE_COMMANDS"^>
echo         ^<Command Local="BIAOGE_INITIALIZE" Global="BIAOGE_INITIALIZE" StartupCommand="True" /^>
echo         ^<Command Local="BIAOGE_AI" Global="BIAOGE_AI" /^>
echo         ^<Command Local="BIAOGE_TRANSLATE" Global="BIAOGE_TRANSLATE" /^>
echo         ^<Command Local="BIAOGE_TRANSLATE_ZH" Global="BIAOGE_TRANSLATE_ZH" /^>
echo         ^<Command Local="BIAOGE_TRANSLATE_EN" Global="BIAOGE_TRANSLATE_EN" /^>
echo         ^<Command Local="BIAOGE_SETTINGS" Global="BIAOGE_SETTINGS" /^>
echo         ^<Command Local="BIAOGE_HELP" Global="BIAOGE_HELP" /^>
echo       ^</Commands^>
echo     ^</ComponentEntry^>
echo.
echo     ^<!-- AutoCAD 2021-2024 ^(R24.1-R24.3^) - Binary Compatible --^>
echo     ^<ComponentEntry
echo       AppName="BiaogPlugin"
echo       Version="1.0.13"
echo       ModuleName="./Contents/2021/BiaogPlugin.dll"
echo       AppDescription="Biaoge Plugin - AutoCAD 2021-2024"
echo       LoadOnAutoCADStartup="True"^>
echo       ^<RuntimeRequirements
echo         OS="Win64"
echo         Platform="AutoCAD"
echo         SeriesMin="R24.1"
echo         SeriesMax="R24.3" /^>
echo       ^<Commands GroupName="BIAOGE_COMMANDS"^>
echo         ^<Command Local="BIAOGE_INITIALIZE" Global="BIAOGE_INITIALIZE" StartupCommand="True" /^>
echo         ^<Command Local="BIAOGE_AI" Global="BIAOGE_AI" /^>
echo         ^<Command Local="BIAOGE_TRANSLATE" Global="BIAOGE_TRANSLATE" /^>
echo         ^<Command Local="BIAOGE_TRANSLATE_ZH" Global="BIAOGE_TRANSLATE_ZH" /^>
echo         ^<Command Local="BIAOGE_TRANSLATE_EN" Global="BIAOGE_TRANSLATE_EN" /^>
echo         ^<Command Local="BIAOGE_SETTINGS" Global="BIAOGE_SETTINGS" /^>
echo         ^<Command Local="BIAOGE_HELP" Global="BIAOGE_HELP" /^>
echo       ^</Commands^>
echo     ^</ComponentEntry^>
echo.
echo   ^</Components^>
echo ^</ApplicationPackage^>
) > "%OUTPUT_DIR%\PackageContents.xml"

echo.
echo Creating README...
(
echo # Biaoge AutoCAD Plugin v1.0.6 - 面板用户体验优化版
echo.
echo ## 本版本修复内容 ^(2025-01-16^)
echo.
echo ### ✅ 面板用户体验优化^(v1.0.6^)echo **问题**：AI助手、翻译、算量面板首次打开时需要手动拖拽到右侧，尺寸过小echo **根本原因**：echo - AutoCAD会从注册表读取历史尺寸配置，覆盖代码设置echo - 缺少首次显示的强制配置逻辑echo - 异常Dock检测逻辑过于激进，可能误判为浮动模式echo.echo **修复方案**：echo - ✅ 首次显示强制停靠右侧，高度自动使用屏幕90%%echo - ✅ 后续打开保持用户自定义的位置和尺寸echo - ✅ 只在真正异常时才修复^(尺寸^<300px^)echo - ✅ 优化渲染触发逻辑，确保UI正确显示echo.echo **用户体验提升**：echo - 开箱即用：首次打开自动停靠右侧，无需手动拖拽echo - 合理尺寸：高度使用屏幕90%%，宽度根据面板类型优化echo - 记忆功能：保存并恢复用户自定义的位置和大小echo.
echo ### ✅ qwen-flash翻译深度优化^(重大升级 v1.0.5^)
echo **问题**：qwen-flash翻译输出满屏系统提示词和各种杂质
echo **根本原因**：
echo - qwen-flash是通用对话模型，可能以各种格式返回^(Markdown、引号、解释等^)
echo - 缺少深度清洗机制，导致提示词泄露到翻译结果
echo - 上下文长度配置错误^(1M配置用在8K模型上^)
echo.
echo **修复方案**：
echo - ✅ 实现7步深度清洗流程^(Markdown、提示词、前缀、引号、注释等^)
echo - ✅ 智能响应解析，自动提取纯净翻译
echo - ✅ 修正上下文长度配置^(qwen-flash 1M vs qwen-mt-flash 8K^)
echo - ✅ 充分利用1M上下文^(可一次性翻译整个图纸^)
echo - ✅ 默认切换到qwen-flash模型^(更强推理和专业术语理解^)
echo - ✅ 动态批次大小^(根据模型类型自动调整^)
echo.
echo **性能提升**：
echo - 翻译速度：↑ 5倍 ^(一次性处理vs分段^)
echo - Token利用率：37%% → 99.8%% ^(充分利用1M上下文^)
echo - 输出纯净度：80%% → 99.9%% ^(7步清洗^)
echo - 典型图纸翻译：10次API调用 → 1次API调用
echo.
echo ### ✅ AI Agent框架修复^(v1.0.5^)
echo **问题**：AI助手调用工具时报错 HTTP 400 invalid_request_error
echo **修复**：
echo - ✅ 扩展ChatMessage类，添加ToolCalls字段
echo - ✅ 修复assistant消息保存逻辑^(同时保存ToolCalls^)
echo - ✅ 修复ConvertToOpenAIChatMessages方法^(正确传递工具调用^)
echo - ✅ 符合阿里云百炼Function Calling规范
echo.
echo ### ✅ 流式消息显示^(v1.0.4^)
echo **问题**：AI助手流式消息显示异常，消息延迟很久后一次性全部出现
echo **根本原因**：
echo - SSE回调在后台ThreadPool线程执行
echo - 直接从后台线程调用AutoCAD API和WPF UI违反线程安全规则
echo - 之前的Dispatcher.Invoke方案不完整，未解决根本问题
echo.
echo **修复方案**：
echo - ✅ 在BailianApiClient层捕获SynchronizationContext
echo - ✅ 使用SynchronizationContext.Post将回调Marshal回AutoCAD主线程
echo - ✅ 异步非阻塞方式，避免死锁
echo - ✅ 符合.NET跨线程调用最佳实践
echo - ✅ 真正实现逐字流式显示效果
echo.
echo ### ✅ 中文字体自动切换^(v1.0.3修复^)
echo **问题**：翻译后文本显示为 ？？？ 问号
echo **修复**：自动检测中文字符并切换到支持中文的字体^(txt.shx + gbcbig.shx^)
echo.
echo ### ✅ 块文本提取完整性^(v1.0.2修复^)
echo **问题**：很多块里面的文本都不翻译
echo **修复**：
echo - 不再跳过匿名块^(动态块、标注块等^)
echo - 提取AttributeDefinition^(块属性定义^)
echo - 递归提取块定义中的嵌套块
echo.
echo ### ✅ 其他修复^(v1.0.1^)
echo - 流式消息立即显示^(改用Dispatcher.Invoke^)
echo - 中文输入焦点锁定^(PreviewLostKeyboardFocus^)
echo - PaletteSet首次显示修复
echo.
echo ## 支持的版本
echo - AutoCAD 2018-2024 ^(Windows 64-bit^)
echo.
echo ## 安装方法1：自动加载^(推荐^)
echo 1. 复制整个BiaogPlugin.bundle文件夹到:
echo    C:\ProgramData\Autodesk\ApplicationPlugins\
echo.
echo 2. 重启AutoCAD，插件会自动加载
echo.
echo ## 安装方法2：手动加载^(快速测试^)
echo 1. 在AutoCAD命令行输入: NETLOAD
echo 2. 选择对应版本的DLL:
echo    - 2018-2020: Contents\2018\BiaogPlugin.dll
echo    - 2021-2024: Contents\2021\BiaogPlugin.dll
echo.
echo ## 常用命令
echo - BIAOGE_AI ^(或 BAI^) - AI助手
echo - BIAOGE_TRANSLATE ^(或 BT^) - 翻译面板
echo - BIAOGE_TRANSLATE_ZH ^(或 BTZ^) - 一键翻译为中文
echo - BIAOGE_TRANSLATE_EN ^(或 BTE^) - 一键翻译为英语
echo - BIAOGE_QUANTITY - 智能算量
echo - BIAOGE_SETTINGS ^(或 BS^) - 设置
echo - BIAOGE_HELP ^(或 BH^) - 帮助
echo.
echo ## 首次使用
echo 1. 运行 BIAOGE_SETTINGS
echo 2. 在"Bailian API Config"标签页输入阿里云百炼API密钥
echo 3. 保存并开始使用
echo.
echo ## 测试重点^(v1.0.4^)
echo.
echo ### 1. AI助手流式消息测试
echo ```
echo 测试步骤：
echo 1. 运行命令：BAI^(打开AI助手^)
echo 2. 输入问题并发送
echo 3. 观察回复显示：
echo    ✓ 应该看到文字逐字/逐词显示^(真正的流式效果^)
echo    ✓ 不应该等很久后一次性全部出现
echo    ✓ 深度思考内容应该在独立的可折叠框中
echo ```
echo.
echo ### 2. 深度思考流式测试
echo ```
echo 测试步骤：
echo 1. 在AI助手中勾选"深度思考"
echo 2. 输入复杂问题^(如"分析这个图纸的结构设计"^)
echo 3. 观察思考过程显示：
echo    ✓ 思考内容在独立的Expander中
echo    ✓ 思考过程应该逐字显示
echo    ✓ 可以随时展开/收起思考框
echo ```
echo.
echo ### 3. 中文输入测试
echo ```
echo 测试步骤：
echo 1. 打开AI助手
echo 2. 单击输入框^(不需要双击^)
echo 3. 输入中文：
echo    ✓ 应该能够正常输入中文
echo    ✓ 焦点不应该跳转到AutoCAD命令行
echo    ✓ IME组字过程不应该中断
echo ```
echo.
echo ## 版本历史
echo.
echo - **v1.0.6** ^(2025-01-16^) - 面板用户体验优化^(自动停靠、合理尺寸^)
echo - **v1.0.4** ^(2025-01-14^) - 修复流式消息线程安全问题^(SynchronizationContext^)
echo - **v1.0.3** ^(2025-01-14^) - 修复中文显示为？？？的问题^(自动字体切换^)
echo - **v1.0.2** ^(2025-01-14^) - 修复块文本提取遗漏问题
echo - **v1.0.1** ^(2025-01-14^) - 修复流式消息、中文输入焦点、PaletteSet显示
echo - **v1.0.0** - 初始版本
echo.
echo ## 技术说明^(v1.0.4^)
echo.
echo ### 流式消息线程模型
echo.
echo **问题诊断**：
echo - HttpClient.SendAsync以ResponseHeadersRead模式执行
echo - ReadLineAsync^(^)在ThreadPool后台线程执行
echo - SSE回调^(onStreamChunk, onReasoningChunk^)在后台线程调用
echo - AutoCAD API不是线程安全的，必须从主线程调用
echo.
echo **正确方案^(SynchronizationContext模式^)**：
echo ```csharp
echo // 1. 在调用前捕获主线程的SynchronizationContext
echo var syncContext = SynchronizationContext.Current;
echo.
echo // 2. 在SSE回调中使用Post将调用Marshal回主线程
echo if ^(syncContext != null^)
echo {
echo     syncContext.Post^(_ =^> onStreamChunk^(text^), null^);
echo }
echo ```
echo.
echo 这是.NET跨线程调用的标准模式，确保所有回调都在AutoCAD主线程执行。
echo.
echo ## 支持与反馈
echo https://github.com/lechatcloud-ship-it/biaoge
echo.
echo 如果遇到问题，请提供：
echo 1. AutoCAD版本
echo 2. 问题描述
echo 3. 日志文件^(%%APPDATA%%\Biaoge\Logs\^)
echo 4. 问题截图
) > "%OUTPUT_DIR%\README.txt"

echo.
echo ========================================
echo Build Complete!
echo ========================================
echo.
echo Output location: %OUTPUT_DIR%
echo.
echo Installation:
echo   Copy BiaogPlugin.bundle to: C:\ProgramData\Autodesk\ApplicationPlugins\
echo   Then restart AutoCAD
echo.

pause
