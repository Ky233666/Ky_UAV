# Ky_UAV

多无人机协同路径规划与调度算法的仿真系统设计与实现。

本项目基于 Unity 2022.3.62f3，目标不是单独复现某个算法，而是构建一个可运行、可配置、可演示、可导出的多无人机仿真系统。系统围绕城市障碍环境下的任务分配、路径规划、状态推进、可视化观察和实验结果留档展开，适合毕业设计演示、系统设计说明和论文撰写引用。

## 项目定位

- 面向多无人机场景的仿真系统，而不是纯算法脚本集合。
- 支持调度算法和路径规划算法以统一接口接入系统。
- 支持任务点配置、起飞点配置、参数设置、仿真控制、结果导出和批量实验。
- 强调软件工程结构、运行时交互和可视化表达。

## 当前已实现能力

- 默认演示场景为 `Assets/Scenes/Main/MainScene.unity`；可通过编辑器工具生成 `Assets/Scenes/Sandbox/CustomObstacleSandbox.unity` 作为自定义障碍物实验场景。
- 城市建筑障碍环境已接入主场景，`DroneManager` 可自动配置建筑层和障碍代理碰撞体。
- 支持 `EvenSplit`、`GreedyNearest`、`PriorityGreedy` 三种调度算法。
- 支持 `StraightLine`、`AStar`、`RRT` 三种路径规划算法。
- 支持无人机生成、重建、任务分配、状态推进、重置和简单局部避让。
- 支持任务点运行时新增、清空、导入。
- 支持起飞点运行时新增、移动、删除、清空。
- 支持在运行时通过右侧面板拖拽创建、删除、清空自定义障碍物，可在长方体与城市楼体模板间切换，并调节缩放倍率与高度，自动刷新规划障碍层和建筑 footprint。
- 支持总览、跟随、`2D俯视` 三种观察模式，以及路径与轨迹可视化。
- 支持实时统计、CSV/JSON 导出、批量实验、`session_manifest.json`、`session_summary.csv`。
- 支持运行时实验中心，按 `Scheduling / Planning / Scaling / Density` 预设分组切换实验。
- 支持编辑器烟雾验证、Windows 一键打包和 EditMode 自动化测试。

## 当前未完成或仅为演示级实现

- 多机协同避让仍是基于局部规则的第一版实现，不是正式的时空协同避碰算法。
- 结果对比依赖导出文件和批量实验，不提供独立的结果回放模块。
- 不支持运行时多场景加载；场景编辑能力当前仅限编辑器生成 sandbox 场景和运行时会话级自定义障碍物，不是完整地图编辑器流程。
- 自定义障碍物当前不会自动保存回场景资源或独立布局文件，退出 Play Mode 或程序重启后需要重新创建。
- 没有联网、强化学习、复杂动力学、传感器建模等扩展模块。

## 代码结构

- `Assets/Scripts/UAV/Controller`
  - 仿真控制、无人机管理、状态机、相机、运行时面板、导出、任务点与起飞点交互。
- `Assets/Scripts/UAV/Comm`
  - 调度算法、路径规划算法、算法接口、算法名称映射、实验预设目录构建。
- `Assets/Scripts/UAV/Config`
  - `DroneConfig` 默认参数配置。
- `Assets/Scripts/UAV/Model`
  - 调度请求/结果、路径请求/结果、实验导出模型、实验预设模型。
- `Assets/Editor`
  - 交付资产初始化、烟雾验证、批量测试、Windows 打包、城市场景导入、环境修复与 sandbox 场景生成工具。
- `Assets/Tests/EditMode/Editor`
  - 调度、规划、导出、算法名称映射、实验预设目录逻辑测试。

## 快速运行

1. 使用 Unity Hub 以 `2022.3.62f3` 打开项目。
2. 首次打开后执行 `Tools/KY UAV/Bootstrap Delivery Assets`，生成 `DroneConfig` 和实验预设资产。
3. 打开 `Assets/Scenes/Main/MainScene.unity`，或执行 `Tools/KY UAV/Create Or Refresh Custom Obstacle Sandbox Scene` 后使用 `Assets/Scenes/Sandbox/CustomObstacleSandbox.unity`。
4. 进入 Play Mode。
5. 使用主场景原有 Canvas 按钮进行任务点新增、导入、清空。
6. 使用右侧运行时控制面板进行仿真控制、算法切换、规划参数设置、起飞点管理、自定义障碍物编辑、导出和批量实验。

## 构建与验证

- 烟雾验证：`Tools/KY UAV/Run Project Smoke Validation`
- Windows 打包：`Tools/KY UAV/Build Windows Player`
- EditMode 批处理入口：`KyUavEditModeBatchRunner.RunEditModeTests`
- 默认打包输出目录：`D:\unityhub\project\build\Ky_UAV`

## 文档入口

项目文档现已统一到根目录 `docs`：

- [文档索引](docs/README.md)
- [项目概览](docs/project-overview.md)
- [功能说明](docs/feature-specification.md)
- [系统架构](docs/system-architecture.md)
- [模块设计](docs/module-design.md)
- [用户指南](docs/user-guide.md)
- [部署与构建](docs/deployment-guide.md)
- [测试与实验](docs/testing-and-evaluation.md)
- [执行计划](docs/execution-plan.md)
- [功能测试记录](docs/function-test-checklist.md)
- [稳定性测试记录](docs/stability-test-record.md)

## 说明

- 若文档内容与代码不一致，以 `Assets/Scripts/UAV`、`Assets/Editor` 和 `docs` 下的最新文档为准。
- 当前版本最适合展示“系统设计与实现”，而不是宣称已经完成复杂协同优化算法研究。
