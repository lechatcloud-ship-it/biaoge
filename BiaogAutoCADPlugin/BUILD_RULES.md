# 构建规范 - 必须遵守

> **重要**：这是项目的标准构建流程，任何时候都必须严格遵守，不得自作主张创建其他构建方式。

## 📋 核心规则

### 规则1：所有构建产物必须输出到 `dist/` 目录

- ✅ **正确**：`dist/BiaogPlugin.bundle/`
- ✅ **正确**：`dist/安装程序.exe`
- ❌ **错误**：任何其他位置

### 规则2：必须使用标准构建脚本

- **插件Bundle打包**：必须使用 `build-bundle.bat`
- **安装程序构建**：必须使用 `build-installer.ps1`
- ❌ **禁止**：创建新的构建脚本或手动复制文件

### 规则3：构建顺序

```
1. 运行 build-bundle.bat   → 生成 dist/BiaogPlugin.bundle/
2. 运行 build-installer.ps1 → 生成 dist/安装程序.exe
```

## 🚀 标准构建流程

### 完整构建（从零开始）

```bash
# 步骤1：构建插件Bundle
cd BiaogAutoCADPlugin
.\build-bundle.bat

# 步骤2：构建安装程序
.\build-installer.ps1
```

**耗时**：约1-2分钟

### 仅更新安装程序（Bundle已存在）

```bash
cd BiaogAutoCADPlugin
.\build-installer.ps1
```

**耗时**：约30秒

## 📁 dist/ 目录结构（标准输出）

```
dist/
├── BiaogPlugin.bundle/          # 插件完整包（22MB）
│   ├── PackageContents.xml     # 版本和组件配置
│   ├── README.txt              # 用户安装说明
│   └── Contents/
│       ├── 2021/               # AutoCAD 2021-2024
│       │   └── BiaogPlugin.dll + 依赖
│       └── 2018/               # AutoCAD 2018-2020
│           └── BiaogPlugin.dll + 依赖
└── 安装程序.exe                 # 智能安装程序（72MB）
```

## ❌ 禁止的做法

### 1. 禁止手动复制文件

❌ 不要这样做：
```bash
# 错误示例
cp src/BiaogPlugin/bin/Release/* dist/BiaogPlugin.bundle/Contents/2021/
```

✅ 应该这样做：
```bash
.\build-bundle.bat
```

### 2. 禁止创建新的构建脚本

❌ 不要创建：
- `快速更新dist.bat`
- `快速更新dist.ps1`
- 任何其他自定义构建脚本

✅ 使用现有脚本：
- `build-bundle.bat`
- `build-installer.ps1`

### 3. 禁止输出到其他目录

❌ 不要输出到：
- 桌面
- 临时文件夹
- 任何非 `dist/` 的位置

## 🔧 build-bundle.bat 详细说明

### 功能
1. 清理旧的 `dist/BiaogPlugin.bundle/`
2. 创建Bundle目录结构
3. 运行 `dotnet clean`
4. 运行 `dotnet restore`
5. 运行 `dotnet build --configuration Release`
6. 复制编译产物到 `dist/BiaogPlugin.bundle/Contents/2021/`
7. 复制到 `dist/BiaogPlugin.bundle/Contents/2018/`（兼容）
8. 生成 `PackageContents.xml`（v1.0.4）
9. 生成 `README.txt`

### 运行方式

```bash
# 在项目根目录（BiaogAutoCADPlugin/）运行
.\build-bundle.bat
```

### 输出
- `dist/BiaogPlugin.bundle/` - 完整的可分发插件包

## 🔧 build-installer.ps1 详细说明

### 前置条件
- **必须先运行** `build-bundle.bat`
- `dist/BiaogPlugin.bundle/` 必须存在

### 功能
1. 检查 `dist/BiaogPlugin.bundle/` 是否存在
2. 清理 `Installer/bin` 和 `Installer/obj`
3. 运行 `dotnet publish` 生成单文件exe
4. 复制安装程序到 `dist/安装程序.exe`

### 运行方式

```bash
# 在项目根目录（BiaogAutoCADPlugin/）运行
.\build-installer.ps1
```

### 输出
- `dist/安装程序.exe` - 智能安装程序（72MB）

## 🐛 常见问题

### Q: 编译后发现DLL没有更新到dist？

A: 重新运行完整构建流程：
```bash
.\build-bundle.bat        # 这会重新编译并更新dist
.\build-installer.ps1    # 重新打包安装程序
```

### Q: 能否只更新DLL而不重新构建？

A: **不推荐**。应该重新运行 `build-bundle.bat`。

如果确实需要（仅调试时）：
```bash
dotnet build --configuration Release --no-restore
# 然后重新运行
.\build-bundle.bat
```

### Q: 构建失败怎么办？

A: 检查：
1. 是否在正确的目录（BiaogAutoCADPlugin/）
2. 是否有权限访问文件
3. 查看详细错误信息
4. 删除 `dist/BiaogPlugin.bundle/` 后重试

## 📝 版本号更新

每次发布新版本时，需要更新：

1. **build-bundle.bat** (3处)：
   - Line 66: `AppVersion="x.x.x"`
   - Line 70: `FriendlyVersion="x.x.x"`
   - Line 88: `Version="x.x.x"`
   - Line 101: `Version="x.x.x"`

2. **README生成** (build-bundle.bat内)：
   - 更新版本号和修复说明

## 🎯 分发流程

完成构建后：

```
dist/
├── BiaogPlugin.bundle/    ← 完整插件包
└── 安装程序.exe            ← 智能安装程序
```

**分发方式**：
1. 将整个 `dist/` 文件夹打包为ZIP
2. 或直接分发 `dist/` 文件夹
3. 用户运行 `安装程序.exe` 即可

## ✅ 检查清单

构建完成后，验证：

- [ ] `dist/BiaogPlugin.bundle/PackageContents.xml` 存在且版本正确
- [ ] `dist/BiaogPlugin.bundle/README.txt` 存在
- [ ] `dist/BiaogPlugin.bundle/Contents/2021/BiaogPlugin.dll` 存在
- [ ] `dist/BiaogPlugin.bundle/Contents/2018/BiaogPlugin.dll` 存在
- [ ] `dist/安装程序.exe` 存在（约72MB）
- [ ] DLL修改时间是最新的

---

**最后更新**：2025-01-14
**版本**：v1.0.4
