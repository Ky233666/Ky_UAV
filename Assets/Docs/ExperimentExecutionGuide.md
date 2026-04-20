# 实验执行说明

## 1. 初始化
1. 打开项目后先执行 `Tools/KY UAV/Bootstrap Delivery Assets`。
2. 打开 `Assets/Scenes/Main/MainScene.unity`。
3. 确认运行面板右侧已显示批量实验区块。

## 2. 预设
- 预设资源目录：`Assets/Resources/ExperimentPresets`
- 默认密度预设：`ExperimentPresets/Density/Medium`
- 预设由 `BatchExperimentRunner` 自动加载，也可直接在运行时实验中心切换，无需回 Inspector。

## 3. 执行顺序
1. 调度对比
2. 规划对比
3. 机群扩展
4. 障碍密度

## 4. 每组实验要求
- 固定同一套任务点。
- 每组至少运行 `5` 轮。
- 每轮记录 CSV、JSON。
- 每个 session 额外检查：
  - `session_manifest.json`
  - `session_summary.csv`

## 5. 输出目录
- 默认根目录：`Application.persistentDataPath/ExperimentResults`
- 默认归档结构：`根目录 / 日期 / 会话`

## 6. 推荐留档
- 导出整个会话目录。
- 将 `session_summary.csv` 作为论文汇总依据。
- 将各轮 JSON 作为详细实验追溯依据。
