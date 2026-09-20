# V11 第一人称角色与摄像机

> V12 已修正本版的静止手臂与空中松键归零问题。当前手臂会播放行走、快走、腾空和落地动作，移动保留惯性；具体参数和验证见 [V12 手臂动作与移动惯性](CoastalV12Motion.md)。下文保留 V11 的历史实现与测试记录。

当前场景：`Assets/Scenes/CoastalTemple.unity`。在 Unity 打开该场景并按 Play 即可。修改前的场景（包括当时尚未保存的编辑）保存在 `Assets/Scenes/Archive/CoastalTemple_V10_BeforeFirstPerson.unity`。

## 修正内容

原先相机位于角色后方 4.18 米并带肩部偏移，仍然是第三人称环绕相机。现在层级为：

`50_Player_And_Cameras / Player_ShoreStart / Eye / Main Camera / FirstPersonHands`

- Main Camera 在 Eye 下的位置和旋转均为零。鼠标左右转动角色的朝向，俯仰只转 Eye；原地转头不会移动角色，也不会绕角色中心公转。
- Eye 位于角色胶囊内，高度 1.62 米；镜头不绑动画头骨，不随走路、游泳骨骼摇摆。主相机近裁面 0.025 米、视野 75 度。
- 玩家画面只渲染原下载人偶的双手和手臂。头、躯干和腿不进入主画面，全身模型只投射环境阴影；编辑检查用全景视角可显示全身。
- 手臂从原 Quaternius 人偶网格按蒙皮权重提取，保留原骨骼、蒙皮、手指和动作，不是另外拼出的程序人偶。手臂网格 6478 个顶点。
- 独立手臂相机只绘制 `FirstPersonHands` 层，作为 URP Overlay 清除世界深度，避免靠墙时双手被墙面裁掉；近眼模型整体缩放为 0.35，保持投影大小并使几何靠近胶囊中心。手臂没有碰撞体，也不驱动角色位移。
- 原地使用原动画的静止持手姿势；游泳使用原浮水与前进游泳动画。游泳、持灯额外调整双臂根部取景，保留原肘部、手腕和手指动作，将肩部切口放在屏幕外。提灯挂到原持灯动画对应的左手，世界照明排除近眼手臂层，避免双手过曝。
- 常规游戏关闭 C、Tab、F1–F3 视角切换。编辑检查功能仍由 `ShowView` / `SetOverview` 显式调用，不属于玩家操作。

## 操作

| 操作 | 按键 |
|---|---|
| 行走、游泳 | WASD |
| 原地转头 | 鼠标 |
| 快走 | Shift |
| 跳跃 / 在水中上浮 | 空格 |
| 下潜 | 左 Ctrl |
| 交互 | E（以附近提示为准） |
| 提灯开关（拾取后） | L |
| 观察线索 | H |
| 释放 / 恢复鼠标 | Esc；释放后也可点击游戏画面恢复 |
| 回出生点 | Home |

Esc 仅释放鼠标，角色仍接受重力和浮水模拟，不会悬停在空中。

## 后续调整

第一人称手臂预制体：`Assets/Prefabs/Player/FirstPersonHands.prefab`。在 `FirstPersonHands` 组件中调整静止、游泳位置及肩部与持灯取景参数；不要为了调整手的位置而移动 Main Camera。双手视野在其 `HandsOverlay` 相机上单独调节。

动画控制器：同目录 `FirstPersonHands.controller`。原始人偶与动画仍位于 `Assets/Objects/Characters/Quaternius`，来源和 CC0 许可保留在该目录。当前使用 `Rig|Punch_Enter` 的收尾静止姿势、`Rig|Idle_Torch_Loop`、`Rig|Swim_Idle_Loop`、`Rig|Swim_Fwd_Loop`。

分工：`CourtyardWalker` 管移动和转头；`CoastalPlayerCamera` 管固定眼部绑定及本地模型显示；`FirstPersonHands` 只管手臂展示；`PlayerLantern` 管提灯持有和照明。编辑菜单 `Coastal Temple / Player / Configure first-person hands and eye camera` 可重新配置手臂与摄像机，不重新生成关卡地形。旧 V10 绑定菜单已移除，避免把当前视角重新改回旧方案。

## 参考依据

- [Epic 第一人称角色示例](https://dev.epicgames.com/documentation/unreal-engine/implementing-your-character-in-unreal-engine)：玩家本地使用附着摄像机的手臂表示，与世界中的完整角色表示分开。
- [Epic 第一人称渲染说明](https://dev.epicgames.com/documentation/unreal-engine/first-person-rendering)：第一人称模型可使用独立视野与近眼缩放；世界表示负责阴影。这里借鉴呈现方式，Unity 项目未使用 Unreal 功能或代码。
- [Unity 6 URP 相机渲染顺序](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/cameras-advanced.html)：使用 Base / Overlay 相机叠加，并在手臂层清除深度。
- [Quaternius 原始资源](https://quaternius.com/packs/universalanimationlibrary.html)。

## 验证与范围

- [场景配置检查](CoastalV11Verification/SceneContract.txt)：8 项通过。
- [Unity Play 运行检查](CoastalV11Verification/PlayChecks.txt)：35 项通过，包含 360 度转头、极限俯仰、原地转头位移为零、真实 CharacterController 贴墙碰撞、肩部断口取景、原水池浮水与下潜、持灯显示和旧视角接口保护。
- 实际 Game 画面：[出生点](CoastalV11Verification/01-FirstPerson.png)、[贴墙](CoastalV11Verification/02-NearWall.png)、[俯视](CoastalV11Verification/03-LookDown.png)、[浮水](CoastalV11Verification/04-Floating.png)、[游泳](CoastalV11Verification/05-SwimForward.png)、[水下](CoastalV11Verification/06-Underwater.png)、[持灯](CoastalV11Verification/07-CarriedLantern.png)。

运行检查调用正式移动与转头入口并推进真实物理和渲染帧，没有注入实体键鼠操作；不等同于整条关卡人工通关。贴墙验证覆盖本次视角问题，不代表已重新检查全场景每个模型的碰撞。既有 V9 关卡、地形和水算法本次未重做；旧 Windows 压缩包仍是历史版本，本次交付以 Unity 场景为准。尚未进行性能基准测试。
