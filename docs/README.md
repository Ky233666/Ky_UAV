# 文档索引

本目录是项目唯一的工程文档根目录。后续阅读、维护和继续开发时，只需要查看 `docs/` 下的文件，不再区分 `docs/` 与 `Assets/Docs/`。

## 阅读顺序

建议按以下顺序理解项目：

1. `project-overview.md`
   先建立项目定位、范围边界、当前完成度和未完成项的整体认识。
2. `feature-specification.md`
   查看系统已经具备哪些功能，以及每个功能的输入、处理、输出和依赖模块。
3. `system-architecture.md`
   理解系统分层、主控制链、算法接入方式和关键数据流。
4. `module-design.md`
   对照代码目录理解各模块职责、依赖关系和完成程度。
5. `user-guide.md`
   了解如何运行系统、如何在界面中操作和如何观察仿真效果。
6. `deployment-guide.md`
   了解如何做烟雾验证、EditMode 测试和 Windows 打包。
7. `testing-and-evaluation.md`
   查看当前测试范围、实验执行方式和已知限制。
8. `execution-plan.md`
   查看项目收口计划、阶段出口和固定实验矩阵。
9. `function-test-checklist.md`
   查看当前打包版功能验收结果。
10. `stability-test-record.md`
   查看当前稳定性测试结果和剩余人工复核项。

## 文档职责

- `project-overview.md`
  说明项目是什么，不解决什么，当前做到哪里。
- `feature-specification.md`
  说明系统能做什么。
- `system-architecture.md`
  说明系统怎么组织、算法怎么接进来。
- `module-design.md`
  说明代码模块如何拆分。
- `user-guide.md`
  说明用户如何操作。
- `deployment-guide.md`
  说明开发者如何验证和打包。
- `testing-and-evaluation.md`
  说明如何做测试、实验和结果判读。
- `execution-plan.md`
  说明当前开发主线和收口状态。
- `function-test-checklist.md`
  保存功能验收记录。
- `stability-test-record.md`
  保存稳定性验证记录。

## 与代码的对应原则

- 以 `Assets/Scripts/UAV` 和 `Assets/Editor` 中的代码实现为准。
- 如果文档与代码冲突，以代码为准，并应同步修正文档。
- 不把“计划实现”写成“已实现”。
- 当前系统是“二维规划 + 三维场景表现”，不是完整三维路径规划系统。

## 后续开发建议

后续继续开发时，优先关注以下真实缺口：

- 多机局部避让仍是演示级实现，存在 `Moving -> Waiting -> Moving` 抖动。
- 结果对比目前主要依赖导出文件，没有独立的回放与图表界面。
- 动态障碍、禁飞区和正式时空协同避碰尚未实现。
- 运行时交互已较完整，但部分打包版交互仍需要人工回归验证。

如果要继续做功能迭代，先更新 `execution-plan.md`，再同步 `feature-specification.md`、`system-architecture.md` 和对应测试记录。
