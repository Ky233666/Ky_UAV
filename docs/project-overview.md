# 项目概览

## 1. 项目背景

多无人机系统在城市巡检、应急搜索和任务协同中需要同时解决两个问题：

- 如何把多个任务点分配给多架无人机。
- 如何在带有障碍物的场景中为每架无人机生成可执行路径。

本科毕业设计如果只实现单个算法，很难完整体现工程能力。本项目因此采用“仿真系统 + 算法接入”的方式展开：以 Unity 为运行平台，以调度和路径规划为核心算法能力，以场景、交互、控制、可视化、导出和测试作为系统层支撑。

## 2. 项目定位

本项目的准确定位是：

- 一个面向多无人机场景的仿真系统。
- 一个支持调度算法与路径规划算法接入的实验平台。
- 一个强调运行闭环、可视化表达和工程化交付的本科软件工程类项目。

它不是：

- 只输出算法对比曲线的纯算法代码仓库。
- 带真实飞控、通信链路和硬件接入的实机系统。
- 完整的三维动力学仿真平台。

## 3. 系统目标

系统当前围绕以下目标建设：

- 在默认主场景或自定义障碍物实验场景中完成多无人机任务执行演示。
- 支持在运行时切换调度算法和路径规划算法。
- 支持任务点、起飞点和自定义障碍物配置。
- 支持观察路径规划结果、路径规划过程和真实飞行轨迹。
- 支持保存单次结果和批量实验结果。
- 支持后续将系统设计、模块设计和功能实现写入毕业论文。

## 4. 当前代码范围

### 4.1 场景与资源

- 主场景：`Assets/Scenes/Main/MainScene.unity`
- 自定义障碍物实验场景：`Assets/Scenes/Sandbox/CustomObstacleSandbox.unity`
- 默认任务数据：`Assets/Resources/taskpoints.csv`
- 默认无人机配置：`Assets/Resources/Configs/DroneConfig_Default.asset`
- 默认实验预设：`Assets/Resources/ExperimentPresets/*`

系统当前没有运行时多场景切换入口。实验时可在编辑器中直接打开 `MainScene`，或通过 `Tools/KY UAV/Create Or Refresh Custom Obstacle Sandbox Scene` 生成并打开 `CustomObstacleSandbox.unity`。

### 4.2 算法能力

调度算法：

- `EvenSplit`
- `GreedyNearest`
- `PriorityGreedy`

路径规划算法：

- `StraightLine`
- `AStar`
- `RRT`

三种规划器都通过 `IPathPlanner` 接口统一接入，三种调度器都通过 `ISchedulerAlgorithm` 接口统一接入。
其中 `StraightLine`、`AStar`、`RRT` 还额外实现了 `IPathPlannerWithVisualization`，可向统一过程可视化框架输出步骤事件。

### 4.3 运行时系统能力

- 无人机生成、重建、状态机推进与重置
- 任务点新增、清空、导入
- 起飞点新增、移动、删除、清空
- 自定义障碍物绘制、删除、清空、样式切换、缩放与高度调整
- 仿真开始、暂停、继续、重置
- 算法切换、规划边界与网格参数调整
- 算法过程演示面板：支持无人机切换、演示模式切换、倍速、播放、暂停、单步和重置
- 总览、跟随、`2D俯视` 相机观察
- UI 面板滚轮输入拦截，避免面板滚动时误触发场景相机缩放
- 路径和轨迹可视化
- 统计显示、导出、批量实验和实验预设调用

## 5. 已完成与未完成边界

### 5.1 已完成

- 主场景与城市障碍环境接入
- 自定义障碍物实验场景生成工具
- 障碍层自动配置与建筑代理碰撞体生成
- 统一 UI 输入拦截与滚轮防穿透
- 多无人机统一管理与状态推进
- 三种调度器与三种规划器接入
- 路径规划算法过程可视化框架与统一回放控制
- 运行时实验中心、导出与批量实验
- `2D俯视` 路径检查与建筑告警
- 运行时自定义障碍物编辑与障碍缓存刷新
- Windows 打包、烟雾验证、EditMode 自动化测试

### 5.2 未完成或仅为演示级实现

- 正式的多机协同避障算法
- 双算法并排对比和独立结果回放系统
- 完整的运行时场景编辑器或地图编辑器
- 自定义障碍物布局持久化（保存/加载场景或布局文件）
- 自动图表生成和系统内结果对比视图
- 复杂动力学、传感器或通信建模

## 6. 当前文档体系

项目文档统一位于根目录 `docs`：

- `README.md`：文档索引与阅读顺序
- `project-overview.md`：项目定位、范围和边界
- `feature-specification.md`：当前功能明细
- `system-architecture.md`：系统分层、数据流、算法接入方式
- `module-design.md`：模块职责、输入输出与依赖
- `user-guide.md`：使用流程与运行时操作
- `deployment-guide.md`：构建、验证与打包
- `testing-and-evaluation.md`：测试、实验和已知问题
- `execution-plan.md`：项目收口计划与固定实验矩阵
- `function-test-checklist.md`：当前打包版功能验收记录
- `stability-test-record.md`：当前稳定性验证记录

## 7. 与旧文档的冲突说明

本次重构发现以下旧文档表述与代码不一致：

- 旧 README 将“任务导入、任务新增”写成运行时控制面板能力，但代码显示这部分由 `TaskPointUIManager` 绑定场景 Canvas 按钮实现，不属于右侧运行时控制面板。
- 旧 `ProjectStatusAndRoadmap.md` 中仍有“批量实验模板、session 汇总尚未补齐”的历史表述，但代码中 `ExperimentPreset`、`session_manifest.json`、`session_summary.csv` 已存在。
- 旧路线图同时混写“现状、计划、展望”，不利于论文引用，因此本次拆分为概览、功能、架构、模块、部署和测试文档。
