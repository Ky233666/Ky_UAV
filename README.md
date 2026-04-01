# Ky_UAV

基于 Unity 的多无人机协同巡检与路径规划仿真项目。

## 项目概览

当前主场景为 `Assets/Scenes/Main/MainScene.unity`，核心能力包括：

- 多无人机生成、任务分配与状态控制
- 调度算法切换：`EvenSplit`、`GreedyNearest`
- 路径规划算法切换：`StraightLine`、`AStar`
- 任务点运行时添加、清空、导入
- 总览/跟随相机切换与多机观察
- 规划路径与实际飞行轨迹可视化
- 面向打包版的运行时控制面板

## 运行环境

- Unity `2022.3.62f3`
- C#

## 快速开始

1. 使用 Unity Hub 打开项目。
2. 打开 `Assets/Scenes/Main/MainScene.unity`。
3. 进入 Play Mode。
4. 使用底部按钮开始、暂停或重置仿真。

## 主要交互

### 仿真控制

- `开始仿真`：为当前任务点分配任务并启动无人机
- `暂停`：暂停当前仿真
- `重置`：重置无人机和状态

### 任务点工具

- `新增任务点`：进入点击放置模式
- `导入任务`：从资源数据导入任务点
- `清空任务`：删除当前任务点

### 视角控制

- `1`：切换到总览视角
- `2`：切换到跟随视角
- `Tab` / `E`：切换到下一架无人机
- `Q`：切换到上一架无人机
- `W/A/S/D`：总览视角平移
- `R/F`：总览视角升降
- 鼠标滚轮：缩放
- 按住右键：旋转总览视角
- `Shift`：总览视角加速移动

## 运行时控制面板

右侧运行时面板用于替代部分 Inspector 调参，适合打包后展示和交互。

当前已支持：

- 调度算法切换
- 路径规划算法切换
- 无人机数量调整与机群重建
- 飞行速度调整
- 仿真倍速调整
- 规划路径显示开关
- 飞行轨迹显示开关
- 总览/跟随/下一架无人机切换

## 主要脚本

- `Assets/Scripts/UAV/Controller/SimulationManager.cs`
- `Assets/Scripts/UAV/Controller/DroneManager.cs`
- `Assets/Scripts/UAV/Controller/CameraManager.cs`
- `Assets/Scripts/UAV/Controller/TaskPointUIManager.cs`
- `Assets/Scripts/UAV/Controller/SimulationRuntimeControlPanel.cs`
- `Assets/Scripts/UAV/Controller/DronePathVisualizer.cs`
- `Assets/Scripts/UAV/Comm/AStarPlanner.cs`
- `Assets/Scripts/UAV/Comm/GreedyNearestScheduler.cs`

## 文档

- 项目进度与路线图：`Assets/Docs/ProjectStatusAndRoadmap.md`
