# Services 复用说明

以下服务需要从 `BiaogeCSharp/src/BiaogeCSharp/Services/` 复制：

## 需要复用的文件列表

1. **TranslationEngine.cs** - 翻译引擎核心
   - 复制自: `BiaogeCSharp/src/BiaogeCSharp/Services/TranslationEngine.cs`
   - 修改命名空间: `BiaogeCSharp.Services` → `BiaogPlugin.Services`
   - 复用率: 100%

2. **BailianApiClient.cs** - 阿里云百炼API客户端
   - 复制自: `BiaogeCSharp/src/BiaogeCSharp/Services/BailianApiClient.cs`
   - 修改命名空间: `BiaogeCSharp.Services` → `BiaogPlugin.Services`
   - 复用率: 100%

3. **CacheService.cs** - SQLite翻译缓存
   - 复制自: `BiaogeCSharp/src/BiaogeCSharp/Services/CacheService.cs`
   - 修改命名空间: `BiaogeCSharp.Services` → `BiaogPlugin.Services`
   - 复用率: 100%

4. **ConfigManager.cs** - 配置管理
   - 复制自: `BiaogeCSharp/src/BiaogeCSharp/Services/ConfigManager.cs`
   - 修改命名空间: `BiaogeCSharp.Services` → `BiaogPlugin.Services`
   - 复用率: 100%

5. **ComponentRecognizer.cs** - 构件识别
   - 复制自: `BiaogeCSharp/src/BiaogeCSharp/Services/ComponentRecognizer.cs`
   - 修改命名空间: `BiaogeCSharp.Services` → `BiaogPlugin.Services`
   - 复用率: 90% (输入源从DwgDocument改为TextEntity列表)

6. **QuantityCalculator.cs** - 工程量计算
   - 复制自: `BiaogeCSharp/src/BiaogeCSharp/Services/QuantityCalculator.cs`
   - 修改命名空间: `BiaogeCSharp.Services` → `BiaogPlugin.Services`
   - 复用率: 100%

## 复制步骤

### 方法1: 在Visual Studio中直接复制

1. 打开两个Visual Studio实例
   - 实例1: 打开 `BiaogeCSharp.sln`
   - 实例2: 打开 `BiaogPlugin.sln`

2. 在实例1中，复制上述文件
3. 在实例2中，粘贴到 `Services/` 文件夹
4. 使用查找替换功能（Ctrl+H）:
   - 查找: `namespace BiaogeCSharp.Services`
   - 替换: `namespace BiaogPlugin.Services`
5. 解决using引用问题（添加必要的using语句）

### 方法2: 使用文件管理器

```bash
# 进入BiaogeCSharp Services目录
cd BiaogeCSharp/src/BiaogeCSharp/Services/

# 复制文件到BiaogPlugin
cp TranslationEngine.cs ../../../BiaogAutoCADPlugin/src/BiaogPlugin/Services/
cp BailianApiClient.cs ../../../BiaogAutoCADPlugin/src/BiaogPlugin/Services/
cp CacheService.cs ../../../BiaogAutoCADPlugin/src/BiaogPlugin/Services/
cp ConfigManager.cs ../../../BiaogAutoCADPlugin/src/BiaogPlugin/Services/
cp ComponentRecognizer.cs ../../../BiaogAutoCADPlugin/src/BiaogPlugin/Services/
cp QuantityCalculator.cs ../../../BiaogAutoCADPlugin/src/BiaogPlugin/Services/
```

然后在Visual Studio中批量替换命名空间。

## 修改注意事项

### TranslationEngine.cs
- 无需修改，直接复用

### BailianApiClient.cs
- 无需修改，直接复用

### CacheService.cs
- 确保SQLite数据库路径正确
- 路径应该设置为: `%APPDATA%\Biaoge\cache.db`

### ConfigManager.cs
- 确保配置文件路径正确
- 路径应该设置为: `%APPDATA%\Biaoge\config.json`

### ComponentRecognizer.cs
- 输入参数从 `DwgDocument` 改为 `List<TextEntity>`
- 其他逻辑保持不变

## 验证复用是否成功

复制后，在Visual Studio中：

1. 构建解决方案 (F6)
2. 检查错误列表，解决任何编译错误
3. 主要检查点:
   - 命名空间是否正确
   - using语句是否完整
   - Models类是否存在（如TranslationResult等）

## 当前状态

- [ ] TranslationEngine.cs - 待复用
- [ ] BailianApiClient.cs - 待复用
- [ ] CacheService.cs - 待复用
- [ ] ConfigManager.cs - 待复用
- [ ] ComponentRecognizer.cs - 待复用
- [ ] QuantityCalculator.cs - 待复用

复制完成后，请删除本README文件。
