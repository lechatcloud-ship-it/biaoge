# 构建和打包指南

## 开发环境设置

### 1. 安装依赖

```bash
# 基础依赖
pip install -r requirements.txt

# 可选：安装PyQt-Fluent-Widgets获得更好的UI
pip install pyqt-fluent-widgets

# 可选：安装rtree获得更好的性能
pip install rtree

# 可选：psutil用于性能监控
pip install psutil
```

### 2. 配置API密钥

```bash
# 方式1：环境变量（推荐）
export DASHSCOPE_API_KEY="your_api_key_here"

# 方式2：在应用设置中配置
# 运行应用后，在"工具" -> "设置" -> "阿里云百炼"中输入
```

### 3. 运行开发版本

```bash
python main.py
```

## 打包发布

### 方式1：PyInstaller（推荐）

#### Windows

```bash
# 安装PyInstaller
pip install pyinstaller

# 打包为单个可执行文件
pyinstaller build.spec

# 输出在 dist/biaoge/ 目录
```

#### macOS

```bash
# 安装PyInstaller
pip install pyinstaller

# 打包为.app
pyinstaller build.spec

# 可选：创建DMG
hdiutil create -volname "表哥" -srcfolder dist/biaoge.app -ov -format UDZO dist/biaoge.dmg
```

#### Linux

```bash
# 安装PyInstaller
pip install pyinstaller

# 打包
pyinstaller build.spec

# 可选：创建AppImage
# 需要安装 appimagetool
```

### 方式2：setuptools

```bash
# 构建wheel包
python setup.py bdist_wheel

# 安装wheel
pip install dist/biaoge-1.0.0-py3-none-any.whl
```

## 性能测试

```bash
# 运行性能基准测试
python tests/performance_test.py
```

预期结果：
- ✅ 50K实体查询 < 10ms
- ✅ 内存占用 < 500MB
- ✅ 构件识别 < 100ms
- ✅ DWG导出 < 200ms

## 代码质量检查

```bash
# 语法检查
python -m py_compile src/**/*.py

# 类型检查（如果安装了mypy）
mypy src/

# 代码格式化（如果安装了black）
black src/
```

## 故障排除

### 问题1：PyQt6导入失败

```bash
# 解决方案：重新安装PyQt6
pip uninstall PyQt6 PyQt6-Qt6 PyQt6-sip
pip install PyQt6
```

### 问题2：ezdxf版本不兼容

```bash
# 解决方案：安装特定版本
pip install ezdxf==1.1.0
```

### 问题3：API密钥未生效

```bash
# 检查环境变量
echo $DASHSCOPE_API_KEY

# 或在Python中检查
python -c "import os; print(os.getenv('DASHSCOPE_API_KEY'))"
```

### 问题4：打包后无法运行

```bash
# 检查依赖是否完整
pyinstaller --onedir build.spec

# 查看详细日志
./dist/biaoge/biaoge --debug
```

## 发布清单

在发布前确保：

- [ ] 所有功能测试通过
- [ ] 性能基准测试通过
- [ ] README.md文档完整
- [ ] LICENSE文件正确
- [ ] 版本号已更新
- [ ] API密钥从代码中移除
- [ ] 日志级别设置为INFO
- [ ] 打包测试在目标平台通过
- [ ] 创建GitHub Release
- [ ] 更新CHANGELOG.md

## 持续集成 (可选)

创建 `.github/workflows/build.yml`:

```yaml
name: Build

on: [push, pull_request]

jobs:
  build:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
        python-version: ['3.8', '3.9', '3.10', '3.11']

    steps:
    - uses: actions/checkout@v2
    - name: Set up Python
      uses: actions/setup-python@v2
      with:
        python-version: ${{ matrix.python-version }}
    - name: Install dependencies
      run: |
        python -m pip install --upgrade pip
        pip install -r requirements.txt
        pip install pyinstaller
    - name: Build
      run: pyinstaller build.spec
    - name: Test
      run: python tests/performance_test.py
```

## 许可证

商业软件 - 版权所有 © 2025

详见 LICENSE 文件
