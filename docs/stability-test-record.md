# 稳定性测试记录

## 测试环境
- 日期：`2026-04-21`
- 构建版本：`D:\unityhub\project\build\Ky_UAV\Ky_UAV.exe`
- 自动化日志：`Library/Logs/packaged-stability-run.log`
- 运行日志：`C:\Users\KangYun\AppData\LocalLow\DefaultCompany\Ky_UAV\Player.log`

## 结果记录

| 日期 | 用例 | 轮次 | 结果 | 阻塞性错误 | 备注 |
| --- | --- | --- | --- | --- | --- |
| 2026-04-21 | 开始-完成/重置 | 10/10 | 通过 | 无 | 使用 `F5` 启动、等待任务完成后 `F7` 重置，10 轮均正常回到空闲态。 |
| 2026-04-21 | 开始-暂停-继续 | 5/5 | 通过 | 无 | `Player.log` 记录 `Paused` 共 `5` 次，恢复后均能继续完成任务。 |
| 2026-04-21 | 重建机群-重新开始 | 3/3 | 通过 | 无 | 在第 `1 / 4 / 7` 轮执行 `F8` 重建，随后可再次启动仿真。 |
| 2026-04-21 | 2D 俯视持续运行 | 1/1 | 通过 | 无 | 稳定性循环开始后切入 `2D俯视`，后续 `10` 轮未出现崩溃或镜头失效。 |
| 2026-04-21 | 任务完成统计 | 40/40 | 通过 | 无 | `Player.log` 记录 `Moving -> Finished` 共 `40` 次，对应 `10` 轮、每轮 `4` 架无人机。 |
| 2026-04-21 | kinematic 刚体速度警告 | 0 条 | 通过 | 无 | 未再出现 `Setting linear/angular velocity of a kinematic body is not supported.` |
| 2026-04-21 | 空引用/崩溃 | 0 条 | 通过 | 无 | `Player.log` 未出现 `NullReferenceException`，打包版可正常退出。 |
| 2026-04-21 | 建筑告警 | 0 条 | 通过 | 无 | 默认任务集与当前 `Medium` 场景下未出现穿楼或撞楼，`2D俯视` 观察结果与日志一致。 |

## 关注点复盘
- 本轮稳定性测试没有出现卡死、崩溃、空引用或刚体速度警告。
- 默认任务集下建筑告警为 `0`，说明当前示例路径未穿越建筑投影。
- 功能回归中仍能观察到局部避让导致的 `Moving -> Waiting -> Moving` 反复切换，但本轮 `10` 次稳定性循环未发展为死锁。

## 后续人工复核
- 使用运行时面板手动验证 `JSON` 导出与批量实验，补充 `session_manifest.json`、`session_summary.csv` 实际产物。
- 构造一组明确穿越建筑 footprint 的任务点，用于专项验证建筑告警确实会触发。
