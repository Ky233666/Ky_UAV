# Runtime Control Panel Guide

## 用途

`SimulationRuntimeControlPanel` 是面向运行时和打包版的控制面板，用来把一部分原本依赖 Inspector 的关键参数迁移到 UI。

脚本位置：

- `Assets/Scripts/UAV/Controller/SimulationRuntimeControlPanel.cs`

挂载方式：

- 由 `SimulationManager` 在运行时自动创建
- 默认挂载到当前主 Canvas 右侧区域

## 当前支持的能力

### 算法切换

- 调度算法：`EvenSplit`、`GreedyNearest`
- 路径规划算法：`StraightLine`、`AStar`

说明：

- 切换后会立即写回 `DroneManager`
- 若要让机群按新数量或新组合完整重建，使用 `重建`

### 机群参数

- 无人机数量
- 飞行速度
- 仿真倍速

说明：

- `同步`：把当前面板配置应用到现有机群
- `重建`：按当前数量重新生成机群，并同步当前算法与显示配置

### 显示控制

- 规划路径开关
- 飞行轨迹开关

### 镜头控制

- 总览
- 跟随
- 下一架

## 关联脚本

- `SimulationManager.cs`
  - 负责在启动时确保控制面板存在

- `DroneManager.cs`
  - 提供 `RespawnDrones`
  - 提供 `ApplyDroneSpeedToAll`
  - 提供 `ApplyPathVisibilityToAll`

- `DronePathVisualizer.cs`
  - 负责规划路径和飞行轨迹显示

- `CameraManager.cs`
  - 负责总览/跟随和目标切换

## 打包版建议

- 保留右侧运行时面板，避免打包后仍需回到 Inspector 调参
- 演示前优先确认字体资源与 TMP 字体资产已导入
- 若继续扩展面板，优先增加统计信息和实验导出入口，而不是继续堆叠单项按钮

## 后续建议

下一步更值得补充的内容：

- 实时统计面板
- 规划网格和障碍参数面板
- 实验结果导出按钮
- 任务集保存与切换

