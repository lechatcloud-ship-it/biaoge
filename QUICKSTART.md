# 快速开始指南

## 环境准备

### 1. 安装Python
确保安装了Python 3.11或更高版本：
```bash
python --version  # 应该显示 Python 3.11+
```

### 2. 创建虚拟环境（推荐）
```bash
# Windows
python -m venv venv
venv\Scripts\activate

# macOS/Linux
python3 -m venv venv
source venv/bin/activate
```

### 3. 安装依赖
```bash
pip install -r requirements.txt
```

**核心依赖**：
- PyQt6 >= 6.6.0 (GUI框架)
- PyQt-Fluent-Widgets >= 1.5.0 (Fluent Design UI)
- ezdxf >= 1.1.0 (DWG/DXF解析)
- numpy >= 1.26.0 (数值计算)
- numba >= 0.58.0 (JIT加速)

## 运行应用

### 方法1：使用启动脚本
```bash
python run.py
```

### 方法2：直接运行main.py
```bash
python src/main.py
```

### 方法3：作为模块运行
```bash
python -m src.main
```

## 项目结构

```
biaoge/
├── src/                    # 源代码
│   ├── ui/                 # UI界面
│   │   ├── main_window.py  # 主窗口
│   │   ├── welcome.py      # 欢迎界面
│   │   └── dwg_viewer.py   # DWG查看器
│   ├── dwg/                # DWG解析
│   │   ├── entities.py     # 实体模型
│   │   └── parser.py       # 解析器
│   ├── utils/              # 工具函数
│   │   ├── config_manager.py  # 配置管理
│   │   └── logger.py          # 日志系统
│   ├── config/             # 配置文件
│   │   └── default.toml    # 默认配置
│   └── main.py             # 主入口
├── requirements.txt        # Python依赖
├── run.py                  # 启动脚本
└── README-PyQt6.md         # 项目说明
```

## 功能测试

### 测试DWG解析
1. 启动应用
2. 点击左侧导航栏"图纸查看"
3. 点击"打开DWG文件"
4. 选择一个DWG/DXF文件
5. 查看解析结果（图层数、实体数等）

### 当前已实现功能
✅ 基础应用框架（PyQt6）
✅ Fluent Design UI（可选）
✅ DWG/DXF解析（ezdxf）
✅ 配置管理（TOML）
✅ 日志系统

### 开发中功能
🚧 图纸渲染（QPainter）
🚧 翻译引擎（阿里云百炼）
🚧 算量计算（AI识别+Numba加速）
🚧 多格式导出（DWG/PDF/Excel）

## 常见问题

### Q: PyQt-Fluent-Widgets安装失败？
A: 尝试使用国内镜像源：
```bash
pip install PyQt-Fluent-Widgets -i https://pypi.tuna.tsinghua.edu.cn/simple
```

### Q: 应用启动后是基础UI而不是Fluent Design？
A: 检查PyQt-Fluent-Widgets是否安装成功：
```bash
python -c "import qfluentwidgets; print('OK')"
```

### Q: 无法导入DWG文件？
A: 确保文件格式为DWG或DXF，且版本为R12-R2024

## 开发说明

### 添加新界面
1. 在`src/ui/`下创建新的界面类
2. 继承自`QWidget`或`ScrollArea`
3. 在`main_window.py`中注册到导航栏

### 配置管理
配置文件位置：
- 默认配置：`src/config/default.toml`
- 用户配置：`~/.biaoge/config.toml`

修改配置：
```python
from src.utils.config_manager import config

# 读取配置
value = config.get('api.model', 'qwen-plus')

# 修改配置
config.set('api.model', 'qwen-max')
config.save()
```

### 日志系统
```python
from src.utils.logger import logger

logger.info("信息日志")
logger.warning("警告日志")
logger.error("错误日志")
```

日志文件位置：`~/.biaoge/logs/app.log`

## 下一步

查看完整文档：
- [架构设计文档](docs/01-架构设计文档-PyQt6.md)
- [技术选型文档](docs/03-技术选型与最佳实践-PyQt6.md)
- [项目说明](README-PyQt6.md)

## 技术支持

遇到问题？
1. 查看日志：`~/.biaoge/logs/app.log`
2. 提交Issue：https://github.com/yourusername/biaoge/issues
3. 查看文档：`docs/`目录

---

**开始开发吧！** 🚀
