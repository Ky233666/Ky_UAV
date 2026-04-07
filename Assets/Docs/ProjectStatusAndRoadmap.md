# 项目现状与后续开发路线

项目仓库地址  
<https://github.com/Ky233666/Ky_UAV>

> 最后重构时间：2026-04-07  
> 本文依据：`Assets/Scripts/UAV`、`Assets/Editor`、`Assets/Scenes/Main/MainScene.unity`、`Assets/Resources/taskpoints.csv` 的当前源码与资源状态  
> 目标：给后续开发、答辩准备、协作者接手提供一份“按真实源码写的现状说明”，而不是理想状态说明

---

## 1. 文档用途

这份文档回答四件事：

1. 当前项目到底已经实现了什么。
2. 当前系统的真实主链路是什么。
3. 哪些地方已经可用于演示，哪些地方仍然只是第一版实现。
4. 现在最值得继续开发的方向是什么。

如果后续要继续加功能，默认先读这份文档，再决定从哪条链路切入。

---

## 2. 当前项目的一句话定位

这是一个基于 Unity 的多无人机协同巡检与路径规划仿真系统。

当前版本已经不是“只有场景和少量 Inspector 参数的原型”，而是一个具备以下闭环的最小可用演示系统：

- 多无人机统一生成、调度、路径规划、执行与重置
- 运行时任务点和起飞点交互
- 调度算法与路径规划算法对比
- 路径与飞行轨迹可视化
- 总览/跟随相机观察
- 运行时统计
- CSV/JSON 结果导出
- 自定义导出目录与批量实验记录

当前系统已经可以支撑：

- 毕业设计过程演示
- 算法切换与对比
- 障碍环境下路径观察
- 多机任务执行展示
- 基础实验结果留档

---

## 3. 当前工程真实入口

### 3.1 运行环境

- Unity 版本：`2022.3.62f3`
- 开发语言：`C#`
- 主场景：`Assets/Scenes/Main/MainScene.unity`
- 主要源码目录：
  - `Assets/Scripts/UAV/Controller`
  - `Assets/Scripts/UAV/Comm`
  - `Assets/Scripts/UAV/Model`
- 编辑器工具目录：
  - `Assets/Editor`

### 3.2 当前主场景资源

当前主场景以 `SimpleCityPackage` 城市场景为基础，已经不是早期简化测试场景。

当前和主场景直接相关的关键资源包括：

- 主场景：`Assets/Scenes/Main/MainScene.unity`
- 无人机预制体：`Assets/Prefabs/UAV/Drone.prefab`
- 默认任务数据：`Assets/Resources/taskpoints.csv`
- 扩展地面材质：`Assets/Materials/ExtendedGround.mat`
- 扩展地面贴图：`Assets/Materials/GroundTile_Albedo.png`
- 扩展地面法线：`Assets/Materials/GroundTile_Normal.png`

当前已做过一轮场景展示修正：

- 无人机视觉尺寸已缩小，和城市场景比例更接近
- 主场景外围地面已扩展，不再只在城市局部有地面
- 扩展地面已经改为独立地砖贴图方案，不再直接错误复用整张 atlas

### 3.3 当前主场景中的主要运行对象

从主场景结构和源码职责看，当前运行主链路围绕这些对象展开：

- `SimulationManager`
- `DroneManager`
- `CameraManager`
- `TaskPointSpawner`
- `TaskPointImporter`
- `TaskManager`
- `Canvas` 下的主界面按钮
- 运行时自动创建的 `SimulationRuntimeControlPanel`
- 导入后的 `CityEnvironment`
- 用于规划障碍的 `Buildings`

---

## 4. 当前系统真实主链路

## 4.1 仿真总控

核心文件：`Assets/Scripts/UAV/Controller/SimulationManager.cs`

当前职责：

- 管理仿真状态：`Idle / Running / Paused`
- 绑定开始、暂停、重置按钮
- 启动仿真时收集任务点并调用 `DroneManager` 分配任务
- 场景中没有任务点时，自动尝试通过 `TaskPointImporter` 从 `Resources/taskpoints.csv` 导入默认任务
- 启动时自动确保以下运行时组件存在并连接完成：
  - `SimulationRuntimeControlPanel`
  - `SimulationResultExporter`
  - `BatchExperimentRunner`
  - `DroneSpawnPointUIManager`

当前判断：

- 这是当前项目的运行总入口
- 启动、重置、导出统计链路都已经围绕它串起来

## 4.2 多无人机控制层

核心文件：`Assets/Scripts/UAV/Controller/DroneManager.cs`

当前职责：

- 统一生成和管理多架无人机
- 统一保存 `DroneData`
- 统一创建状态机和路径可视化组件
- 统一调度与路径规划入口
- 统一速度、路径可见性、规划设置应用
- 统一重建机群与重置机群
- 自动发现和配置障碍物根节点
- 自动给建筑递归设层
- 自动生成建筑代理 `BoxCollider`
- 支持优先使用场景中的 `SpawnPoint` / `DroneSpawnPointMarker` 作为起飞位置
- 支持机体分离，降低起飞点过近时的重叠

当前已接入的调度算法：

- `EvenSplitScheduler`
- `GreedyNearestScheduler`

当前已接入的路径规划算法：

- `StraightLinePlanner`
- `AStarPlanner`

当前规划相关参数已经实际接入：

- 网格大小 `planningGridCellSize`
- 规划边界 `planningWorldMin / planningWorldMax`
- 对角搜索 `allowDiagonalPlanning`
- 障碍自动配置 `autoConfigurePlanningObstacles`
- 障碍层 `planningObstacleLayer`

当前判断：

- `DroneManager` 是项目最核心的控制层之一
- 当前版本已经具备运行时参数驱动能力，不再只是 Inspector 静态配置

## 4.3 单机执行与状态推进

核心文件：

- `Assets/Scripts/UAV/Controller/DroneController.cs`
- `Assets/Scripts/UAV/Controller/DroneStateMachine.cs`
- `Assets/Scripts/UAV/Controller/DroneData.cs`

当前职责：

- `DroneController`
  - 负责单机基础移动
  - 支持真实目标点和虚拟目标位置两种飞行目标
  - 支持重置到初始位置
- `DroneStateMachine`
  - 负责按任务队列推进任务
  - 负责请求路径规划并逐 waypoint 执行
  - 负责进入等待、恢复运行、完成任务
  - 负责当前第一版局部避让逻辑
- `DroneData`
  - 保存单机当前状态、路径、任务、统计信息和最近冲突信息

当前已经落地的状态/统计字段包括：

- 当前状态 `DroneState`
- 速度
- 最近位置
- 规划路径 `plannedPath`
- 当前 waypoint 索引
- 当前规划器名称
- 任务队列与当前任务索引
- 累计飞行距离
- 完成任务数
- 在线状态
- 等待原因
- 等待次数
- 冲突次数
- 最近一次冲突原因

当前局部避让真实状态：

- 已有第一版近距离避让与等待恢复逻辑
- 已能区分“等待次数”和“冲突次数”
- 已避免同一冲突在连续帧中重复累加
- 等待状态下会做一定退让处理

当前判断：

- 单机任务执行链已经完整
- 但多机协同避让仍是演示级实现，不应误写成“正式协同规避算法已完成”

## 4.4 路径规划与可视化

核心文件：

- `Assets/Scripts/UAV/Comm/AStarPlanner.cs`
- `Assets/Scripts/UAV/Comm/StraightLinePlanner.cs`
- `Assets/Scripts/UAV/Controller/DronePathVisualizer.cs`

当前真实能力：

- `StraightLinePlanner`
  - 直线 waypoint 规划
- `AStarPlanner`
  - 基于网格的二维平面 A*
  - 使用 `worldMin / worldMax / gridCellSize`
  - 支持是否允许对角扩展
  - 支持基于障碍层的碰撞检查
  - 障碍检测会把规划高度范围一起考虑进探测体积
- `DronePathVisualizer`
  - 绘制当前剩余规划路径
  - 绘制实际飞行轨迹
  - 支持分别开关路径和轨迹
  - 重置时会清空轨迹并重新初始化

当前判断：

- 路径规划对比能力已经成立
- 路径可视化已经能直接支撑演示和答辩讲解

## 4.5 任务点系统

核心文件：

- `Assets/Scripts/UAV/Controller/TaskPoint.cs`
- `Assets/Scripts/UAV/Controller/TaskPointSpawner.cs`
- `Assets/Scripts/UAV/Controller/TaskPointImporter.cs`
- `Assets/Scripts/UAV/Controller/TaskPointUIManager.cs`
- `Assets/Scripts/UAV/Controller/TaskPointData.cs`

当前真实能力：

- `TaskPoint`
  - 支持 `Pending / InProgress / Completed`
  - 保存任务 ID、名称、描述、优先级、预计耗时
  - 记录开始时间与完成耗时
  - 支持任务重置
- `TaskPointSpawner`
  - 运行时创建任务点
  - 放置时可检测障碍并自动重试偏移
  - 支持 `IsPlacementSafe` 与 `GetGroundedPosition`
- `TaskPointImporter`
  - 默认从 `Resources/taskpoints.csv` 导入
  - 支持从字符串导入
  - 支持 CSV 和 JSON 两种格式解析
- `TaskPointUIManager`
  - 支持点击场景放置任务点
  - 支持落点合法性判断
  - 支持放置预览圈
  - 支持 `Esc` 取消放置
  - 支持清空任务点
  - 支持导入任务点

当前判断：

- 任务点交互已经比较完整
- 当前版本的任务输入不再只依赖预设静态点

## 4.6 起飞点系统

核心文件：

- `Assets/Scripts/UAV/Controller/DroneSpawnPointUIManager.cs`
- `Assets/Scripts/UAV/Controller/DroneSpawnPointMarker.cs`

当前真实能力：

- 运行时点击场景新增起飞点
- 运行时删除单个起飞点
- 运行时移动已有起飞点
- 清空全部手动起飞点
- 起飞点编号显示
- 起飞点落点合法性检测
- 起飞点最小间距限制
- 起飞点预览圈
- `Esc` 取消当前交互模式
- 起飞点会自动带 `SpawnPoint` 标签，供 `DroneManager` 读取排序生成机群

当前判断：

- 起飞点交互已经形成闭环
- 这是当前项目和普通“固定起飞点演示”相比的重要提升

## 4.7 相机系统

核心文件：`Assets/Scripts/UAV/Controller/CameraManager.cs`

当前真实能力：

- 管理总览相机与跟随相机
- 在多机之间切换跟随目标
- 支持刷新当前受管无人机列表
- 支持运行时修改跟随偏移

当前快捷键：

- `1`：总览视角
- `2`：跟随视角
- `Tab` / `E`：下一架无人机
- `Q`：上一架无人机
- `W/A/S/D`：总览平移
- `R / F`：总览升降
- 鼠标滚轮：缩放
- 右键拖动：总览旋转
- `Shift`：总览加速

当前判断：

- 相机系统已满足观察、演示和调试需求

## 4.8 运行时界面与控制面板

核心文件：

- `Assets/Scripts/UAV/Controller/SimulationDashboardStyler.cs`
- `Assets/Scripts/UAV/Controller/SimulationRuntimeControlPanel.cs`

当前真实能力：

- `SimulationDashboardStyler`
  - 统一标题卡、状态卡、底部控制区和任务工具区风格
  - 让主界面视觉更适合演示
- `SimulationRuntimeControlPanel`
  - 运行时自动创建右侧面板
  - 支持展开/收起
  - 支持滚动
  - 支持摘要、状态提示和多区块控制

当前面板已经接入的区块和功能：

- `统计`
  - 仿真状态、耗时、镜头模式、当前目标
  - 任务进度
  - 无人机状态统计
  - 总飞行距离与平均飞行距离
  - 总等待次数与总冲突次数
  - 逐机摘要
- `结果`
  - 当前导出根目录显示
  - 当前归档目录显示
  - 目录选择器
  - 手动输入导出路径
  - 切回默认目录
  - 新建导出会话
  - 导出 CSV
  - 导出 JSON
  - 批量实验轮数设置
  - 批量实验开始/停止
- `起飞点`
  - 新增
  - 移动
  - 删除
  - 清空
- `算法`
  - 调度算法切换
  - 路径规划算法切换
- `规划`
  - 网格大小
  - `X` 最小/最大
  - `Z` 最小/最大
  - 高度最小/最大
  - 对角搜索开关
  - 障碍自动配置开关
- `机群`
  - 无人机数量
  - 飞行速度
  - 仿真倍速
  - 同步当前设置
  - 重建机群
- `显示`
  - 规划路径显示开关
  - 飞行轨迹显示开关
- `镜头`
  - 总览
  - 跟随
  - 下一架
  - 跟随高度
  - 跟随距离

当前判断：

- 右侧运行时面板已经是当前版本最重要的交互入口之一
- 很多原来必须在 Inspector 里改的参数已经迁移到运行时

## 4.9 结果导出与批量实验

核心文件：

- `Assets/Scripts/UAV/Controller/SimulationResultExporter.cs`
- `Assets/Scripts/UAV/Controller/BatchExperimentRunner.cs`
- `Assets/Scripts/UAV/Model/SimulationExperimentRecord.cs`
- `Assets/Scripts/UAV/Model/SimulationExperimentDetailExport.cs`

当前真实能力：

- `SimulationResultExporter`
  - 支持自动导出 CSV 记录
  - 支持手动导出 CSV
  - 支持手动导出 JSON 明细
  - 支持自定义导出目录
  - 支持默认导出目录回退
  - 支持按 `根目录 / 日期 / 会话` 自动归档
  - 支持手动创建新导出会话
  - CSV 会在表头不兼容时自动写入 `_v2` 文件，避免旧文件结构被污染
- `SimulationExperimentRecord`
  - 记录单轮实验摘要
  - 当前字段已包含等待次数与冲突次数
- `SimulationExperimentDetailExport`
  - JSON 明细根对象
  - 包含规划参数快照
  - 包含逐机详情
  - 包含逐任务详情
- `BatchExperimentRunner`
  - 支持连续多轮自动实验
  - 每轮自动重置、启动、等待完成、导出结果、再次重置
  - 批量开始时自动新建会话归档目录
  - 支持 CSV 与 JSON 同时导出
  - 支持中途停止

当前判断：

- 实验留档链路已经从“只有一份 CSV”升级为“可手动、可批量、可分会话归档”
- 但还没有归档摘要文件和批量实验模板配置

## 4.10 编辑器工具与场景维护

核心文件：

- `Assets/Editor/SimpleCitySceneImporter.cs`
- `Assets/Editor/SimpleCityEnvironmentTools.cs`
- `Assets/Scripts/UAV/Controller/TaskPointEditorHelper.cs`

当前真实能力：

- `SimpleCitySceneImporter`
  - 菜单入口：`Tools/KY UAV/Import Simple City Into Main Scene`
  - 把 `SimpleCityPackage/Scene 01` 合并进主场景
  - 需要时自动创建主场景备份
  - 自动建立 `CityEnvironment`
  - 自动抽取 `Buildings`
  - 自动更新 `DroneManager.obstacleRoot`
- `SimpleCityEnvironmentTools`
  - 菜单入口：`Tools/KY UAV/Apply Simple City Day Environment`
  - 菜单入口：`Tools/KY UAV/Frame Overview Camera To City`
  - 用于恢复白天环境、雾效、太阳光和总览镜头取景
- `TaskPointEditorHelper`
  - 用于编辑器里批量创建和清理任务点
  - 更偏辅助工具，不是当前运行主链路的一部分

当前判断：

- 场景维护工具已经存在
- 当前主场景已经不再建议通过手工大改场景资源来推进项目主线

## 4.11 兼容和历史辅助脚本

当前工程里还有一些脚本仍然存在，但不属于当前多机主链路核心：

- `TaskManager.cs`
  - 偏早期任务分配辅助
  - 当前主线任务分配已由 `SimulationManager + DroneManager + 调度器` 负责
- `TargetPoint.cs`
  - 旧目标点/单目标式辅助组件
- `DroneController.cs`
  - 仍然是单机移动底层核心
  - 但当前主线调度执行已经由 `DroneStateMachine + DroneManager` 包在上层

文档后续若要描述“当前系统”，应优先围绕主链路写，不要把这些兼容辅助脚本误写成核心入口。

---

## 5. 当前已实现功能清单

按“是否已经可以当成现有功能使用”来判断，当前版本已经完成的功能可以归纳为：

### 5.1 平台基础闭环

- 主场景、城市环境、障碍根节点、无人机、任务点、相机、UI 已经连通
- 多无人机可统一生成、启动、暂停、重置
- 运行时默认可自动导入任务数据

### 5.2 算法接入闭环

- 已有调度接口层
- 已有路径规划接口层
- 调度算法可运行时切换
- 路径规划算法可运行时切换
- A* 已接入障碍层、边界、网格与对角参数

### 5.3 任务执行与观察闭环

- 任务点可运行时新增、清空、导入
- 起飞点可运行时新增、移动、删除、清空
- 规划路径与实际轨迹可同时观察
- 总览相机与跟随相机可切换
- 可在无人机之间轮询观察

### 5.4 运行时参数控制闭环

- 运行时调算法
- 运行时调规划边界和高度范围
- 运行时调网格大小
- 运行时调无人机数量、速度、倍速
- 运行时调跟随相机偏移
- 运行时同步当前机群或重建机群

### 5.5 结果导出闭环

- 自动追加 CSV
- 手动导出 CSV
- 手动导出 JSON
- 等待/冲突统计已进导出结构
- 可切换导出根目录
- 可按日期/会话归档
- 可批量运行多轮实验并导出

### 5.6 场景展示闭环

- 主场景已切到城市街区资源
- 障碍识别链路已重新绑定到城市建筑
- 地面已扩展
- 无人机尺寸已做相对场景的展示修正
- 主界面样式和右侧运行时面板已形成稳定视觉框架

---

## 6. 当前仍未真正完成或仍需加强的部分

## 6.1 多机协同规避

当前状态：

- 已有第一版局部避让与等待恢复
- 已有等待次数和冲突次数统计
- 仍然可能在起飞区聚集、窄路相遇、目标点密集区出现重叠和穿透

结论：

- 这部分不能算“正式多机协同避让算法已完成”
- 后续如果继续做，建议作为单独算法专题推进，而不是继续在当前局部规则上无限打补丁

## 6.2 算法对照组仍然偏少

当前状态：

- 调度器只有 `EvenSplit / GreedyNearest`
- 规划器只有 `StraightLine / AStar`

结论：

- 目前已经够演示
- 但如果要做更强论文对比或答辩说服力，还需要再补一类调度或再补一类规划方法

## 6.3 结果归档仍不够“论文友好”

当前状态：

- 已能导出 CSV 和 JSON
- 已能批量实验
- 已能按日期和会话归档

当前缺口：

- 没有批量实验参数模板
- 没有归档摘要文件
- 没有更适合论文表格整理的汇总文件

## 6.4 运行时面板仍未完全覆盖实验级参数

当前状态：

- 关键参数已经迁移一批
- 但更细粒度的实验级参数仍主要依赖 Inspector

例如仍可继续迁移的方向：

- 更细的障碍代理参数
- 更细的避让参数
- 更完整的批量实验配置

## 6.5 场景表现仍可继续精修

当前状态：

- 视觉结构已经足够自然
- 但环境光照、雾、镜头构图、地面材质重复感、城市边界过渡等还可以继续微调

结论：

- 这部分已经不再是“必须先解决的阻塞项”
- 更适合在主功能稳定后做展示强化

---

## 7. 当前完成度判断

### 7.1 已可以明确认定完成的层

- 多无人机统一生成、执行与重置
- 任务点运行时交互
- 起飞点运行时交互
- 调度算法切换
- 路径规划算法切换
- 路径与轨迹可视化
- 运行时参数面板第一版
- 实时统计第一版
- CSV/JSON 导出
- 自定义导出目录
- 批量实验记录
- 日期/会话归档
- 城市场景迁移与障碍链路重建

### 7.2 仍属于“后半程深化任务”的层

- 正式多机协同避让方案
- 更强算法对照组
- 更完整实验归档
- 更完整实验参数面板
- 更强展示质量打磨

### 7.3 一句话结论

当前项目已经具备“可运行、可交互、可切算法、可看路径、可导结果、可批量留档”的完整演示闭环，但多机协同规避仍停留在第一版演示级实现，实验归档也刚进入可用阶段，还没有达到最终论文整理工具链的完成度。

---

## 8. 现在最值得继续投入的开发顺序

### P1：优先级最高

1. 多机协同规避专题升级  
   建议基于文献方案重做，而不是继续只靠局部等待规则堆补丁。

2. 实验导出继续升级  
   建议优先补：
   - 批量实验参数模板
   - 归档摘要文件
   - 更适合论文表格整理的汇总格式

3. 算法对照组增强  
   至少再补一类调度器或再补一类规划器，提升对比说服力。

### P2：有余力再做

- 统计图表化展示
- 更完整的运行时实验参数面板
- 多任务数据集切换
- 更真实的飞行模型或能耗模型
- 展示级场景效果精修

---

## 9. 后续协作默认规则

后续如果继续由他人或 AI 接手，默认遵循以下规则：

1. 先读本文件和 `README.md`。
2. 先以 `Assets/Scenes/Main/MainScene.unity` 的实际运行结果为准，再做结构性修改。
3. 如果增加算法，必须同时写清：
   - 算法解决什么问题
   - 输入输出是什么
   - 如何接入 `DroneManager` 或当前控制层
   - 在 Unity 里如何验证
4. 如果增加运行时交互，优先考虑打包版是否可用，不要只面向 Editor。
5. 每完成一个核心模块，都应同步更新本文档，否则这份文档会很快再次失真。

---

## 10. 当前结论

这个项目已经完成了从“场景原型”到“可交互的多无人机仿真演示系统”的关键跃迁。

当前真正已经落地的核心能力包括：

- 多无人机统一控制
- 调度与路径规划双层算法切换
- 任务点与起飞点运行时交互
- 路径与轨迹可视化
- 相机观察链路
- 运行时参数控制
- 统计、导出、批量实验与归档
- 基于城市街区场景的完整展示环境

现在最值得继续补的，不再是“让无人机飞起来”，而是：

- 更强的多机协同规避
- 更强的实验对照与归档
- 更完整的论文支撑型结果链路

把这三块继续补齐后，这个项目会更自然地从“可演示系统”升级为“可支撑毕业设计实验、答辩和分析的完整仿真系统”。
