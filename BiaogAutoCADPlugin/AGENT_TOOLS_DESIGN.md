# 标哥AutoCAD Agent - 完整工具集设计

## 设计理念

**目标**：让用户通过自然语言完成**所有**CAD工作，无需手动操作AutoCAD界面

**核心原则**：
1. **操作完整性** - 覆盖AutoCAD 90%+的常用功能
2. **精确性** - 使用AutoCAD .NET API确保100%精确
3. **安全性** - 所有修改操作需要事务和文档锁定
4. **可观测性** - 所有操作返回详细结果，便于AI总结

---

## 工具分类架构（共30+工具）

### 📐 1. 绘图工具（Drawing Tools）- 8个

#### 1.1 draw_line - 绘制直线
```json
{
  "name": "draw_line",
  "description": "在AutoCAD中绘制一条直线",
  "parameters": {
    "start_point": [x, y, z],  // 起点坐标
    "end_point": [x, y, z],    // 终点坐标
    "layer": "string",         // 图层名（可选）
    "color": "string"          // 颜色（可选，如"红色", "255,0,0", "ByLayer"）
  }
}
```

#### 1.2 draw_circle - 绘制圆
```json
{
  "name": "draw_circle",
  "description": "绘制一个圆",
  "parameters": {
    "center_point": [x, y, z],  // 圆心坐标
    "radius": 100.0,            // 半径
    "layer": "string",
    "color": "string"
  }
}
```

#### 1.3 draw_rectangle - 绘制矩形
```json
{
  "name": "draw_rectangle",
  "description": "绘制一个矩形（使用Polyline实现）",
  "parameters": {
    "corner1": [x, y],     // 第一个角点
    "corner2": [x, y],     // 对角点
    "layer": "string"
  }
}
```

#### 1.4 draw_polyline - 绘制多段线
```json
{
  "name": "draw_polyline",
  "description": "绘制多段线（支持闭合）",
  "parameters": {
    "points": [[x1,y1], [x2,y2], ...],  // 点列表
    "closed": true/false,               // 是否闭合
    "layer": "string"
  }
}
```

#### 1.5 draw_arc - 绘制圆弧
```json
{
  "name": "draw_arc",
  "description": "绘制圆弧",
  "parameters": {
    "center": [x, y, z],
    "radius": 100.0,
    "start_angle": 0.0,    // 起始角度（度数）
    "end_angle": 90.0,     // 结束角度（度数）
    "layer": "string"
  }
}
```

#### 1.6 draw_text - 添加文本
```json
{
  "name": "draw_text",
  "description": "在图纸中添加单行文本或多行文本",
  "parameters": {
    "position": [x, y],
    "text": "string",
    "height": 3.5,         // 文字高度
    "rotation": 0.0,       // 旋转角度（度数）
    "layer": "string",
    "text_type": "single"  // "single" 或 "mtext"
  }
}
```

#### 1.7 draw_hatch - 绘制填充
```json
{
  "name": "draw_hatch",
  "description": "创建填充图案",
  "parameters": {
    "boundary_ids": ["id1", "id2"],  // 边界实体ID
    "pattern": "SOLID",              // 填充图案（SOLID, ANSI31等）
    "scale": 1.0,
    "layer": "string"
  }
}
```

#### 1.8 draw_block - 插入块
```json
{
  "name": "draw_block",
  "description": "插入块参照",
  "parameters": {
    "block_name": "string",
    "position": [x, y, z],
    "scale": 1.0,
    "rotation": 0.0
  }
}
```

---

### 🔧 2. 修改工具（Modify Tools）- 10个

#### 2.1 delete_entity - 删除实体
```json
{
  "name": "delete_entity",
  "description": "删除一个或多个实体",
  "parameters": {
    "entity_ids": ["id1", "id2"],   // 实体ID列表
    "selection_criteria": {         // 或使用选择条件
      "type": "Line",               // 实体类型
      "layer": "图层名",
      "color": "红色"
    }
  }
}
```

#### 2.2 move_entity - 移动实体
```json
{
  "name": "move_entity",
  "description": "移动实体到新位置",
  "parameters": {
    "entity_ids": ["id1"],
    "from_point": [x, y, z],        // 基点
    "to_point": [x, y, z],          // 目标点
    "displacement": [dx, dy, dz]    // 或使用位移向量
  }
}
```

#### 2.3 copy_entity - 复制实体
```json
{
  "name": "copy_entity",
  "description": "复制实体",
  "parameters": {
    "entity_ids": ["id1"],
    "from_point": [x, y, z],
    "to_point": [x, y, z],
    "count": 1                      // 复制数量
  }
}
```

#### 2.4 rotate_entity - 旋转实体
```json
{
  "name": "rotate_entity",
  "description": "旋转实体",
  "parameters": {
    "entity_ids": ["id1"],
    "base_point": [x, y, z],        // 旋转基点
    "angle": 90.0                   // 旋转角度（度数）
  }
}
```

#### 2.5 scale_entity - 缩放实体
```json
{
  "name": "scale_entity",
  "description": "缩放实体",
  "parameters": {
    "entity_ids": ["id1"],
    "base_point": [x, y, z],
    "scale_factor": 2.0             // 缩放比例
  }
}
```

#### 2.6 mirror_entity - 镜像实体
```json
{
  "name": "mirror_entity",
  "description": "镜像实体",
  "parameters": {
    "entity_ids": ["id1"],
    "mirror_line_p1": [x1, y1],
    "mirror_line_p2": [x2, y2],
    "delete_source": false          // 是否删除原实体
  }
}
```

#### 2.7 offset_entity - 偏移实体
```json
{
  "name": "offset_entity",
  "description": "偏移曲线（Line, Polyline, Circle等）",
  "parameters": {
    "entity_id": "id",
    "offset_distance": 10.0,        // 偏移距离
    "side_point": [x, y]            // 偏移方向点
  }
}
```

#### 2.8 modify_entity_properties - 修改实体属性
```json
{
  "name": "modify_entity_properties",
  "description": "修改实体属性（颜色、图层、线型、线宽等）",
  "parameters": {
    "entity_ids": ["id1"],
    "layer": "新图层",
    "color": "红色",
    "linetype": "DASHED",
    "lineweight": 0.5
  }
}
```

#### 2.9 modify_text_content - 修改文本内容
```json
{
  "name": "modify_text_content",
  "description": "修改文本实体的内容（已实现为modify_drawing）",
  "parameters": {
    "entity_ids": ["id1"],
    "new_text": "新文本内容"
  }
}
```

#### 2.10 extend_trim_entity - 延伸/修剪实体
```json
{
  "name": "extend_trim_entity",
  "description": "延伸或修剪实体到边界",
  "parameters": {
    "entity_id": "id",
    "boundary_ids": ["id1", "id2"],
    "operation": "extend"           // "extend" 或 "trim"
  }
}
```

---

### 📊 3. 查询工具（Query Tools）- 8个

#### 3.1 query_entity_info - 查询实体信息
```json
{
  "name": "query_entity_info",
  "description": "查询单个实体的详细信息",
  "parameters": {
    "entity_id": "id",
    "info_type": "all"              // "all", "properties", "geometry"
  }
}
```

#### 3.2 measure_distance - 测量距离
```json
{
  "name": "measure_distance",
  "description": "测量两点之间的距离",
  "parameters": {
    "point1": [x1, y1, z1],
    "point2": [x2, y2, z2]
  }
}
```

#### 3.3 measure_area - 测量面积
```json
{
  "name": "measure_area",
  "description": "测量闭合区域的面积",
  "parameters": {
    "entity_id": "id"               // Polyline, Circle, Region等
  }
}
```

#### 3.4 list_entities - 列出实体
```json
{
  "name": "list_entities",
  "description": "列出符合条件的所有实体",
  "parameters": {
    "filter": {
      "type": "Line",
      "layer": "图层名",
      "color": "红色"
    },
    "limit": 100                    // 最多返回数量
  }
}
```

#### 3.5 get_entity_at_point - 获取点上的实体
```json
{
  "name": "get_entity_at_point",
  "description": "获取指定点处的实体",
  "parameters": {
    "point": [x, y],
    "tolerance": 1.0                // 拾取容差
  }
}
```

#### 3.6 query_layer_info - 查询图层信息
```json
{
  "name": "query_layer_info",
  "description": "查询图层的详细信息",
  "parameters": {
    "layer_name": "string"
  }
}
```

#### 3.7 count_entities - 统计实体数量
```json
{
  "name": "count_entities",
  "description": "统计符合条件的实体数量",
  "parameters": {
    "filter": {
      "type": "Line",
      "layer": "图层名"
    }
  }
}
```

#### 3.8 query_drawing_bounds - 查询图纸边界
```json
{
  "name": "query_drawing_bounds",
  "description": "获取当前图纸的边界范围",
  "parameters": {}
}
```

---

### 🗂️ 4. 图层工具（Layer Tools）- 4个

#### 4.1 create_layer - 创建图层
```json
{
  "name": "create_layer",
  "description": "创建新图层",
  "parameters": {
    "layer_name": "string",
    "color": "红色",
    "linetype": "Continuous",
    "lineweight": 0.25
  }
}
```

#### 4.2 delete_layer - 删除图层
```json
{
  "name": "delete_layer",
  "description": "删除图层（必须为空）",
  "parameters": {
    "layer_name": "string"
  }
}
```

#### 4.3 set_current_layer - 设置当前图层
```json
{
  "name": "set_current_layer",
  "description": "设置当前活动图层",
  "parameters": {
    "layer_name": "string"
  }
}
```

#### 4.4 modify_layer_properties - 修改图层属性
```json
{
  "name": "modify_layer_properties",
  "description": "修改图层属性（颜色、线型、可见性等）",
  "parameters": {
    "layer_name": "string",
    "color": "红色",
    "is_frozen": false,
    "is_locked": false,
    "is_off": false
  }
}
```

---

### 👁️ 5. 视图工具（View Tools）- 3个

#### 5.1 zoom_extents - 缩放到全部范围
```json
{
  "name": "zoom_extents",
  "description": "缩放视图以显示所有实体",
  "parameters": {}
}
```

#### 5.2 zoom_window - 窗口缩放
```json
{
  "name": "zoom_window",
  "description": "缩放到指定窗口范围",
  "parameters": {
    "corner1": [x1, y1],
    "corner2": [x2, y2]
  }
}
```

#### 5.3 pan_view - 平移视图
```json
{
  "name": "pan_view",
  "description": "平移视图",
  "parameters": {
    "displacement": [dx, dy]
  }
}
```

---

### 💾 6. 文件工具（File Tools）- 3个

#### 6.1 save_drawing - 保存图纸
```json
{
  "name": "save_drawing",
  "description": "保存当前图纸",
  "parameters": {
    "file_path": "string"           // 可选，不指定则原位保存
  }
}
```

#### 6.2 export_to_pdf - 导出为PDF
```json
{
  "name": "export_to_pdf",
  "description": "将当前图纸导出为PDF",
  "parameters": {
    "output_path": "string",
    "layout": "Model"               // "Model" 或 "Layout1"
  }
}
```

#### 6.3 import_block - 导入块
```json
{
  "name": "import_block",
  "description": "从外部文件导入块定义",
  "parameters": {
    "dwg_path": "string",
    "block_name": "string"
  }
}
```

---

### 🔢 7. 算量工具（Calculation Tools）- 2个

#### 7.1 recognize_components - 构件识别（已实现）
```json
{
  "name": "recognize_components",
  "description": "识别建筑构件并计算工程量",
  "parameters": {
    "component_types": ["柱", "梁", "板", "墙"]
  }
}
```

#### 7.2 calculate_total_area - 计算总面积
```json
{
  "name": "calculate_total_area",
  "description": "计算选定区域的总面积",
  "parameters": {
    "entity_ids": ["id1", "id2"]
  }
}
```

---

### 🌐 8. 翻译工具（Translation Tools）- 1个

#### 8.1 translate_text - 翻译文本（已实现）
```json
{
  "name": "translate_text",
  "description": "翻译CAD图纸中的文本",
  "parameters": {
    "text": "string",
    "target_language": "en"
  }
}
```

---

## 实现优先级

### P0 - 核心绘图和修改（必须立即实现）
1. draw_line
2. draw_circle
3. draw_rectangle
4. draw_polyline
5. draw_text
6. delete_entity
7. modify_entity_properties
8. move_entity
9. copy_entity

### P1 - 高级修改和查询（第二优先级）
10. rotate_entity
11. scale_entity
12. query_entity_info
13. measure_distance
14. measure_area
15. list_entities
16. create_layer
17. set_current_layer

### P2 - 增强功能（第三优先级）
18. draw_arc
19. draw_hatch
20. mirror_entity
21. offset_entity
22. zoom_extents
23. zoom_window
24. save_drawing

---

## AutoCAD .NET API 关键代码模式

### 创建实体的标准模式
```csharp
using (var docLock = doc.LockDocument())
using (var tr = db.TransactionManager.StartTransaction())
{
    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
    var modelSpace = (BlockTableRecord)tr.GetObject(
        bt[BlockTableRecord.ModelSpace],
        OpenMode.ForWrite
    );

    // 创建实体
    var line = new Line(startPoint, endPoint);
    line.Layer = "0";

    // 添加到模型空间
    modelSpace.AppendEntity(line);
    tr.AddNewlyCreatedDBObject(line, true);

    tr.Commit();
    return line.ObjectId;  // 返回ID供后续操作
}
```

### 修改实体的标准模式
```csharp
using (var docLock = doc.LockDocument())
using (var tr = db.TransactionManager.StartTransaction())
{
    var entity = tr.GetObject(objectId, OpenMode.ForWrite) as Entity;

    if (entity != null)
    {
        entity.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);  // 红色
        entity.Layer = "新图层";
    }

    tr.Commit();
}
```

### 删除实体的标准模式
```csharp
using (var docLock = doc.LockDocument())
using (var tr = db.TransactionManager.StartTransaction())
{
    var entity = tr.GetObject(objectId, OpenMode.ForWrite);
    entity.Erase();  // 标记删除

    tr.Commit();
}
```

---

## Agent系统提示词优化

```
你是标哥AutoCAD AI助手，一个强大的CAD Agent，能够理解用户的自然语言指令并执行各种AutoCAD操作。

你拥有30+个专业工具，涵盖：
- 绘图：线、圆、矩形、多段线、文本、填充等
- 修改：删除、移动、复制、旋转、缩放、镜像、属性修改等
- 查询：测量距离、测量面积、查询实体信息、统计等
- 图层：创建、删除、修改图层
- 视图：缩放、平移
- 文件：保存、导出PDF
- 算量：构件识别、工程量计算
- 翻译：多语言翻译

核心能力：
1. 理解用户意图，将自然语言转换为精确的工具调用
2. 使用AutoCAD .NET API保证100%精确操作
3. 支持复杂的多步骤任务（如"绘制一个房间"需要多次调用draw_line）
4. 提供清晰的执行反馈

工作原则：
- 所有坐标默认单位为毫米（mm）
- 所有角度使用度数（0-360）
- 默认在"0"图层绘制，除非用户指定
- 修改操作前先查询确认实体存在
- 提供详细的操作结果反馈
```

---

## 测试用例

### 用例1：绘制一个房间
用户："请绘制一个长6000mm，宽4000mm的房间"

AI执行：
1. draw_line: (0, 0) → (6000, 0)
2. draw_line: (6000, 0) → (6000, 4000)
3. draw_line: (6000, 4000) → (0, 4000)
4. draw_line: (0, 4000) → (0, 0)

反馈："已绘制完成，房间尺寸6000×4000mm"

### 用例2：删除所有红色的线
用户："删除图纸中所有红色的线"

AI执行：
1. list_entities: {type: "Line", color: "红色"}
2. delete_entity: [id1, id2, id3, ...]

反馈："已删除25条红色线"

### 用例3：创建新图层并绘制
用户："在'墙体'图层上绘制240mm厚的墙"

AI执行：
1. create_layer: {name: "墙体", color: "红色"}
2. draw_polyline: {points: [...], layer: "墙体"}

反馈："已创建'墙体'图层并绘制墙体"

---

## 总结

此设计将标哥AI助手从"对话工具"升级为"真正的AutoCAD Agent"，覆盖了AutoCAD 90%+的常用操作。

**核心价值**：
- 用户无需学习AutoCAD命令
- 通过自然语言完成所有CAD工作
- 支持复杂的多步骤任务
- 100%精确的AutoCAD .NET API操作

**下一步**：
1. 实现P0优先级工具（9个核心工具）
2. 更新GetAvailableTools()方法
3. 实现所有ExecuteXXXTool()方法
4. 深度测试确保无错误
