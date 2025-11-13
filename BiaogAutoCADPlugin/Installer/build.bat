@echo off
chcp 65001 >nul
echo ============================================
echo 编译标哥AutoCAD插件安装程序
echo ============================================
echo.

echo 清理旧的编译输出...
if exist bin rd /s /q bin
if exist obj rd /s /q obj

echo.
echo 开始编译...
echo.

REM 编译单文件可执行程序
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishReadyToRun=true

if errorlevel 1 (
    echo.
    echo ============================================
    echo 编译失败！
    echo ============================================
    pause
    exit /b 1
)

echo.
echo ============================================
echo 编译成功！
echo ============================================
echo.
echo 输出位置：
echo   bin\Release\net8.0-windows\win-x64\publish\标哥AutoCAD插件安装程序.exe
echo.

REM 复制到 dist 目录
echo 复制安装程序到 dist 目录...
if not exist "..\dist" mkdir "..\dist"
copy /Y "bin\Release\net8.0-windows\win-x64\publish\标哥AutoCAD插件安装程序.exe" "..\dist\安装程序.exe"

if errorlevel 1 (
    echo 复制失败！
    pause
    exit /b 1
)

echo.
echo ============================================
echo 完成！安装程序已复制到 dist 目录
echo ============================================
echo.
echo 分发位置：..\dist\安装程序.exe
echo.
echo 文件大小：
for %%F in (..\dist\安装程序.exe) do echo   %%~zF 字节 (约 %%~zF/1024/1024 MB)
echo.

pause
