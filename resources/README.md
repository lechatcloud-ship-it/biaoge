# 资源文件目录

## 说明

此目录用于存放应用程序资源文件：

- `icon.png` - 应用图标（PNG格式，推荐512x512）
- `icon.ico` - Windows图标（ICO格式）
- `logo.png` - 应用Logo（用于关于对话框）
- `splash.png` - 启动画面（600x400）

## 图标生成

如需生成图标，可以使用以下工具：

```bash
# 从PNG生成ICO (Windows)
convert icon.png -define icon:auto-resize=256,128,64,48,32,16 icon.ico

# 或使用在线工具
# https://icoconvert.com/
# https://convertio.co/png-ico/
```

## 占位符

如果没有提供图标，应用程序将使用默认的文本占位符。
