# 原庭院排水实验的独立模块

`CourtyardDrainExperiment` 保存原水池、折射光源、可见闸门及浮体引用。`StartDrain()` 开启原光源，`ResetExperiment()` 关闭光源、复原水量和浮体并立即合上可见闸门，`Toggle()` 切换这两种操作；`Powered` 返回实际光源状态。闸门动画继续读取水模拟中的真实闸门状态，因此开启光源不会绕过折射命中与蓄光规则。该模块不需要教程、玩家或总观察控制器。

已有 V8 场景无需重新执行教程作者构建。停止 Play 后，执行 **Coastal Temple → Migrate Existing Drain Experiment**，再保存场景；也可调用 `CoastalTemple.Editor.CourtyardDrainMigration.Migrate(walkthrough)` 后由关卡编辑流程保存。迁移将既有引用复制到新模块并连接原控制台，保留旧组件、物体身份和 `.meta` GUID。重复调用不会重复创建模块，已有非空引用会保留。迁移只在编辑器中显式执行，游戏启动不创建组件或环境。

`CoastalWalkthrough` 仅保留全景、观察点、回起点与调试输入。旧水池字段隐藏保留为迁移输入和旧检查工具的读取入口；旧 `ResetWater()` 已转交独立模块。调试按键 1 / R / V 使用模块引用。正常场景控制台直接调用模块，不依赖 `CoastalWalkthrough`。

验证入口：**Coastal Temple → Tests → Independent Drain Experiment**，或调用 `CoastalTemple.Editor.CourtyardDrainExperimentChecks.Run()`。该检查在独立预览场景中使用真实 `WaterVolume`、`BuoyantBody` 和 Rigidbody，覆盖开启、重复开启、排水、复原水量/浮体/动量/闸门、重复复原、切换、无教程调用、旧场景引用迁移与重复迁移。检查不执行键盘事件，也不替代完整场景的折射排水与行走回归。
