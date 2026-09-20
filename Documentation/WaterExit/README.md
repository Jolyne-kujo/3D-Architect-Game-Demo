# 水中上岸、攀爬与光源指示灯

> 此页保留第一版实现与当时验证记录。当前上岸、浮水高度和楼梯规则已被 [TraversalV2](../TraversalV2/README.md) 替代：不再需要空格抬身抓边，上下楼动画已启用。

2026-09-20。Unity 6000.5.9f1，主场景 `CoastalTemple`、试验场 `MechanismPlayground` 和 `Assets/Prefabs/Player/ExplorerThirdPerson.prefab` 已同步。

## 试玩

- 在水下踩到台阶 / 池底 / 浮板后切回站立，空格可以起跳。空中松开方向键仍保留惯性。
- 朝平台边缘前进，低于站立肩高且上方有足够空间时自动攀上；水面略高的浮板，按住前进和空格抬身后抓边。空旷水域保持原来的浮水高度。
- 接近水面的很低台阶使用短跨步，避免浮水高度与台阶之间出现上不去的死区。较高边缘使用新下载的 `Braced Hang To Crouch` 动作，并用双手 IK 贴合平台边缘。
- 新发生器初始关闭，E 循环 **黄 → 蓝 → 红 → 关闭**。NOW 为当前光色，NEXT 为下次 E 的光色；熄暗表示关闭。黄光先测试临时消失，蓝光使红墙恢复，红光最后测试永久消失。重新进入 Play 可重置永久状态。

`Ascending Stairs`、`Descending Stairs`、`Braced Hang To Crouch` 三个文件均已导入为有效 Humanoid，复用红 Bot 骨架。前两个楼梯动作保留备用，正常楼梯继续使用原来的行走 / 跳跃。

## 维护与调节

- `Assets/Scripts/WaterDemo/Runtime/CourtyardWalker.cs`：移动、水层采样、着地状态和水中起跳宽限；不依赖场景、关卡或光路系统。
- `CourtyardCharacterQueries.cs`：无每帧数组分配的脚底、落点和空间查询；忽略触发器、自身和不可行走墙面，使用角色所在物理场景。
- `CourtyardLedgeClimb.cs`：可复用攀爬组件。`shoulderHeight` 默认 1.45 m（按角色高度缩放）、`reach` 0.26 m、`duration` 1.13 s；`waterReachLift` 为靠近可达边缘且按住空格时的抬身范围。平台位置在其局部空间记录，可随平台移动、旋转、正值缩放。
- 攀爬检测要求真实碰撞表面及可容纳完整人物的落点。播放曲线期间只临时忽略所抓平台与角色之间的碰撞，其他墙体 / 顶棚仍参与碰撞；结束、取消、重生、穿门及组件停用恢复原始碰撞状态。平台消失会中止动作；头顶新出现障碍时取消并尝试退回安全位置。
- `Assets/Scripts/Gameplay/Player/RiggedPlayerAnimation.cs` 与 `LedgeClimbHandIK.cs`：动画观察移动状态，不驱动角色根运动。手部 IK 挂在 Animator 所在模型节点，Base Layer 开启 IK Pass。低边缘跨步用行走姿态。提灯手臂覆盖层在攀爬时退回。
- `Assets/Scripts/Gameplay/Light/LaserEmitterConsole.cs`：`colorCycle` 调整每个实例的颜色顺序；`indicator` 是前端灯头，`currentReadout` / `nextIndicator` 是背面两盏灯。HUD、指示灯与 E 使用同一颜色序列。
- 双灯外观已经存进 `LaserDevice.prefab`，随根缩放和旋转。使用共享 Unlit 材质与 MaterialPropertyBlock，不生成材质实例，不增加实时点光源。

## 验证证据

- `Baseline.txt`：修复前，水下脚踩台阶仍游泳、无法正常起跳的复现。
- `RegressionChecks.txt`：真实 CharacterController、水体、物理碰撞及浮板预制体的隔离检查；覆盖水下着地、起跳、低台阶、肩高限制、顶棚限制、移动平台、消失 / 新障碍取消、碰撞恢复与颜色指示。
- `ShowroomChecks.txt`：实际试验场的整段水下楼梯上岸、浮板上岸、原生攀爬状态，以及黄 / 蓝 / 红石幕顺序。
- `SavedAssetChecks.txt`：重新读取主场景、完整玩家预制体、三个动画和四个发生器实例，确认改动已保存。
- `Documentation/CoastalV12Verification/FrameRateChecks.txt`：30 / 60 / 144 Hz 空中惯性与地面移动检查。
- `Documentation/CoastalTemple/MechanismLightChecks.json`：颜色规则、E 交互和旋转缩放检查。
- `GuidedPlatformChecks.txt`：浮力、导轨及角色随平台移动检查。

自动截图在同一个编辑器回调中逐步模拟，使用 BakeMesh 冻结当时原生 Animator 骨骼姿态，避免 GPU 重用该帧首个姿态。`05-ClimbLiveFrame.png` 另在实际运行帧更新后验证了双手贴边。

本次在 Unity 编辑器验证，未重新打包 Windows 可执行文件。

运行检查通过；项目原有的 Package Manager `The "path" argument must be of type string. Received undefined` 提示仍存在，本次没有修改包管理配置。它未阻止本次脚本编译及 Play 验证。
