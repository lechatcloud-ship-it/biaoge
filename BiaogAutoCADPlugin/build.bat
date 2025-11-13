@echo off
REM ================================================================
REM 表哥 - AutoCAD插件自动化构建脚本
REM ================================================================

setlocal enabledelayedexpansion

echo.
echo ╔══════════════════════════════════════════════════════╗
echo ║                                                      ║
echo ║      表哥 - AutoCAD插件构建脚本                      ║
echo ║                                                      ║
echo ╚══════════════════════════════════════════════════════╝
echo.

REM 检查是否安装了.NET SDK
echo [1/6] 检查.NET SDK...
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 未找到.NET SDK，请先安装Visual Studio 2022或.NET SDK
    exit /b 1
)
echo [✓] .NET SDK已安装

REM 检查解决方案文件
echo.
echo [2/6] 检查项目文件...
if not exist "BiaogPlugin.sln" (
    echo [错误] 未找到BiaogPlugin.sln文件
    echo 请在项目根目录运行此脚本
    exit /b 1
)
echo [✓] 找到解决方案文件

REM 清理旧的构建输出
echo.
echo [3/6] 清理旧的构建输出...
dotnet clean BiaogPlugin.sln --configuration Release >nul 2>&1
if exist "src\BiaogPlugin\bin\Release" rmdir /s /q "src\BiaogPlugin\bin\Release"
if exist "src\BiaogPlugin\obj" rmdir /s /q "src\BiaogPlugin\obj"
echo [✓] 清理完成

REM 还原NuGet包
echo.
echo [4/6] 还原NuGet包...
dotnet restore BiaogPlugin.sln
if %errorlevel% neq 0 (
    echo [错误] NuGet包还原失败
    exit /b 1
)
echo [✓] NuGet包还原成功

REM 构建项目（Release模式）
echo.
echo [5/6] 构建项目 (Release配置)...
dotnet build BiaogPlugin.sln --configuration Release --no-restore
if %errorlevel% neq 0 (
    echo [错误] 构建失败
    exit /b 1
)
echo [✓] 构建成功

REM 创建发布包
echo.
echo [6/6] 创建发布包...
set OUTPUT_DIR=dist\BiaogPlugin
set RELEASE_DIR=src\BiaogPlugin\bin\Release\net48

if not exist "%RELEASE_DIR%" (
    echo [错误] 找不到构建输出目录: %RELEASE_DIR%
    exit /b 1
)

REM 创建输出目录
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
mkdir "%OUTPUT_DIR%"

REM 复制必要的文件
echo 复制插件文件...
copy "%RELEASE_DIR%\BiaogPlugin.dll" "%OUTPUT_DIR%\" >nul
copy "%RELEASE_DIR%\BiaogPlugin.pdb" "%OUTPUT_DIR%\" >nul 2>nul

REM 复制依赖的DLL（排除AutoCAD的DLL）
echo 复制依赖文件...
for %%F in (
    Serilog.dll
    Serilog.Sinks.File.dll
    Serilog.Sinks.Console.dll
    Microsoft.Data.Sqlite.dll
    SQLitePCLRaw.core.dll
    SQLitePCLRaw.provider.e_sqlite3.dll
    SQLitePCLRaw.batteries_v2.dll
    System.Text.Json.dll
    Newtonsoft.Json.dll
    EPPlus.dll
    Microsoft.Extensions.Http.dll
) do (
    if exist "%RELEASE_DIR%\%%F" (
        copy "%RELEASE_DIR%\%%F" "%OUTPUT_DIR%\" >nul
    )
)

REM 创建README文件
echo 创建安装说明...
(
echo 表哥 - AutoCAD翻译插件
echo ================================
echo.
echo 安装方法1：NETLOAD命令
echo 1. 打开AutoCAD
echo 2. 命令行输入: NETLOAD
echo 3. 选择: BiaogPlugin.dll
echo 4. 插件加载成功！
echo.
echo 安装方法2：自动加载
echo 1. 复制整个BiaogPlugin文件夹到:
echo    C:\ProgramData\Autodesk\ApplicationPlugins\
echo 2. 重启AutoCAD
echo.
echo 首次使用：
echo 1. 命令行输入: BIAOGE_SETTINGS
echo 2. 配置阿里云百炼API密钥
echo 3. 点击"测试连接"验证
echo 4. 保存设置
echo.
echo 主要命令：
echo - BIAOGE_TRANSLATE  翻译当前图纸
echo - BIAOGE_CALCULATE  构件识别算量
echo - BIAOGE_SETTINGS   打开设置
echo - BIAOGE_HELP       显示帮助
echo.
echo 版本: 1.0.0
echo 构建时间: %date% %time%
) > "%OUTPUT_DIR%\README.txt"

REM 输出构建信息
echo.
echo ╔══════════════════════════════════════════════════════╗
echo ║                构建完成！                            ║
echo ╚══════════════════════════════════════════════════════╝
echo.
echo 输出目录: %OUTPUT_DIR%
echo.
echo 文件清单:
dir /b "%OUTPUT_DIR%"
echo.
echo 下一步:
echo 1. 在AutoCAD中使用NETLOAD命令加载 BiaogPlugin.dll
echo 2. 或复制到 C:\ProgramData\Autodesk\ApplicationPlugins\
echo 3. 使用 BIAOGE_HELP 命令查看使用说明
echo.

REM 询问是否打开输出目录
set /p OPEN_DIR="是否打开输出目录? (Y/N): "
if /i "%OPEN_DIR%"=="Y" (
    explorer "%OUTPUT_DIR%"
)

echo.
echo 构建脚本执行完成！
pause
