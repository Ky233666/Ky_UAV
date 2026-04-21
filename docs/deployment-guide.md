# 部署与构建

## 1. 前提条件

- Unity `2022.3.62f3`
- 项目路径有效且未被其他 Unity 进程占用

## 2. 编辑器初始化

首次构建前建议执行：

1. `Tools/KY UAV/Bootstrap Delivery Assets`
2. `Tools/KY UAV/Run Project Smoke Validation`

作用：

- 生成默认 `DroneConfig` 资产
- 生成实验预设资产
- 验证 `MainScene`、核心对象和关键引用

## 3. Windows 打包

菜单方式：

- `Tools/KY UAV/Build Windows Player`

默认输出：

- `D:\unityhub\project\build\Ky_UAV\Ky_UAV.exe`

批处理方式：

```powershell
& 'D:\unityhub\Unity Hub\Editor\2022.3.62f3\Editor\Unity.exe' `
  -batchmode `
  -projectPath 'D:\unityhub\project\Ky_UAV' `
  -executeMethod KyUavBuildTools.BuildWindowsPlayerBatch `
  -logFile 'D:\unityhub\project\Ky_UAV\Library\Logs\build-windows.log' `
  -quit
```

如需指定输出目录，可设置环境变量 `KY_UAV_BUILD_DIR`。

## 4. EditMode 测试

批处理入口：

- `KyUavEditModeBatchRunner.RunEditModeTests`

结果文件：

- `Library/Logs/editmode-results.xml`

当前覆盖内容：

- 调度容量限制
- 路径规划基础约束
- CSV 字段转义
- 算法名称映射
- 实验预设目录构建

## 5. 烟雾验证

入口：

- `ProjectSmokeValidator.RunSmokeValidation`

检查内容：

- `SimulationManager`
- `DroneManager`
- `CameraManager`
- `TaskPointSpawner`
- `TaskPointImporter`
- `Buildings`
- `CityEnvironment`
- `Canvas`
- `OverviewCamera`
- `FollowCamera`

## 6. 常用日志位置

- Windows 打包日志：`Library/Logs/build-windows.log`
- 烟雾验证日志：`Library/Logs/smoke.log`
- 打包版运行日志：`C:\Users\<用户名>\AppData\LocalLow\DefaultCompany\Ky_UAV\Player.log`
- EditMode 结果：`Library/Logs/editmode-results.xml`

## 7. 构建注意事项

- 若项目被 Unity 编辑器占用，batchmode 会被项目锁阻止。
- `BootstrapDeliveryAssets` 会刷新实验预设和部分字体资产，构建前后可能导致工作区出现资源改动。
- 构建成功前会自动执行资产初始化和烟雾验证，因此打包脚本本身已经承担了最小交付检查职责。
