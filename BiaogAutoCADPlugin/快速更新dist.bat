@echo off
chcp 65001 >nul
echo ========================================
echo 快速更新 dist/BiaogPlugin.bundle
echo ========================================
echo.

set SRC=src\BiaogPlugin\bin\Release
set DEST=dist\BiaogPlugin.bundle\Contents

echo 正在复制最新编译文件...
echo.

echo [1/2] 复制到 2021 目录...
xcopy /E /I /Y "%SRC%\*" "%DEST%\2021\" >nul
if errorlevel 1 (
    echo ✗ 复制失败！
    pause
    exit /b 1
)
echo ✓ 完成

echo.
echo [2/2] 复制到 2018 目录...
xcopy /E /I /Y "%DEST%\2021\*" "%DEST%\2018\" >nul
if errorlevel 1 (
    echo ✗ 复制失败！
    pause
    exit /b 1
)
echo ✓ 完成

echo.
echo ========================================
echo ✓ 更新完成！
echo ========================================
echo.

echo 最新DLL已复制到：
echo   - dist\BiaogPlugin.bundle\Contents\2021\
echo   - dist\BiaogPlugin.bundle\Contents\2018\
echo.

pause
