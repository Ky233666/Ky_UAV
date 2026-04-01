# MainScene 脚本挂载与 Inspector 配置指南

本文档说明 MainScene 中**各对象应挂载的脚本**、**Inspector 中需设置的内容**，以及**缺失时需添加的项**。

---

## 一、管理器对象（根节点）

### 1. TaskManager
| 项目 | 说明 |
|------|------|
| **挂载脚本** | `TaskManager.cs` |
| **挂载位置** | 场景根下独立空物体（如 `TaskManager`） |
| **Inspector** | 无必填引用；`allTasks` 在运行时由 `Start()` 通过 `FindObjectsOfType<TaskPoint>()` 自动填充。 |
| **是否需要添加** | 若 Hierarchy 中已有 `TaskManager` 且已挂载该脚本，则无需添加。 |

---

### 2. TaskPointSpawner
| 项目 | 说明 |
|------|------|
| **挂载脚本** | `TaskPointSpawner.cs` |
| **挂载位置** | 场景根下独立空物体（如 `TaskPointSpawner`） |
| **Inspector 必填** | **taskPointPrefab**：拖入 `Assets/Prefabs/TaskPoints/TaskPoint_1` 预制体。 |
| **Inspector 可选** | **parentContainer**：拖入用于存放生成任务点的父节点（可为空，则生成在根下）。 |
| **是否需要添加** | 若未设置 `taskPointPrefab`，点击「添加任务点」会报错，**必须**在 Inspector 中指定 TaskPoint 预制体。 |

---

### 3. TaskUIManager（脚本类名为 TaskPointUIManager）
| 项目 | 说明 |
|------|------|
| **挂载脚本** | `TaskPointUIManager.cs`（Inspector 中显示为 Task Point UI Manager） |
| **挂载位置** | 场景根或 Canvas 下（如与其它 UI 管理放一起） |
| **Inspector 必填** | **spawner**：拖入挂载了 `TaskPointSpawner` 的物体。<br>**addTaskButton**：拖入 `Canvas/PointButton/Btn_AddTask`。<br>**clearButton**：拖入 `Canvas/PointButton/Btn_clearTask`。<br>**importButton**：拖入 `Canvas/PointButton/Btn_Import`。 |
| **Inspector 可选** | **importer**：拖入挂载了 `TaskPointImporter` 的物体（不设则「导入」按钮无效）。<br>**spawnRadius**：添加任务点时的随机半径，默认 20。 |
| **是否需要添加** | 若按钮未绑定，**必须**在 Inspector 中设置上述三个按钮与 spawner；若需导入功能，**必须**设置 importer。 |

---

### 4. TaskPointImporter
| 项目 | 说明 |
|------|------|
| **挂载脚本** | `TaskPointImporter.cs` |
| **挂载位置** | 场景根下独立空物体（如 `TaskPointImporter`） |
| **Inspector 必填** | **spawner**：拖入挂载了 `TaskPointSpawner` 的物体。 |
| **Inspector 可选** | **fileName**：Resources 下文件名（不含扩展名），默认 `"taskpoints"`。需在 `Assets/Resources/` 下放置 `taskpoints.txt` 或 `taskpoints.json`。 |
| **是否需要添加** | 若未设置 `spawner`，导入会报错，**必须**在 Inspector 中指定 TaskPointSpawner。 |

---

### 5. DroneManager
| 项目 | 说明 |
|------|------|
| **挂载脚本** | `DroneManager.cs` |
| **挂载位置** | 场景根下独立空物体（如 `DroneManager`） |
| **Inspector 必填** | **dronePrefab**：拖入带 `DroneController`（建议也带 `DroneStateMachine`）的无人机预制体，如 `Assets/Prefabs/UAV/Drone`。 |
| **Inspector 可选** | **droneCount**：生成数量，默认 4。<br>**spawnOrigin**、**spawnSpacing**、**spawnDirection**：生成位置与间距。 |
| **是否需要添加** | 若使用「多机 + 任务分配」流程，**必须**设置 `dronePrefab`；否则运行时 SpawnDrones 会报错。若场景中只放一架无人机且不用 DroneManager 生成，可不设 prefab 并将 `droneCount` 设为 0。 |

---

### 6. SimulationManager
| 项目 | 说明 |
|------|------|
| **挂载脚本** | `SimulationManager.cs` |
| **挂载位置** | 场景根下独立空物体（如 `SimulationManager`） |
| **Inspector 必填** | **statusText**：拖入用于显示「状态：就绪/运行中/已暂停」的 UI 文本组件。需为 **TextMeshPro - Text (TMP)**（即 `TMP_Text`）。若场景用的是 legacy `UnityEngine.UI.Text`，需改为 TMP 或改脚本中的类型（见下方说明）。<br>**startButton**：拖入 `Canvas/ControlPanel/StartButton`。<br>**pauseButton**：拖入 `Canvas/ControlPanel/PauseButton`。<br>**resetButton**：拖入 `Canvas/ControlPanel/ResetButton`。 |
| **Inspector 二选一** | **droneManager**：多机模式时拖入 `DroneManager` 所在物体。<br>**droneController**：单机模式时拖入场景中唯一的 `Drone`（挂有 `DroneController` 的对象）。 |
| **是否需要添加** | 若状态文字不更新或按钮无反应，**必须**在 Inspector 中绑定 statusText 与三个按钮；并至少设置 droneManager 或 droneController 其一。 |

**关于 StatusText：**  
脚本中类型为 `TMP_Text`（TextMeshPro）。若当前 `StatusText` 是 legacy Text，可：(1) 将 `StatusText` 改为 TextMeshPro - Text (TMP)，或 (2) 在代码中把 `TMP_Text` 改为 `UnityEngine.UI.Text` 并改用 `statusText.text`。

---

## 二、相机与相机管理

### 7. Cameras（父物体） + CameraManager
| 项目 | 说明 |
|------|------|
| **挂载脚本** | `CameraManager.cs` 挂在 **Cameras** 父物体上（或任一包含两台相机的父物体）。 |
| **挂载位置** | 与 `FollowCamera`、`OverviewCamera` 同层级或为其父物体。 |
| **Inspector 必填** | **overviewCamera**：拖入 `Cameras/OverviewCamera` 的 Camera 组件。<br>**followCamera**：拖入 `Cameras/FollowCamera` 的 Camera 组件。<br>**targetDrone**：拖入要跟随的无人机 **Transform**（通常为场景中 Drone 的根 Transform）。 |
| **Inspector 可选** | **followOffset**：默认 (0, 5, -10)。<br>**isOverview**：初始是否为总览视角，默认 true。 |
| **是否需要添加** | 若场景中已有 Cameras 且已挂 `CameraManager`，只需确认 overviewCamera、followCamera、targetDrone 均已赋值。若没有挂 `CameraManager`，**需要**在 Cameras 父物体上添加 `CameraManager` 并设置上述引用。 |

**说明**：键盘 **1** 切换总览，**2** 切换跟随。

---

## 三、UI（Canvas）

### 8. StatusText
| 项目 | 说明 |
|------|------|
| **组件** | 需为 **TextMeshPro - Text (TMP)**（即挂有 `TMP_Text`），以便被 `SimulationManager.statusText` 引用。 |
| **是否需要添加** | 若当前是 legacy Text，要么换成 TMP，要么按上文修改 SimulationManager 脚本支持 `UnityEngine.UI.Text`。 |

### 9. StartButton / PauseButton / ResetButton
| 项目 | 说明 |
|------|------|
| **事件** | 不需要在 Inspector 的 OnClick 里手动挂方法；`SimulationManager.Start()` 中已用代码绑定 `OnStartClicked`、`OnPauseClicked`、`OnResetClicked`。 |
| **前提** | SimulationManager 的 Inspector 中 **startButton / pauseButton / resetButton** 必须正确拖入这三个 Button。 |

### 10. Btn_AddTask / Btn_clearTask / Btn_Import
| 项目 | 说明 |
|------|------|
| **事件** | 由 `TaskPointUIManager` 在 `Start()` 中绑定，无需在 OnClick 里手动添加。 |
| **前提** | TaskPointUIManager 的 Inspector 中 **addTaskButton / clearButton / importButton** 必须正确拖入。 |

---

## 四、任务点（TaskPoint_1 及其实例）

### 11. TaskPoint_1（预制体及场景中的实例）
| 项目 | 说明 |
|------|------|
| **挂载脚本** | `TaskPoint.cs`（巡检任务点） |
| **挂载位置** | 任务点根物体上（预制体 `TaskPoint_1` 已包含）。 |
| **Inspector** | taskId、taskName、description、priority、estimatedDuration、currentState、三色（pending/inProgress/completed）可按需在预制体或实例上修改。子物体需有名为 **Marker** 的带 Renderer 的对象，用于按状态变色。 |
| **是否需要添加** | 预制体已有 `TaskPoint`；若自己新建任务点预制体，**需要**在根物体上添加 `TaskPoint` 并确保有 Marker 子物体。 |

---

## 五、无人机（Drone）

### 12. Drone（预制体 或 场景中放置的实例）
| 项目 | 说明 |
|------|------|
| **挂载脚本（必选）** | **DroneController.cs**：飞控与目标点移动，需在 Drone 根物体上。 |
| **挂载脚本（强烈建议）** | **DroneStateMachine.cs**：状态机（Idle/Moving/Waiting/Finished）。若使用 `DroneManager.SpawnDrones()`，运行时会自动为每架机添加并关联；若场景中**直接放置**一架 Drone 且希望走状态机逻辑，**需在预制体上预先添加** `DroneStateMachine`。 |
| **Inspector（DroneController）** | **speed**：飞行速度（如 5）。**arriveDistance**：到达判定距离（如 0.5）。**targetPoint** 由状态机或外部设置，通常无需在 Inspector 里拖。**stateMachine** 可由 DroneManager 运行时赋值，预制体上可留空。 |
| **Inspector（DroneStateMachine）** | **droneController**、**droneData** 可由 DroneManager 在生成时自动设置；若为场景中单机，可只设 **droneController**（拖自身或同物体上的 DroneController），**droneData** 会在 Awake 中 new。**waitTimeout** 可选，默认 10。 |
| **是否需要添加** | 若当前 **Drone 预制体**只有 `DroneController` 没有 `DroneStateMachine`：<br>• 使用 DroneManager 生成多机时，可保持现状（运行时会加状态机）。<br>• 若场景中单独放了一架 Drone 且希望按任务队列飞行，**需在 Drone 预制体上添加 `DroneStateMachine`**，并确保 SimulationManager 使用 **droneManager** 引用且 DroneManager 用这架机或生成多机。 |

---

## 六、其他对象（无需脚本）

- **Main Camera**：可由 Cameras 下的 Overview/Follow 替代，或保留做编辑用；无需额外脚本。
- **Directional Light / Ground / Buildings / SpawnArea / TestTarget**：无需挂载本项目的 Controller 脚本。
- **EventSystem**：Unity UI 默认，无需改。

---

## 七、检查清单（缺失时需添加）

| 对象/功能 | 检查项 | 缺失时操作 |
|-----------|--------|------------|
| **TaskPointSpawner** | 是否已设 `taskPointPrefab`？ | 将 `TaskPoint_1` 预制体拖到 taskPointPrefab。 |
| **TaskPointUIManager** | 是否已设 spawner、addTaskButton、clearButton、importButton？ | 在 Inspector 中拖入对应物体/组件。 |
| **TaskPointImporter** | 是否已设 `spawner`？ | 拖入 TaskPointSpawner 所在物体。 |
| **DroneManager** | 是否使用多机？若使用，是否已设 `dronePrefab`？ | 将 Drone 预制体（含 DroneController，建议含 DroneStateMachine）拖到 dronePrefab。 |
| **SimulationManager** | statusText、startButton、pauseButton、resetButton 是否已设？ | 拖入 StatusText（TMP）、三个 Button。 |
| **SimulationManager** | 多机时是否设了 `droneManager`？单机时是否设了 `droneController`？ | 二选一拖入。 |
| **CameraManager** | overviewCamera、followCamera、targetDrone 是否已设？ | 拖入两台 Camera 与 Drone 的 Transform。 |
| **StatusText** | 是否为 TMP_Text？ | 换成 TextMeshPro 或改 SimulationManager 支持 Legacy Text。 |
| **Drone 预制体** | 场景中单机且需状态机时，是否有 `DroneStateMachine`？ | 在 Drone 预制体上添加组件 `DroneStateMachine`。 |
| **导入任务** | 是否需从文件导入？ | 在 `Assets/Resources/` 下放置 `taskpoints.txt` 或 `taskpoints.json`，并在 TaskPointImporter 中设置 fileName（默认已为 "taskpoints"）。 |

---

## 八、脚本与对象对应关系速查

| Hierarchy 对象 | 应挂载脚本 | 关键 Inspector 设置 |
|----------------|------------|---------------------|
| TaskManager | TaskManager | 无（运行时收集 TaskPoint） |
| TaskPointSpawner | TaskPointSpawner | taskPointPrefab, parentContainer(可选) |
| TaskUIManager | TaskPointUIManager | spawner, addTaskButton, clearButton, importButton, importer(可选) |
| TaskPointImporter | TaskPointImporter | spawner, fileName(可选) |
| DroneManager | DroneManager | dronePrefab, droneCount 等 |
| SimulationManager | SimulationManager | statusText, startButton, pauseButton, resetButton, droneManager 或 droneController |
| Cameras | CameraManager | overviewCamera, followCamera, targetDrone |
| TaskPoint_1（及实例） | TaskPoint | 预制体已有；注意 Marker 子物体 |
| Drone | DroneController + DroneStateMachine(建议) | speed, arriveDistance；状态机可由 DroneManager 运行时填 |

按上述配置后，MainScene 的脚本挂载与 Inspector 即可完整对接，无需在 OnClick 中重复绑定事件。
