# 实验执行说明

## 1. 初始化
1. 打开项目后先执行 `Tools/KY UAV/Bootstrap Delivery Assets`。
2. 打开 `Assets/Scenes/Main/MainScene.unity`。
3. 确认右侧运行时面板已显示“实验中心”和“镜头”区块。

## 2. 预设
- 预设资源目录：`Assets/Resources/ExperimentPresets`
- 默认密度预设：`ExperimentPresets/Density/Medium`
- 预设由 `BatchExperimentRunner` 自动加载，也可直接在运行时实验中心切换。

## 3. 执行顺序
1. 调度对比
2. 规划对比
3. 机群扩展
4. 障碍密度

## 4. 每组实验要求
- 固定同一套任务点。
- 每组至少运行 `5` 轮。
- 每轮记录 `CSV`、`JSON`。
- 每个 session 额外检查：
- `session_manifest.json`
- `session_summary.csv`

## 5. 运行时快捷键
- `1`：总览视角
- `2`：跟随视角
- `3`：`2D俯视`
- `F5`：开始/继续仿真
- `F6`：暂停仿真
- `F7`：重置仿真
- `F8`：重建机群
- `F10`：退出程序
- `Ctrl+Shift+N`：新建导出会话
- `Ctrl+Shift+C`：导出 CSV
- `Ctrl+Shift+J`：导出 JSON
- `Ctrl+Shift+B`：开始批量实验
- `Ctrl+Shift+X`：停止批量实验

## 6. 2D 轨迹检查
- 按 `3` 切到 `2D俯视`，直接观察 `XZ` 平面路径。
- 轨迹线与规划线会投影到建筑上方统一平面，避免被屋顶遮挡。
- 右侧摘要会显示 `建筑告警` 数量。
- 当无人机当前位置进入建筑 footprint，或轨迹穿越建筑 footprint 时，对应轨迹会进入告警样式。

## 7. 输出目录
- 默认根目录：`Application.persistentDataPath/ExperimentResults`
- 默认归档结构：`根目录 / 日期 / 会话`

## 8. 推荐留档
- 导出整个会话目录。
- 将 `session_summary.csv` 作为论文汇总依据。
- 将各轮 `JSON` 作为详细实验追溯依据。
- 对 `2D俯视` 下出现建筑告警的场景保留截图，作为路径安全性证据。
