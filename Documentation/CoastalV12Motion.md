# V12 手臂动作与移动惯性

> 手型和摆臂取景已在 [V13](CoastalV13RelaxedWalk.md) 改为放松手指与低位交替摆臂；本页的移动惯性设置仍有效。

已保存到 `Assets/Scenes/CoastalTemple.unity`。打开场景按 Play 试玩。修改前的场景保存在 `Assets/Scenes/Archive/CoastalTemple_V11_BeforeMotionFix.unity`。

## 问题原因与修正

V11 的世界人偶播放行走动画，因此影子会动；第一人称手臂却使用 `Punch_Enter` 的静止收尾姿势，状态播放速度为零，只给整个模型加了很小的位移。这不是正常的行走手臂动画。实际检查复现了行走期间手臂骨骼角度变化为 0°。

现在第一人称手臂使用下载资源中的 `Idle_Loop`、`Walk_Loop`、`Jog_Fwd_Loop`、`Jump_Loop`、`Jump_Land` 和两段游泳动画。实际水平速度、着地与游泳状态驱动状态切换；骨骼参数在动画计算前更新，随后把原动作中的摆臂映射到第一人称取景。摄像机仍固定于 Eye，手部动画不移动摄像机和角色胶囊。

持灯采用单独的左臂动画层，左手保持持灯，右臂仍随行走、快走和跳跃运动。游泳时暂时让双臂都使用游泳动作。手臂、模型、蒙皮与动作仍来自原下载人偶。

旧移动代码每帧直接把按键换成位移，没有保存水平速度。实际复现：约 5.3 米/秒起跳，空中松键后速度立即归零。现在保存世界坐标中的水平速度：

- 地面有加速与刹车，松键后短距离停下。
- 空中松键只施加很小的空气阻力，保留起跳速度；转头不会旋转已有的运动方向。
- 空中按方向键逐渐改变速度，反向键不会瞬间反向。
- 落地恢复地面刹车；撞墙消除朝墙面的速度，沿墙方向仍可滑动，不储存撞墙后的冲量。
- 游泳逐渐趋近游泳速度与水流速度；重生清除已有速度。释放鼠标不直接清零空中惯性。

## 手感参数在哪里改

选中 `50_Player_And_Cameras / Player_ShoreStart`，在 `CourtyardWalker` 的 **Horizontal movement** 分组调整：

| 参数 | 当前值 | 用途 |
|---|---:|---|
| `walkSpeed` | 3.4 m/s | 普通行走速度 |
| `runSpeed` | 5.3 m/s | Shift 快走速度 |
| `groundAcceleration` | 22 m/s² | 地面起步与转向加速度 |
| `groundBraking` | 28 m/s² | 地面松键刹车；更小则滑得更远 |
| `airAcceleration` | 4 m/s² | 空中方向控制力度；更小则更难改变轨迹 |
| `airDrag` | 0.1 /s | 空中无输入时的指数阻尼；设为 0 可完全保留水平速度 |
| `swimAcceleration` | 7 m/s² | 水平游泳加减速 |

第一人称手臂的 `importedSwing` 控制保留原摆臂的可见幅度。`restingAim / movingAim / airborneAim / swimmingAim / carryingAim` 分别调整不同动作的手部取景，`shoulderAnchor` 保持肩部切口在镜头外。需要改变动作时编辑 `Assets/Animations/Player/FirstPersonHands.controller`；它不再包含播放速度为零的静止手部状态。

## 实际验证

- [修改前复现](CoastalV12Verification/MotionChecks-Before.txt)：7 项行为失败，包含静止手臂、空中松键归零和瞬间反向。
- [动作和惯性](CoastalV12Verification/MotionChecks.txt)：15 项通过。骨骼动作真实变化；松键 0.2 秒后速度约为 **5.19 m/s**，起跳前约 **5.30 m/s**；贴墙、落地、重生、空中转头与反向控制均检查通过。
- [30 / 60 / 144 Hz 检查](CoastalV12Verification/FrameRateChecks.txt)：19 项通过。相同起跳速度下，空中无输入滑行 0.3 秒的水平距离约 1.564–1.566 米，帧率间最大差异约 2 毫米。
- [第一人称回归](CoastalV12Verification/FirstPersonRegression.txt)：35 项通过，覆盖固定眼部、无公转、贴墙裁切、游泳、下潜和持灯。
- [教学路线检查](CoastalV12Verification/Route/PlayChecks.md)：89 项通过，原有三段断路、升降石墙、移动平台与光路机关均沿正式路线走通，19 次正常交互，全程没有传送或自动回点；用时约 81 秒。另行的失足回点专项本次未重跑。

实际画面：[待机](CoastalV12Verification/01-Idle.png)、[行走 A](CoastalV12Verification/02-Walk-A.png)、[行走 B](CoastalV12Verification/03-Walk-B.png)、[快走 A](CoastalV12Verification/04-Run-A.png)、[快走 B](CoastalV12Verification/05-Run-B.png)、[腾空](CoastalV12Verification/06-Airborne.png)。动作对比图在临时测试平面拍摄，主场景没有加入该平面。

检查调用正式移动、转头和交互代码，使用 Unity 原生角色碰撞与真实渲染帧；没有注入实体键鼠，不等同于完整人工试玩。本次没有更新旧 Windows 试玩压缩包，当前结果以 Unity 场景为准。
