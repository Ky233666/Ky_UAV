# 任务点导入功能教程

---

## 1. 推荐的数据格式

支持两种格式：**CSV**（简单）和 **JSON**（规范）。

### 格式一：CSV（推荐，最简单）

```csv
taskId,taskName,x,z,priority,description
1,巡检点A,0,0,1,入口检查
2,巡检点B,10,5,2,设备检查
3,巡检点C,20,10,1,出口检查
```

| 列 | 说明 | 是否必填 |
|---|---|---|
| taskId | 任务点 ID | 必填 |
| taskName | 任务点名称 | 必填 |
| x | X 坐标 | 必填 |
| z | Z 坐标 | 必填 |
| priority | 优先级（越大越优先） | 可选 |
| description | 描述 | 可选 |

### 格式二：JSON

```json
{
  "taskPoints": [
    { "taskId": 1, "taskName": "巡检点A", "x": 0, "z": 0, "priority": 1, "description": "入口检查" },
    { "taskId": 2, "taskName": "巡检点B", "x": 10, "z": 5, "priority": 2, "description": "设备检查" },
    { "taskId": 3, "taskName": "巡检点C", "x": 20, "z": 10, "priority": 1, "description": "出口检查" }
  ]
}
```

或纯数组格式：

```json
[
  { "taskId": 1, "taskName": "巡检点A", "x": 0, "z": 0 },
  { "taskId": 2, "taskName": "巡检点B", "x": 10, "z": 5 }
]
```

---

## 2. 实现流程

```
1. 创建数据文件（CSV 或 JSON）
      ↓
2. 放入 Resources 文件夹
      ↓
3. 创建 TaskPointImporter 对象
      ↓
4. 配置 Spawner 引用
      ↓
5. 调用 ImportFromResources() 导入
```

---

## 3. 需要新建的脚本

| 脚本 | 作用 |
|---|---|
| `TaskPointData.cs` | 任务点数据结构 |
| `TaskPointImporter.cs` | 文件读取与解析 |

---

## 4. 完整代码

已创建两个脚本：

- `TaskPointData.cs`：任务点数据结构
- `TaskPointImporter.cs`：导入器，支持 CSV、JSON 自动识别

---

## 5. Unity 中配置步骤

### 步骤 1：创建数据文件

在 `Assets/Resources/` 目录下新建 `taskpoints.csv`（或 `taskpoints.json`）。

**示例 CSV：**

```
taskId,taskName,x,z,priority,description
1,巡检点A,0,0,1,入口检查
2,巡检点B,10,5,2,设备检查
3,巡检点C,20,10,1,出口检查
4,巡检点D,15,15,3,巡检终点
```

> 注意：没有 `Resources` 文件夹的话，自己在 `Assets` 下新建一个。

### 步骤 2：创建 Importer 对象

1. 在 Hierarchy 右键 → Create Empty，命名为 `TaskPointImporter`。
2. 把 `Assets/Scripts/UAV/Controller/TaskPointImporter.cs` 拖上去。

### 步骤 3：配置 Importer

选中 `TaskPointImporter`，在 Inspector 中：

| 属性 | 设置 |
|---|---|
| Spawner | 拖入场景里的 `TaskPointSpawner` 对象 |
| File Name | `taskpoints`（不含扩展名，即 Resources 里的文件名） |

### 步骤 4：绑定 UI 按钮（已有 Importer 引用）

1. 选中 `TaskPointUIManager`（你的 UI 管理器对象）。
2. 在 Inspector 找到 **Task Point Importer**，拖入 `TaskPointImporter` 对象。
3. 在 Hierarchy 或 Canvas 里新建一个按钮（比如叫 `Btn_Import`）。
4. 把这个按钮拖到 `TaskPointUIManager` 的 **Import Button** 槽位。

> 运行后点这个按钮，就会从 `Resources/taskpoints.csv` 导入任务点。

### 步骤 5：测试

1. 运行 Unity。
2. 点「导入」按钮。
3. 场景里应该出现对应位置的任务点。
4. 点「清除」后再点「导入」，可以重新导入。

---

## 6. 与现有任务点结构兼容

新建的脚本已经兼容现有结构：

| 现有字段 | 导入时赋值 |
|---|---|
| `taskId` | ✅ 从文件读取 |
| `taskName` | ✅ 从文件读取 |
| `priority` | ✅ 从文件读取 |
| `description` | ✅ 从文件读取 |
| `position` | ✅ 通过 `ToPosition()` 转换 |
| 其他运行时字段 | 由 Spawner 和 TaskPoint 自身初始化 |

`TaskPointSpawner.SpawnTaskPoint()` 会创建对象并返回 `TaskPoint` 组件，导入器拿到后直接赋值 `taskId`、`taskName`、`priority`、`description`，完全兼容。

---

## 常见问题

### Q：文件放哪里？
A：必须放在 `Assets/Resources/` 目录下。导入时写文件名**不含扩展名**（如 `taskpoints`），代码会自动识别 CSV 或 JSON。

### Q：为什么导入后没反应？
A：检查 Console 有没有报错。常见原因：
- 文件名拼写不对
- 文件不在 Resources 里
- CSV 表头不对（必须有一行表头）

### Q：怎么指定文件路径？
A：目前设计是从 Resources 读取，这样最简单。如果要从其他路径读取，可以调用 `ImportFromPath("完整路径")`，但当前最小实现先用 Resources。
