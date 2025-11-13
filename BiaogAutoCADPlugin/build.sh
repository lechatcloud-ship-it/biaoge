#!/bin/bash

# ================================================================
# 表哥 - AutoCAD插件自动化构建脚本 (Linux/Mac)
# ================================================================

set -e

echo ""
echo "╔══════════════════════════════════════════════════════╗"
echo "║                                                      ║"
echo "║      表哥 - AutoCAD插件构建脚本                      ║"
echo "║                                                      ║"
echo "╚══════════════════════════════════════════════════════╝"
echo ""

# 检查是否安装了.NET SDK
echo "[1/6] 检查.NET SDK..."
if ! command -v dotnet &> /dev/null; then
    echo "[错误] 未找到.NET SDK，请先安装.NET SDK"
    exit 1
fi
echo "[✓] .NET SDK已安装: $(dotnet --version)"

# 检查解决方案文件
echo ""
echo "[2/6] 检查项目文件..."
if [ ! -f "BiaogPlugin.sln" ]; then
    echo "[错误] 未找到BiaogPlugin.sln文件"
    echo "请在项目根目录运行此脚本"
    exit 1
fi
echo "[✓] 找到解决方案文件"

# 清理旧的构建输出
echo ""
echo "[3/6] 清理旧的构建输出..."
dotnet clean BiaogPlugin.sln --configuration Release > /dev/null 2>&1 || true
rm -rf src/BiaogPlugin/bin/Release
rm -rf src/BiaogPlugin/obj
echo "[✓] 清理完成"

# 还原NuGet包
echo ""
echo "[4/6] 还原NuGet包..."
dotnet restore BiaogPlugin.sln
if [ $? -ne 0 ]; then
    echo "[错误] NuGet包还原失败"
    exit 1
fi
echo "[✓] NuGet包还原成功"

# 构建项目（Release模式）
echo ""
echo "[5/6] 构建项目 (Release配置)..."
dotnet build BiaogPlugin.sln --configuration Release --no-restore
if [ $? -ne 0 ]; then
    echo "[错误] 构建失败"
    exit 1
fi
echo "[✓] 构建成功"

# 创建发布包
echo ""
echo "[6/6] 创建发布包..."
OUTPUT_DIR="dist/BiaogPlugin"
RELEASE_DIR="src/BiaogPlugin/bin/Release/net48"

if [ ! -d "$RELEASE_DIR" ]; then
    echo "[错误] 找不到构建输出目录: $RELEASE_DIR"
    exit 1
fi

# 创建输出目录
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# 复制必要的文件
echo "复制插件文件..."
cp "$RELEASE_DIR/BiaogPlugin.dll" "$OUTPUT_DIR/"
[ -f "$RELEASE_DIR/BiaogPlugin.pdb" ] && cp "$RELEASE_DIR/BiaogPlugin.pdb" "$OUTPUT_DIR/" || true

# 复制依赖的DLL（排除AutoCAD的DLL）
echo "复制依赖文件..."
DEPENDENCIES=(
    "Serilog.dll"
    "Serilog.Sinks.File.dll"
    "Serilog.Sinks.Console.dll"
    "Microsoft.Data.Sqlite.dll"
    "SQLitePCLRaw.core.dll"
    "SQLitePCLRaw.provider.e_sqlite3.dll"
    "SQLitePCLRaw.batteries_v2.dll"
    "System.Text.Json.dll"
    "Newtonsoft.Json.dll"
    "EPPlus.dll"
    "Microsoft.Extensions.Http.dll"
)

for dep in "${DEPENDENCIES[@]}"; do
    if [ -f "$RELEASE_DIR/$dep" ]; then
        cp "$RELEASE_DIR/$dep" "$OUTPUT_DIR/"
    fi
done

# 创建README文件
echo "创建安装说明..."
cat > "$OUTPUT_DIR/README.txt" <<EOF
表哥 - AutoCAD翻译插件
================================

安装方法1：NETLOAD命令
1. 打开AutoCAD
2. 命令行输入: NETLOAD
3. 选择: BiaogPlugin.dll
4. 插件加载成功！

安装方法2：自动加载
1. 复制整个BiaogPlugin文件夹到:
   C:\\ProgramData\\Autodesk\\ApplicationPlugins\\
2. 重启AutoCAD

首次使用：
1. 命令行输入: BIAOGE_SETTINGS
2. 配置阿里云百炼API密钥
3. 点击"测试连接"验证
4. 保存设置

主要命令：
- BIAOGE_TRANSLATE  翻译当前图纸
- BIAOGE_CALCULATE  构件识别算量
- BIAOGE_SETTINGS   打开设置
- BIAOGE_HELP       显示帮助

版本: 1.0.0
构建时间: $(date)
EOF

# 输出构建信息
echo ""
echo "╔══════════════════════════════════════════════════════╗"
echo "║                构建完成！                            ║"
echo "╚══════════════════════════════════════════════════════╝"
echo ""
echo "输出目录: $OUTPUT_DIR"
echo ""
echo "文件清单:"
ls -lh "$OUTPUT_DIR"
echo ""
echo "下一步:"
echo "1. 将文件复制到Windows机器"
echo "2. 在AutoCAD中使用NETLOAD命令加载 BiaogPlugin.dll"
echo "3. 或复制到 C:\\ProgramData\\Autodesk\\ApplicationPlugins\\"
echo "4. 使用 BIAOGE_HELP 命令查看使用说明"
echo ""
echo "构建脚本执行完成！"
