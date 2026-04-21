# 模块设计

## 模块总览

| 模块 | 责任 | 输入 | 输出 | 依赖关系 | 当前完成程度 |
| --- | --- | --- | --- | --- | --- |
| 场景与环境管理模块 | 维护主场景、城市环境、建筑障碍层、总览环境参数 | 主场景、城市资源包、编辑器菜单命令 | 可运行的城市主场景 | 依赖 `SimpleCitySceneImporter`、`SimpleCityEnvironmentTools`、`DroneManager` | 已完成 |
| 仿真控制模块 | 管理系统状态、开始、暂停、重置、自动补齐运行时组件 | 用户按钮、快捷键、当前任务集合 | `Idle / Running / Paused` 状态切换 | 依赖 `SimulationManager`、`DroneManager`、`SimulationResultExporter` | 已完成 |
| 无人机管理模块 | 生成机群、重建机群、保存无人机数据、统一调度与规划调用 | `DroneConfig`、起飞点、算法枚举、规划参数 | `drones`、`droneDataList`、任务队列、路径数据 | 依赖 `DroneController`、`DroneStateMachine`、算法接口 | 已完成 |
| 单机执行模块 | 推进单机任务状态、请求路径、执行位移、记录等待/冲突 | 当前任务、路径结果、仿真状态 | 单机状态、实时位置、统计计数 | 依赖 `DroneController`、`DroneStateMachine`、`DroneData` | 已完成，避让为第一版 |
| 任务管理模块 | 生成任务点、导入任务数据、维护任务状态 | 鼠标点击、CSV/JSON 数据、默认任务文件 | `TaskPoint` 集合 | 依赖 `TaskPointUIManager`、`TaskPointSpawner`、`TaskPointImporter` | 已完成 |
| 起飞点管理模块 | 运行时维护起飞点布局和顺序 | 鼠标点击、起飞点编辑模式 | `DroneSpawnPointMarker` 集合 | 依赖 `DroneSpawnPointUIManager`、`DroneManager`、`CameraManager` | 已完成 |
| 规划与调度模块 | 提供可替换算法接口与实现 | 调度请求、规划请求 | 调度结果、路径结果 | 依赖 `ISchedulerAlgorithm`、`IPathPlanner`、相关模型类 | 已完成 |
| 可视化展示模块 | 展示路径、轨迹、相机视图、建筑告警和运行统计 | 路径数据、无人机状态、建筑 footprint | 3D/2D 路径线、摘要与统计 | 依赖 `CameraManager`、`DronePathVisualizer`、`SimulationRuntimeControlPanel` | 已完成 |
| 数据管理模块 | 导出单次结果、归档会话、汇总批量实验 | 当前仿真数据、预设、导出目录 | CSV、JSON、manifest、summary | 依赖 `SimulationResultExporter`、`BatchExperimentRunner`、模型类 | 已完成 |
| 交互界面模块 | 提供场景按钮和右侧运行时控制面板 | 用户点击、快捷键 | 参数更新、控制命令、状态显示 | 依赖 `TaskPointUIManager`、`SimulationRuntimeControlPanel` | 已完成 |
| 编辑器工具模块 | 初始化资产、烟雾验证、批量测试、打包 | 编辑器菜单命令 | 资产、验证日志、测试结果、Windows 包 | 依赖 `KyUavDeliveryAssetTools`、`ProjectSmokeValidator`、`KyUavBuildTools` | 已完成 |

## 说明

- `TaskManager` 仍然存在，但在当前主流程中只作为早期兼容辅助，不再是核心任务分配入口。
- 调度和规划算法模块与 UI、导出、相机解耦，便于论文中把“算法能力”和“系统功能”分开描述。
- 多机避让逻辑位于 `DroneStateMachine` 内部，尚未独立成单独模块，因此文档中只应描述为“局部冲突处理”，不应写成完整协同避障模块。
