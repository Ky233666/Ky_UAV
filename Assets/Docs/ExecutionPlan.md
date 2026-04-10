# Ky_UAV Execution Plan

> 目标：按“毕设答辩收口”推进，不再扩展高风险功能。  
> 维护规则：阶段完成后必须同步更新本文件与对应交付材料。

## Phase 0
- [x] 新建执行计划文档。
- [x] 将 `Assets/Scenes/Main/MainScene.unity` 写入构建场景配置。
- [x] 新增批处理烟雾验证入口：`ProjectSmokeValidator.RunSmokeValidation`。
- [x] 新增交付资产初始化入口：`KyUavDeliveryAssetTools.BootstrapDeliveryAssets`。

## Phase 1
- [x] 统一 `开始 / 暂停 / 重置 / 重建机群` 主链路的状态切换入口。
- [x] `SimulationManager` 在启动前对非 `Idle` 状态先做重置。
- [x] `RebuildFleet` 改为走完整重置链路，避免只切状态不清理运行时数据。
- [x] `DroneStateMachine.Reset` 统一清理路径、等待、冲突和目标缓存。
- [x] 当前局部避让保留演示级实现，但去掉等待状态里的占位 TODO 流程。

## Phase 2
- [x] 新增 `DroneConfig` 作为默认参数来源。
- [x] `DroneManager` 从配置资产加载默认速度、巡航高度、安全距离和任务容量。
- [x] `DroneStateMachine` 从配置资产加载等待与避让参数。
- [x] 调度请求增加 `maxTaskCapacity`，`0` 代表不限制。

## Phase 3
- [x] 新增 `ExperimentPreset` 资产类型。
- [x] 新增预设批量生成工具，自动生成调度/规划/机群扩展/障碍密度实验预设。
- [x] `BatchExperimentRunner` 支持按预设运行。
- [x] 每个 session 额外输出 `session_manifest.json` 与 `session_summary.csv`。
- [x] 统一算法标识与显示名称映射，避免 UI/导出各自维护一套。

## Phase 4
- [x] 新增 EditMode 测试覆盖低风险逻辑：
  - 调度容量约束
  - 路径规划基础约束
  - CSV 导出转义
  - 算法名称映射
- [x] 新增功能测试表。
- [x] 新增稳定性测试记录模板。
- [x] 新增实验执行说明。

## Phase 5
- [x] 新增答辩演示脚本。
- [x] 运行 Unity batchmode 初始化资产并执行烟雾验证。
- [x] 根据验证结果补最后一轮 README 与现状文档校准。

## 自动化验证记录
- 2026-04-10：`KyUavDeliveryAssetTools.BootstrapDeliveryAssets` batchmode 执行通过，默认 `DroneConfig` 与实验预设资产已生成到 `Assets/Resources/Configs`、`Assets/Resources/ExperimentPresets`。
- 2026-04-10：`ProjectSmokeValidator.RunSmokeValidation` batchmode 执行通过，`MainScene`、核心对象和关键引用检查全部通过。
- 2026-04-10：EditMode 测试执行通过，结果文件位于 `Library/Logs/editmode-results.xml`，共 `6` 项测试，`6` 项通过。
- 当前仍需人工执行的内容保留在功能测试表和稳定性记录模板中，不由 batchmode 自动替代。

## 固定实验矩阵
- 调度对比：`EvenSplit / GreedyNearest / PriorityGreedy` + `AStar` + `4` 架无人机。
- 规划对比：`StraightLine / AStar / RRT` + `PriorityGreedy` + `4` 架无人机。
- 机群扩展：`2 / 4 / 6 / 8` 架无人机 + `PriorityGreedy + AStar`。
- 障碍密度：`Sparse / Medium / Dense` + `PriorityGreedy + AStar` + `4` 架无人机。

## 验收出口
- 烟雾验证通过：主场景、核心对象、关键引用均存在。
- 主流程验证通过：开始、暂停、继续、重置、重建机群、导出、批量实验无阻塞性错误。
- 实验导出通过：CSV、JSON、session manifest、session summary 均能生成。
- 文档齐套：执行计划、功能测试表、稳定性记录、实验说明、答辩脚本均在仓库内可直接查阅。
