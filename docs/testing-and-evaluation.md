# 测试与实验

## 1. 测试目标

当前测试不是覆盖所有 Unity 交互细节，而是围绕“毕设演示能否稳定跑通”建立最小可交付验证链。

## 2. 自动化验证

### 2.1 烟雾验证

入口：

- `ProjectSmokeValidator.RunSmokeValidation`

用途：

- 打开主场景
- 检查关键对象和关键引用是否存在
- 回归确认新增障碍物编辑器和路径规划过程可视化接入后未破坏主流程依赖

### 2.2 EditMode 测试

测试程序集：

- `KyUAV.EditMode.Tests`

当前用例：

- `SchedulingAlgorithmTests`
- `PlanningAndExportTests`
- `AlgorithmNameMappingTests`
- `ExperimentPresetCatalogTests`
- `SandboxSceneToolsTests`
- `PathPlanningVisualizationTests`

覆盖重点：

- 调度容量约束
- 规划基础约束
- 路径规划过程轨迹记录与播放状态更新
- CSV 格式
- 算法命名稳定性
- 实验预设目录稳定性
- sandbox 场景生成后的对象完整性

### 2.3 自定义障碍物实验场景生成验证

入口：

- `KyUavSandboxSceneTools.CreateOrRefreshCustomObstacleSandboxSceneBatch`

用途：

- 从 `MainScene` 复制生成 `CustomObstacleSandbox.unity`
- 清空默认建筑和任务点
- 保留地面、起飞点和运行时主链路

## 3. 人工测试与交付记录

当前仓库保留了两份证据文档：

- [功能测试记录](function-test-checklist.md)
- [稳定性测试记录](stability-test-record.md)

它们记录了当前打包版已自动验证通过、部分通过和待人工复核的项目。

## 4. 当前实验矩阵

实验预设由 `KyUavDeliveryAssetTools` 自动生成，固定为四组：

- 调度对比：`EvenSplit / GreedyNearest / PriorityGreedy` + `AStar` + `4` 架无人机
- 规划对比：`StraightLine / AStar / RRT` + `PriorityGreedy` + `4` 架无人机
- 机群扩展：`2 / 4 / 6 / 8` 架无人机 + `PriorityGreedy + AStar`
- 障碍密度：`Sparse / Medium / Dense` + `PriorityGreedy + AStar` + `4` 架无人机

## 5. 当前可用结果类型

- 单次 CSV 摘要
- 单次 JSON 明细
- 批量实验 `session_manifest.json`
- 批量实验 `session_summary.csv`

## 6. 当前验证结论

依据现有记录，可确认：

- 主流程启动、暂停、继续、重置、重建已跑通
- `2D俯视` 和建筑告警检查入口已接入
- `kinematic rigidbody` 速度写入警告已清除
- 默认任务集下未触发建筑穿越告警
- 自定义障碍物实验场景可通过 batchmode 成功生成
- 障碍物编辑功能接入后主场景烟雾验证仍为 `PASS`
- 截至 `2026-04-21`，历史 EditMode 自动化测试为 `9` 项，`9` 项通过
- `2026-04-22` 新增 `PathPlanningVisualizationTests` 共 `3` 项，用于覆盖 A* / RRT 过程记录和播放状态更新；当前已完成程序集构建验证，待用 Unity Test Runner 补正式执行记录

## 7. 当前验证空白

以下内容仍需人工补测或专项测试：

- 通过面板点击验证 JSON 导出
- 通过面板点击验证批量实验并核对 `session_manifest.json`、`session_summary.csv`
- 构造显式穿越建筑 footprint 的任务集，验证建筑告警触发
- 更复杂密集场景下的局部避让稳定性
- 在 `CustomObstacleSandbox` 中人工验证障碍物绘制、删除、清空、样式切换、缩放和高度调整
- 人工验证鼠标悬停在运行时面板、Scroll View、输入框和下拉控件上时，场景滚轮不会再穿透到相机缩放
- 人工验证“算法过程演示”区在不同分辨率下的布局、无人机切换、速度切换和图例显示
- 人工验证 `F9 / F11 / F12` 与面板按钮在播放状态同步上保持一致
- 自定义障碍物布局当前未持久化，保存/加载能力仍未实现
