# 标哥AutoCAD插件 - AI Agent工具目录

**版本**: v1.1.0
**工具总数**: 30个
**最后更新**: 2025-11-17

---

## 工具分类

### P0 - 核心工具 (9个)
基础绘图和修改工具,是AI Agent的核心能力。

### P1 - 高级工具 (10个)
查询、图层管理、高级修改工具,扩展AI Agent的能力范围。

### P2 - 专业工具 (11个)
视图控制、文件操作、高级修改工具,提供完整的CAD工作流支持。

---

## P0.1 绘图工具 (5个)

### 1. draw_line - 绘制直线
**描述**: 在AutoCAD中绘制一条直线
**参数**:
- `start_point` (array, required): 起点坐标[x, y, z]，单位mm
- `end_point` (array, required): 终点坐标[x, y, z]，单位mm
- `layer` (string, optional): 图层名，默认'0'
- `color` (string, optional): 颜色，支持中文如'红色'、RGB如'255,0,0'或'ByLayer'

**示例**:
```json
{
  "start_point": [0, 0, 0],
  "end_point": [100, 100, 0],
  "layer": "轮廓线",
  "color": "红色"
}
```

### 2. draw_circle - 绘制圆
**描述**: 在AutoCAD中绘制圆
**参数**:
- `center` (array, required): 圆心坐标[x, y, z]，单位mm
- `radius` (number, required): 半径，单位mm
- `layer` (string, optional): 图层名
- `color` (string, optional): 颜色

### 3. draw_rectangle - 绘制矩形
**描述**: 在AutoCAD中绘制矩形
**参数**:
- `corner1` (array, required): 第一个角点坐标[x, y]，单位mm
- `corner2` (array, required): 对角点坐标[x, y]，单位mm
- `layer` (string, optional): 图层名
- `color` (string, optional): 颜色

### 4. draw_polyline - 绘制多段线
**描述**: 在AutoCAD中绘制多段线（连续的线段）
**参数**:
- `points` (array, required): 顶点坐标数组[[x1,y1], [x2,y2], ...]，单位mm
- `closed` (boolean, optional): 是否闭合，默认false
- `layer` (string, optional): 图层名
- `color` (string, optional): 颜色

### 5. draw_text - 添加文本
**描述**: 在AutoCAD中添加文本标注
**参数**:
- `position` (array, required): 文本插入点[x, y, z]，单位mm
- `text` (string, required): 文本内容
- `height` (number, optional): 文字高度，单位mm，默认2.5mm
- `layer` (string, optional): 图层名
- `color` (string, optional): 颜色

---

## P0.2 修改工具 (4个)

### 6. delete_entity - 删除实体
**描述**: 删除AutoCAD中的图形实体
**参数**:
- `entity_ids` (array, required): 实体ID列表（Handle，十六进制字符串）

### 7. modify_entity_properties - 修改实体属性
**描述**: 修改AutoCAD实体的属性（颜色、图层等）
**参数**:
- `entity_ids` (array, required): 实体ID列表
- `layer` (string, optional): 新图层名
- `color` (string, optional): 新颜色

### 8. move_entity - 移动实体
**描述**: 移动AutoCAD实体到新位置
**参数**:
- `entity_ids` (array, required): 实体ID列表
- `displacement` (array, required): 位移向量[dx, dy, dz]，单位mm

### 9. copy_entity - 复制实体
**描述**: 复制AutoCAD实体到新位置
**参数**:
- `entity_ids` (array, required): 实体ID列表
- `displacement` (array, required): 位移向量[dx, dy, dz]，单位mm

---

## P1.1 查询工具 (4个)

### 10. measure_distance - 测量距离
**描述**: 测量两点之间的距离
**参数**:
- `point1` (array, required): 第一个点坐标[x, y, z]，单位mm
- `point2` (array, required): 第二个点坐标[x, y, z]，单位mm

### 11. measure_area - 测量面积
**描述**: 测量多边形区域的面积
**参数**:
- `points` (array, required): 多边形顶点[[x1,y1], [x2,y2], ...]，单位mm

### 12. list_entities - 列出实体
**描述**: 列出图纸中的所有实体
**参数**:
- `entity_type` (string, optional): 实体类型过滤，如'Line', 'Circle', 'Text'
- `layer` (string, optional): 图层名过滤

### 13. count_entities - 统计实体数量
**描述**: 统计图纸中的实体数量
**参数**:
- `entity_type` (string, optional): 实体类型过滤
- `layer` (string, optional): 图层名过滤

---

## P1.2 图层工具 (4个)

### 14. create_layer - 创建图层
**描述**: 创建新图层
**参数**:
- `layer_name` (string, required): 图层名称
- `color` (string, optional): 图层颜色，默认白色

### 15. set_current_layer - 设置当前图层
**描述**: 设置当前工作图层
**参数**:
- `layer_name` (string, required): 图层名称

### 16. modify_layer_properties - 修改图层属性
**描述**: 修改图层属性
**参数**:
- `layer_name` (string, required): 图层名称
- `color` (string, optional): 新颜色
- `is_frozen` (boolean, optional): 是否冻结
- `is_locked` (boolean, optional): 是否锁定

### 17. query_layer_info - 查询图层信息
**描述**: 查询图层详细信息
**参数**:
- `layer_name` (string, optional): 图层名称，不指定则返回所有图层

---

## P1.3 高级修改工具 (2个)

### 18. rotate_entity - 旋转实体
**描述**: 旋转AutoCAD实体
**参数**:
- `entity_ids` (array, required): 实体ID列表
- `base_point` (array, required): 旋转基点[x, y, z]，单位mm
- `angle` (number, required): 旋转角度（度，逆时针为正）

### 19. scale_entity - 缩放实体
**描述**: 缩放AutoCAD实体
**参数**:
- `entity_ids` (array, required): 实体ID列表
- `base_point` (array, required): 缩放基点[x, y, z]，单位mm
- `scale_factor` (number, required): 缩放比例（>1放大，<1缩小）

---

## P2.1 视图工具 (3个)

### 20. zoom_extents - 全图显示
**描述**: 全图显示（缩放到所有实体的范围）
**参数**: 无

**示例**:
```
用户: "把图纸全部显示出来"
AI: 调用zoom_extents工具 → 执行成功
```

### 21. zoom_window - 窗口缩放
**描述**: 窗口缩放（缩放到指定矩形区域）
**参数**:
- `corner1` (array, required): 窗口第一个角点[x, y, z]，单位mm
- `corner2` (array, required): 窗口对角点[x, y, z]，单位mm

### 22. pan_view - 平移视图
**描述**: 平移视图
**参数**:
- `displacement` (array, required): 平移向量[dx, dy, dz]，单位mm

---

## P2.2 文件工具 (2个)

### 23. save_drawing - 保存图纸
**描述**: 保存图纸（保存或另存为）
**参数**:
- `file_path` (string, optional): 文件路径，不指定则保存当前文件

**示例**:
```json
// 保存当前文件
{}

// 另存为
{
  "file_path": "C:/Projects/建筑图纸_v2.dwg"
}
```

### 24. export_to_pdf - 导出PDF
**描述**: 导出图纸为PDF文件
**参数**:
- `output_path` (string, required): PDF输出路径（含文件名）

**示例**:
```json
{
  "output_path": "C:/Projects/建筑图纸.pdf"
}
```

---

## P2.3 高级修改工具 (6个)

### 25. mirror_entity - 镜像实体
**描述**: 镜像AutoCAD实体
**参数**:
- `entity_ids` (array, required): 实体ID列表
- `mirror_line_point1` (array, required): 镜像线起点[x, y, z]，单位mm
- `mirror_line_point2` (array, required): 镜像线终点[x, y, z]，单位mm
- `erase_source` (boolean, optional): 是否删除原实体，默认false

**示例**:
```json
{
  "entity_ids": ["1F3", "1F4"],
  "mirror_line_point1": [0, 0, 0],
  "mirror_line_point2": [0, 100, 0],
  "erase_source": false
}
```

### 26. offset_entity - 偏移实体
**描述**: 偏移曲线实体（Line, Circle, Arc, Polyline等）
**参数**:
- `entity_ids` (array, required): 实体ID列表
- `distance` (number, required): 偏移距离，单位mm（正值向外，负值向内）

**AutoCAD API参考**:
- 使用`Curve.GetOffsetCurves(double offsetDistance)`方法
- 支持Line、Circle、Arc、Polyline、Ellipse等曲线实体

### 27. trim_entity - 修剪实体
**描述**: 修剪实体（交互式操作）
**参数**:
- `cutting_edge_ids` (array, required): 切割边实体ID列表
- `entity_to_trim_ids` (array, required): 待修剪实体ID列表

**注意**: 此工具需要AutoCAD的TRIM命令交互式操作

### 28. extend_entity - 延伸实体
**描述**: 延伸实体到边界（交互式操作）
**参数**:
- `boundary_edge_ids` (array, required): 边界实体ID列表
- `entity_to_extend_ids` (array, required): 待延伸实体ID列表

**注意**: 此工具需要AutoCAD的EXTEND命令交互式操作

### 29. fillet_entity - 圆角
**描述**: 在两个曲线之间创建圆角
**参数**:
- `entity_ids` (array, required): 两个实体ID
- `radius` (number, required): 圆角半径，单位mm

**AutoCAD命令**: `_.FILLET _R {radius} {entity1} {entity2}`

### 30. chamfer_entity - 倒角
**描述**: 在两个曲线之间创建倒角
**参数**:
- `entity_ids` (array, required): 两个实体ID
- `distance1` (number, required): 第一条线上的倒角距离，单位mm
- `distance2` (number, required): 第二条线上的倒角距离，单位mm

**AutoCAD命令**: `_.CHAMFER _D {distance1} {distance2} {entity1} {entity2}`

---

## 实现技术细节

### AutoCAD .NET API 最佳实践
所有工具遵循标准AutoCAD .NET API模式:

```csharp
// 1. 事务模式 (Transaction Pattern)
using (var tr = db.TransactionManager.StartTransaction())
{
    // 读取/修改DWG数据
    tr.Commit(); // 或 tr.Abort()
}

// 2. 文档锁定 (Document Lock)
using (var docLock = doc.LockDocument())
{
    // 写入操作必须加锁
}
```

### 阿里云百炼 Function Calling
所有工具定义遵循OpenAI兼容的Function Calling规范:

- **type**: "function"
- **function.name**: 工具名称（snake_case）
- **function.description**: 工具描述（中文）
- **function.parameters**: JSON Schema定义参数

### 颜色支持
支持三种颜色格式:

1. **中文名称**: "红色", "黄色", "绿色", "青色", "蓝色", "品红", "白色"
2. **英文名称**: "red", "yellow", "green", "cyan", "blue", "magenta", "white"
3. **RGB格式**: "255,0,0"（逗号分隔）
4. **ByLayer**: 使用图层颜色

---

## 使用场景

### 场景1: 绘制建筑平面图
```
用户: "在坐标(0,0)绘制一个5000x3000的矩形房间，然后在(1000,1500)画一个直径800的圆形立柱"

AI执行:
1. draw_rectangle({corner1: [0,0], corner2: [5000,3000], layer: "墙体"})
2. draw_circle({center: [1000,1500,0], radius: 400, layer: "柱子"})
3. zoom_extents({}) // 全图显示
```

### 场景2: 批量修改图层
```
用户: "把所有圆改到'结构'图层，颜色改成红色"

AI执行:
1. list_entities({entity_type: "Circle"}) // 获取所有圆的ID
2. modify_entity_properties({entity_ids: [...], layer: "结构", color: "红色"})
```

### 场景3: 镜像复制
```
用户: "把这些实体沿Y轴镜像复制一份"

AI执行:
1. mirror_entity({
     entity_ids: [...],
     mirror_line_point1: [0,0,0],
     mirror_line_point2: [0,100,0],
     erase_source: false
   })
```

---

## 参考文档

- **AutoCAD .NET API**: https://help.autodesk.com/view/OARX/2025/ENU/
- **阿里云百炼Function Calling**: https://help.aliyun.com/zh/model-studio/qwen-function-calling
- **项目设计文档**: PRODUCT_DESIGN.md, AGENT_TOOLS_DESIGN.md

---

## 版本历史

### v1.1.0 (2025-11-17)
- ✅ 新增P2工具集(11个): 视图工具、文件工具、高级修改工具
- ✅ 工具总数达到30个
- ✅ 完整的AutoCAD工作流支持

### v1.0.0 (2025-11-16)
- ✅ 实现P0/P1工具集(19个)
- ✅ 基础绘图、修改、查询、图层工具
- ✅ 与阿里云百炼qwen3-coder-flash集成

---

**END**
