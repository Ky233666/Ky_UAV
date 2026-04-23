# 功能说明

本文档只描述当前代码中已经存在或已被明确验证的系统功能，不把计划功能写成已实现功能。

## 状态标记

- `已实现`：代码已接入主流程，可在当前工程中使用。
- `部分实现`：代码已存在，但覆盖范围有限、仅为第一版，或需要人工复核。
- `未实现`：当前仓库中没有对应功能闭环。

## 1. 场景与环境初始化

### 功能名称

默认场景加载与环境就绪

### 功能目标

在默认主场景或自定义障碍物实验场景中完成仿真运行前的基础环境准备，包括城市建筑或空白地面、障碍层、相机、Canvas 和核心管理器。

### 主要使用流程

1. 打开 `Assets/Scenes/Main/MainScene.unity`，或打开 `Assets/Scenes/Sandbox/CustomObstacleSandbox.unity`
2. 执行 `SimulationManager.Start`
3. 自动挂接运行时控制面板、导出器、批量实验器、起飞点管理器和自定义障碍物编辑器
4. `DroneManager` 自动配置障碍物根节点和建筑碰撞体

### 输入

- `MainScene` 或 `CustomObstacleSandbox` 场景资源
- `Buildings`、`Canvas`、`OverviewCamera`、`FollowCamera`

### 处理

- `SimulationManager` 初始化仿真状态
- `DroneManager` 初始化规划障碍、配置默认参数并生成无人机
- `RuntimeObstacleEditor` 连接 `DroneManager` 与运行时控制面板

### 输出

- 进入 `Idle` 状态的可运行仿真环境
- 若为 `CustomObstacleSandbox`，则保留空的 `Buildings` 容器和原有起飞点/运行时功能链路

### 当前状态

`已实现`

### 依赖模块

- `SimulationManager`
- `DroneManager`
- `CameraManager`
- `ProjectSmokeValidator`

## 2. 场景导入与环境修复

### 功能名称

城市场景导入、环境修复与自定义障碍物实验场景生成

### 功能目标

通过编辑器工具将 `SimpleCityPackage` 场景内容导入主场景，并完成环境与总览镜头修复；同时支持从主场景复制生成一个专用于自定义障碍物演示的 sandbox 场景。

### 主要使用流程

1. 执行 `Tools/KY UAV/Import Simple City Into Main Scene`
2. 执行 `Tools/KY UAV/Apply Simple City Day Environment`
3. 执行 `Tools/KY UAV/Frame Overview Camera To City`
4. 如需自定义障碍物实验场景，执行 `Tools/KY UAV/Create Or Refresh Custom Obstacle Sandbox Scene`

### 输入

- `Assets/SimpleCityPackage/Scene/Scene 01.unity`
- 主场景当前内容
- `Assets/Scenes/Main/MainScene.unity`

### 处理

- 导入城市根节点
- 提取建筑对象为 `Buildings`
- 回写 `DroneManager.obstacleRoot`
- 设置天空盒、雾效、平行光和总览相机构图
- 复制 `MainScene` 生成 `CustomObstacleSandbox.unity`
- 清空 sandbox 场景中的默认建筑和任务点，保留地面、起飞点和运行时系统链路
- 回写 sandbox 场景的 `DroneManager.obstacleRoot` 与默认规划边界

### 输出

- 可用于仿真的城市障碍环境
- 可用于拖拽创建自定义建筑的 sandbox 场景

### 当前状态

`已实现`

### 依赖模块

- `SimpleCitySceneImporter`
- `SimpleCityEnvironmentTools`
- `KyUavSandboxSceneTools`
- `DroneManager`

## 3. 无人机配置与机群生成

### 功能名称

无人机默认配置、生成与重建

### 功能目标

根据默认配置和当前运行参数生成机群，并支持重建与重置。

### 主要使用流程

1. `DroneManager` 从 `DroneConfig` 读取默认参数
2. 按 `droneCount` 和起飞点集合生成无人机
3. 为每架无人机关联 `DroneStateMachine`、`DroneData`、`DronePathVisualizer`
4. 运行时可通过面板修改数量并重建机群

### 输入

- `DroneConfig`
- `dronePrefab`
- 起飞点集合或阵列生成参数

### 处理

- 统一应用速度、巡航高度、安全间距和任务容量
- 根据场景起飞点或规则阵列计算生成位置
- 初始化无人机编号、名称和数据对象

### 输出

- `drones`
- `droneDataList`

### 当前状态

`已实现`

### 依赖模块

- `DroneManager`
- `DroneController`
- `DroneStateMachine`
- `DroneConfig`

## 4. 任务点配置与导入

### 功能名称

任务点新增、清空、默认导入

### 功能目标

支持在运行时配置任务点，并在场景没有任务点时自动加载默认任务集。

### 主要使用流程

1. 通过场景 Canvas 按钮进入任务点交互
2. 点击地面新增任务点，或从 `Resources/taskpoints.csv` 导入
3. 点击清空按钮删除当前任务点
4. 启动仿真时若无任务点，`SimulationManager` 自动调用 `TaskPointImporter`

### 输入

- 鼠标点击地面位置
- `Assets/Resources/taskpoints.csv`
- `TaskPointData`

### 处理

- `TaskPointUIManager` 负责交互模式切换
- `TaskPointSpawner` 负责安全生成与避开建筑
- `TaskPointImporter` 解析 CSV/JSON 并批量创建任务点

### 输出

- 场景中的 `TaskPoint` 对象集合

### 当前状态

`已实现`

### 依赖模块

- `TaskPointUIManager`
- `TaskPointSpawner`
- `TaskPointImporter`
- `TaskPoint`

## 5. 起飞点配置

### 功能名称

起飞点新增、移动、删除、清空

### 功能目标

支持在运行时调整无人机起飞点布局，以影响机群生成位置。

### 主要使用流程

1. 在运行时控制面板中切换起飞点交互模式
2. 点击地面新增起飞点，或选中已有起飞点移动/删除
3. 执行清空后删除全部手动起飞点
4. 若允许自动重建，则刷新机群生成位置

### 输入

- 鼠标点击地面位置
- 起飞点间距限制

### 处理

- `DroneSpawnPointUIManager` 管理三种模式：新增、移动、删除
- 通过 `DroneSpawnPointMarker` 显示顺序与标签
- 重建机群时优先使用场景起飞点

### 输出

- 场景中的起飞点集合
- 更新后的机群生成结果

### 当前状态

`已实现`

### 依赖模块

- `DroneSpawnPointUIManager`
- `DroneSpawnPointMarker`
- `DroneManager`

## 6. 自定义障碍物编辑

### 功能名称

运行时自定义建筑绘制、删除与清空

### 功能目标

支持用户在空白地面区域拖拽创建长方体或城市楼体模板建筑，并将其作为新的静态障碍物接入路径规划、`2D俯视` 建筑 footprint 检查和实验展示流程。

### 主要使用流程

1. 在右侧运行时控制面板的“障碍物”区域点击 `绘制`
2. 通过 `样式` 切换长方体或城市楼体模板
3. 通过 `缩放` 和 `高度` 调整下一次创建建筑的尺寸
4. 在空白地面按住左键拖拽，生成一个建筑
5. 如需删除，点击 `删除` 后选中已有自定义建筑
6. 如需清空，点击 `清空`

### 输入

- 鼠标点击/拖拽地面位置
- 默认障碍物缩放倍率
- 默认障碍物高度
- 当前场景中的建筑、任务点和起飞点

### 处理

- `RuntimeObstacleEditor` 负责绘制、删除和清空三种模式切换
- `RuntimeObstacleCatalog` 提供现成城市楼体模板
- 缩放倍率统一作用于预览包围盒、重叠校验和最终障碍物生成
- 仅允许在 `SimulationState.Idle` 下编辑障碍物
- 创建前检查拖拽区域是否与已有建筑、自定义障碍物、任务点或起飞点重叠
- 新建障碍物统一挂到 `Buildings/RuntimeObstacles`
- 创建或删除后调用 `DroneManager.RefreshObstacleConfiguration`，同步规划障碍层和建筑 footprint 缓存
- 飞行阶段额外执行前向建筑阻挡检测，必要时触发避障重规划，避免直线路径直接穿楼

### 输出

- 运行时会话中的 `RuntimeObstacleMarker` 障碍物集合
- 更新后的障碍层、代理碰撞体和建筑 footprint 缓存

### 当前状态

`已实现`

### 依赖模块

- `RuntimeObstacleEditor`
- `RuntimeObstacleCatalog`
- `RuntimeObstacleMarker`
- `SimulationRuntimeControlPanel`
- `DroneManager`

## 7. 调度算法选择与调用

### 功能名称

调度算法切换与任务分配

### 功能目标

在同一套任务点上切换不同调度策略，生成无人机任务队列。

### 主要使用流程

1. 运行时面板修改当前调度算法
2. 点击开始仿真
3. `SimulationManager` 收集任务点
4. `DroneManager.AutoAssignTasks` 生成 `SchedulingRequest`
5. 由对应调度器返回 `SchedulingResult`
6. 将分配结果写回各无人机的任务队列

### 输入

- 在线无人机列表
- 任务点列表
- 容量限制和权重参数

### 处理

- `EvenSplitScheduler` 按顺序均分任务
- `GreedyNearestScheduler` 按距离和优先级评分贪心分配
- `PriorityGreedyScheduler` 按优先级、距离、负载综合评分分配

### 输出

- 每架无人机的任务队列
- `SchedulingResult`

### 当前状态

`已实现`

### 依赖模块

- `DroneManager`
- `ISchedulerAlgorithm`
- `SchedulingRequest`
- `SchedulingResult`

## 8. 路径规划算法选择与调用

### 功能名称

路径规划算法切换与路径生成

### 功能目标

为当前任务目标生成可执行路径，并将规划结果交给状态机逐点执行。

### 主要使用流程

1. 运行时面板修改规划算法和规划边界
2. 无人机进入 `Moving` 状态
3. `DroneStateMachine` 为当前任务请求路径
4. `DroneManager` 按 `pathPlannerType` 选择规划器
5. 规划结果写入 `droneData.plannedPath`

### 输入

- 起点、终点
- 规划边界
- 网格尺寸
- 建筑障碍层
- 是否允许对角

### 处理

- `StraightLinePlanner` 返回起点到终点的直线路径
- `AStarPlanner` 在 `XZ` 平面网格上做静态障碍 A* 搜索
- `RRTPlanner` 在 `XZ` 平面做确定性种子的随机扩展树搜索
- 支持过程可视化的规划器还会通过 `IPathPlannerWithVisualization` 输出步骤事件，供后续统一回放

### 输出

- `PathPlanningResult`
- 路径点列表

### 当前状态

`已实现`

### 依赖模块

- `IPathPlanner`
- `PathPlanningRequest`
- `PathPlanningResult`
- `DroneManager`
- `DroneStateMachine`

## 9. 路径规划算法过程可视化

### 功能名称

路径规划搜索过程演示

### 功能目标

让用户不只看到最终路径，还能观察不同规划算法在搜索阶段的节点扩展顺序、候选路径变化、回溯过程和最终路径生成方式。

### 主要使用流程

1. 启动仿真，触发无人机路径规划
2. `DroneManager` 在规划时为当前无人机创建过程记录器
3. 支持的规划器把步骤事件写入 `PathPlanningVisualizationRecorder`
4. 运行时控制面板“算法过程演示”区域选择当前无人机
5. 切换 `仅最终结果 / 完整过程 / 关键步骤`
6. 使用 `播放/继续 / 暂停 / 单步 / 重置` 控制演示
7. 观察状态卡、步骤说明卡和图例卡

### 输入

- 规划请求和规划结果
- 规划器输出的步骤事件
- 当前选中的无人机
- 当前演示模式和播放速度

### 处理

- `AlgorithmVisualizerManager` 收集并注册每次规划的 `PathPlanningVisualizationTrace`
- `PathPlanningVisualizationRecorder` 统一保存步骤帧、候选路径、回溯路径和最终路径
- `PathPlanningVisualizationBuilder` 构建初始化帧、结束帧和 fallback 轨迹
- `SimulationRuntimeControlPanel` 提供播放控制、无人机切换、模式切换、速度切换和图例说明
- `PathPlanningProcessRenderer` 负责在场景中渲染已访问节点、当前扩展节点、候选边、回溯路径和最终路径

### 输出

- 场景中的过程节点和过程边
- 当前步骤描述、步骤编号和播放状态
- `仅最终结果 / 完整过程 / 关键步骤` 三种演示模式

### 当前状态

`已实现`

### 依赖模块

- `IPathPlannerWithVisualization`
- `PathPlanningVisualizationRecorder`
- `PathPlanningVisualizationBuilder`
- `AlgorithmVisualizerManager`
- `PathPlanningProcessRenderer`
- `SimulationRuntimeControlPanel`
- `DroneManager`

## 10. 多无人机状态管理与仿真控制

### 功能名称

仿真状态控制与单机状态推进

### 功能目标

支持仿真开始、暂停、继续、重置，并通过状态机推进无人机执行任务。

### 主要使用流程

1. 点击开始或按 `F5`
2. `SimulationManager` 进入 `Running`
3. `DroneStateMachine` 在 `Idle / Moving / Waiting / Finished` 间切换
4. 点击暂停或按 `F6`
5. 点击重置或按 `F7`
6. 点击重建或按 `F8`

### 输入

- 当前仿真状态
- 任务队列
- 路径点列表
- 近距离冲突信息

### 处理

- `SimulationManager` 管理系统级状态 `Idle / Running / Paused`
- `DroneStateMachine` 管理单机级状态推进
- `DroneController` 执行底层移动
- `DroneManager` 在 `LateUpdate` 中做机体分离

### 输出

- 无人机位置与状态变化
- 统计信息更新

### 当前状态

`已实现`

### 依赖模块

- `SimulationManager`
- `DroneStateMachine`
- `DroneController`
- `DroneManager`

## 11. 飞行路径可视化与 2D 轨迹检查

### 功能名称

规划路径、飞行轨迹和建筑告警显示

### 功能目标

让用户同时观察规划路径、实际飞行轨迹，并在 `2D俯视` 下判断是否穿越建筑投影。

### 主要使用流程

1. 运行仿真
2. 在运行时面板切换规划线和航迹显示
3. 切换相机到 `2D俯视` 或按 `3`
4. 观察投影后的路径线、航迹线和建筑告警计数

### 输入

- 规划路径
- 飞行实时位置
- 建筑 footprint 缓存

### 处理

- `DronePathVisualizer` 绘制规划线和航迹线
- `CameraManager` 控制路径是否投影到统一平面
- `DroneManager` 提供建筑 footprint 命中和线段相交检测

### 输出

- 3D 路径线
- 2D 投影轨迹
- 建筑告警样式和告警计数

### 当前状态

`已实现`

### 依赖模块

- `CameraManager`
- `DronePathVisualizer`
- `DroneManager`

## 12. 相机观察与状态展示

### 功能名称

总览、跟随、轮询和统计展示

### 功能目标

从不同角度观察仿真执行过程，并显示任务和无人机状态摘要。

### 主要使用流程

1. 通过面板或快捷键在 `总览 / 跟随 / 2D俯视` 间切换
2. 跟随模式下切换下一架或上一架无人机
3. 查看右侧摘要与统计卡片

### 输入

- 相机模式
- 当前跟随目标
- 任务点与无人机统计数据

### 处理

- `CameraManager` 管理相机状态
- `SimulationRuntimeControlPanel` 汇总任务数、机群状态、等待次数、冲突次数、建筑告警数等

### 输出

- 观察视角
- 运行摘要
- 统计文本

### 当前状态

`已实现`

### 依赖模块

- `CameraManager`
- `SimulationRuntimeControlPanel`
- `DroneManager`
- `SimulationManager`

## 13. 结果导出与数据记录

### 功能名称

CSV、JSON 和会话归档导出

### 功能目标

保存单次仿真结果和详细数据，用于论文结果表、实验记录和后续分析。

### 主要使用流程

1. 仿真完成后自动导出 CSV，或手动点击导出
2. 可手动导出 JSON 明细
3. 可切换导出根目录或新建会话目录

### 输入

- 当前仿真状态
- 无人机统计
- 任务统计
- 规划参数快照
- 当前相机状态

### 处理

- `SimulationResultExporter` 构建 `SimulationExperimentRecord`
- `SimulationResultExporter` 构建 `SimulationExperimentDetailExport`
- 写入 `Application.persistentDataPath/ExperimentResults` 或自定义目录

### 输出

- `simulation_experiment_records.csv`
- `simulation_experiment_detail_*.json`

### 当前状态

`已实现`

### 依赖模块

- `SimulationResultExporter`
- `SimulationExperimentRecord`
- `SimulationExperimentDetailExport`

## 14. 批量实验与实验预设

### 功能名称

运行时实验中心与批量实验

### 功能目标

以可复现的方式批量运行调度、规划、机群规模和障碍密度实验。

### 主要使用流程

1. 在运行时面板浏览实验分组
2. 选择预设并应用到当前配置，或直接启动预设批量实验
3. 由 `BatchExperimentRunner` 自动循环运行并导出每轮结果

### 输入

- `ExperimentPreset`
- 当前运行时配置
- 批量轮数

### 处理

- `ExperimentPresetCatalog` 构建分组目录
- `BatchExperimentRunner` 在每轮间执行重置、启动、等待完成和导出
- `SimulationResultExporter` 写入 session 级摘要文件

### 输出

- 多轮 CSV/JSON
- `session_manifest.json`
- `session_summary.csv`

### 当前状态

`已实现`

### 依赖模块

- `ExperimentPreset`
- `ExperimentPresetCatalog`
- `BatchExperimentRunner`
- `SimulationResultExporter`

## 15. 测试、烟雾验证与打包

### 功能名称

自动化验证与 Windows 打包

### 功能目标

在交付前自动检查主场景关键对象、执行低风险测试，并输出 Windows 可执行程序。

### 主要使用流程

1. 执行 `Tools/KY UAV/Bootstrap Delivery Assets`
2. 执行 `Tools/KY UAV/Run Project Smoke Validation`
3. 执行 `Tools/KY UAV/Build Windows Player`
4. 如需批处理测试，执行 `KyUavEditModeBatchRunner.RunEditModeTests`

### 输入

- 主场景
- 默认配置资产
- 测试程序集

### 处理

- 补齐 `DroneConfig` 与实验预设
- 校验关键对象和关键引用
- 运行 EditMode 测试
- 输出 Windows 打包结果

### 输出

- 构建结果
- 烟雾验证日志
- EditMode XML 测试结果

### 当前状态

`已实现`

### 依赖模块

- `KyUavDeliveryAssetTools`
- `ProjectSmokeValidator`
- `KyUavBuildTools`
- `KyUavEditModeBatchRunner`

## 16. 当前没有实现的功能

以下功能在当前仓库中没有闭环实现，文档中不应写成“已支持”：

- 批量实验结果总览回放与双算法并排对比界面
- 系统内图表化对比面板
- 多场景运行时加载与切换
- 自定义障碍物布局持久化（保存/加载）
- 正式的时空协同避让算法
- 联网通信与实机控制
