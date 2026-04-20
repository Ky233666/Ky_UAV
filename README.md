# Ky_UAV

基于 Unity 的多无人机协同巡检与路径规划仿真项目，当前收口目标为“毕设答辩可稳定演示、实验结果可复现、交付材料可直接归档”。

## 当前范围

主场景为 `Assets/Scenes/Main/MainScene.unity`，并已写入构建场景配置。当前版本保留以下正式能力：

- 多无人机生成、重建、任务分配、状态控制与重置
- 调度算法切换：`EvenSplit`、`GreedyNearest`、`PriorityGreedy`
- 路径规划算法切换：`StraightLine`、`AStar`、`RRT`
- 任务点运行时新增、清空、导入
- 起飞点运行时新增、移动、删除、清空
- 总览/跟随相机切换与多机观察
- 规划路径与飞行轨迹可视化
- CSV/JSON 单次导出与批量实验导出
- session 级 `session_manifest.json` 与 `session_summary.csv`
- 面向打包版的运行时控制面板
- 运行时实验中心：预设分组浏览、实验矩阵切换、预设应用、预设批量与当前配置批量运行

当前阶段不继续扩展联网、强化学习、复杂动力学等高风险内容。

## 环境

- Unity `2022.3.62f3`
- C#

## 快速开始

1. 使用 Unity Hub 打开项目。
2. 打开 `Assets/Scenes/Main/MainScene.unity`。
3. 首次收口初始化时执行 `Tools/KY UAV/Bootstrap Delivery Assets`，生成 `DroneConfig` 和实验预设资产。
4. 进入 Play Mode。
5. 使用底部和右侧运行时面板完成任务导入、算法切换、开始、暂停、重置、导出和批量实验。

## 验证入口

- `Tools/KY UAV/Bootstrap Delivery Assets`
  - 生成默认 `DroneConfig` 资产和 4 组实验矩阵对应的 `ExperimentPreset` 资产。
- `Tools/KY UAV/Run Project Smoke Validation`
  - 打开 `MainScene`，检查 `SimulationManager`、`DroneManager`、`CameraManager`、`TaskPointImporter`、`Buildings`、`Canvas` 等关键对象及引用。

如果要在命令行执行这两个入口，必须先关闭正在占用该项目的 Unity 编辑器实例，否则 batchmode 会被项目锁直接拦截。

## 主要交互

### 仿真控制

- `开始仿真`：按当前调度器和规划器分配任务并启动机群
- `暂停`：暂停当前仿真
- `重置`：清理任务状态、路径缓存、轨迹和统计并回到就绪态
- `重建机群`：按面板数量重新生成无人机并走完整重置链路

### 任务与视角

- `新增任务点` / `导入任务` / `清空任务`
- `1` 切总览，`2` 切跟随
- `Tab` / `E` 切下一架无人机，`Q` 切上一架
- `W/A/S/D` 平移，`R/F` 升降，鼠标滚轮缩放，右键旋转总览视角

### 运行时面板

右侧运行时面板已覆盖答辩演示常用操作：

- 调度算法与路径规划算法切换
- 无人机数量调整与机群重建
- 飞行速度与仿真倍速调整
- 规划边界与路径显示相关设置
- 总览/跟随/下一架无人机切换
- 导出目录切换
- 批量实验启动/停止与预设状态显示
- 实验中心内按分组浏览 `Scheduling / Planning / Scaling / Density` 预设并直接启动

## 实验导出

- 默认导出根目录：`Application.persistentDataPath/ExperimentResults`
- 默认归档结构：`根目录 / 日期 / 会话`
- 单次导出：
  - CSV 摘要
  - JSON 详细结果
- 批量实验额外导出：
  - `session_manifest.json`
  - `session_summary.csv`

固定实验矩阵见 [ExecutionPlan.md](/D:/unityhub/project/Ky_UAV/Assets/Docs/ExecutionPlan.md) 与 [ExperimentExecutionGuide.md](/D:/unityhub/project/Ky_UAV/Assets/Docs/ExperimentExecutionGuide.md)。

## 关键脚本

- `Assets/Scripts/UAV/Controller/SimulationManager.cs`
- `Assets/Scripts/UAV/Controller/DroneManager.cs`
- `Assets/Scripts/UAV/Controller/DroneStateMachine.cs`
- `Assets/Scripts/UAV/Controller/SimulationRuntimeControlPanel.cs`
- `Assets/Scripts/UAV/Controller/SimulationResultExporter.cs`
- `Assets/Scripts/UAV/Controller/BatchExperimentRunner.cs`
- `Assets/Scripts/UAV/Config/DroneConfig.cs`
- `Assets/Scripts/UAV/Model/ExperimentPreset.cs`
- `Assets/Editor/KyUavDeliveryAssetTools.cs`
- `Assets/Editor/ProjectSmokeValidator.cs`

## 文档入口

- 执行计划：[Assets/Docs/ExecutionPlan.md](/D:/unityhub/project/Ky_UAV/Assets/Docs/ExecutionPlan.md)
- 项目现状：[Assets/Docs/ProjectStatusAndRoadmap.md](/D:/unityhub/project/Ky_UAV/Assets/Docs/ProjectStatusAndRoadmap.md)
- 功能测试表：[Assets/Docs/FunctionTestChecklist.md](/D:/unityhub/project/Ky_UAV/Assets/Docs/FunctionTestChecklist.md)
- 稳定性记录：[Assets/Docs/StabilityTestRecord.md](/D:/unityhub/project/Ky_UAV/Assets/Docs/StabilityTestRecord.md)
- 实验执行说明：[Assets/Docs/ExperimentExecutionGuide.md](/D:/unityhub/project/Ky_UAV/Assets/Docs/ExperimentExecutionGuide.md)
- 答辩演示脚本：[Assets/Docs/DefenseDemoScript.md](/D:/unityhub/project/Ky_UAV/Assets/Docs/DefenseDemoScript.md)
