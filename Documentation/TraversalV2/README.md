> 历史版本记录：其中楼梯专用动画、限速和独立预制体的做法已被后续方案替代。当前行为见 [斜坡楼梯与独立脚部落点](../StairFootPlacement/README.md)。

# 主地图、楼梯和水面上岸修正

2026-09-20，Unity 6000.5.9f1。

主地图为 `Assets/Scenes/CoastalTemple.unity`，并未删除。上次编辑器停在独立的 `MechanismPlayground` 试验场。这次修改完成后重新打开主地图；Build Settings 的首个场景仍为主地图。主地图的地形变换逐项比对保持一致，见 `MapPreservation.txt`；本次修改前的场景备份在 `Logs/TraversalBackup/CoastalTemple_BeforeStairPrefab.unity`。

## 当前行为

- 陆地攀爬的下限取 `minimumLedgeHeight` 和实际跳跃高度加 2 cm 的较大值；当前跳跃速度 4.6 m/s，对应约 1.08 m 高度。因此低矮障碍用跳跃，上楼梯不再反复触发抓边动作。1.3 m 的有效平台仍可以攀爬。
- 水下脚底真实着地时优先站立。楼梯使用连续斜坡碰撞，避免每一级台阶导致着地状态抖动；人物按上下行方向播放用户提供的 `Ascending Stairs` / `Descending Stairs`。
- 水面游泳通过红 Bot 的实际 Head 骨骼计算浮水吃深；从直立踩水切换到俯身游泳时，姿态造成的高度变化交给 CharacterController 补偿，仍参与碰撞。不会直接移动模型子节点，也不会用动画根运动移动角色。
- 水中抓边采用独立规则：平台顶面相对水面处于 -0.20～+0.65 m，人物已在水面附近、正朝向平台前进、没有按 Ctrl 下潜，且落点和头顶有足够空间时自动上岸。无需先按空格抬身。主地图旧的浮力升降台与新导轨浮板预制体都已验证。
- 攀爬期间继续跟随所抓平台的局部位置。原有碰撞恢复、平台消失取消、空中惯性和平台随动逻辑保留。

## 拖拽使用楼梯

将 `Assets/Prefabs/Architecture/Staircase.prefab` 拖入场景。根节点位于低端，局部 +Z 指向高端。视觉是可编辑的 14 级台阶，附带顶部接驳面、连续碰撞斜坡和 `CourtyardStaircase` 标记。主地图水池及试验场水池现在都是该预制体的实例。

可以移动、绕竖直轴转向、正值缩放整个根节点。缩放后的坡度仍需在角色 CharacterController 的 `Slope Limit` 内；不能把行走楼梯拉成垂直墙面。不要只缩放视觉台阶而遗漏碰撞斜坡。若自行改变局部阶梯长度，需同步 marker 的 `run` 与碰撞几何。

`ExplorerThirdPerson.prefab` 和两个场景中的玩家都已接入楼梯检测及动画。楼梯本身不持有人物引用，复制多个楼梯无需重新绑定。

## 可调参数和代码边界

| 组件 | 参数 / 职责 |
| --- | --- |
| `CourtyardWalker` | 唯一角色位移入口，水下着地优先；`jumpSpeed` 决定实际跳高 |
| `CourtyardStaircase` | 标识楼梯范围、上行方向；`movementSpeed` 默认 2 m/s，`animationReferenceSpeed` 默认 2 m/s |
| `CourtyardSurfaceSwimmer` | `headClearance` 默认 0.24 m，`poseSmoothing` 默认 0.12 s；骨骼采样与姿态高度补偿，不依赖关卡 |
| `CourtyardLedgeClimb` | 陆地 `minimumLedgeHeight` / `shoulderHeight`；水中 `maximumWaterFreeboard` / `maximumSubmergedLip`；碰撞、落点和攀爬过程 |
| `RiggedPlayerAnimation` | 读取移动状态，驱动楼梯、游泳和抓边动画；不负责物理移动 |
| `RedBotAuthoring` / `TraversalV2Authoring` | 编辑器中生成并保存 Animator、预制体和场景实例；运行时不生成楼梯几何 |

额外水面姿态计算只读取一个骨骼并做标量平滑，不增加水模拟网格、实时光源或每帧查找全部场景对象。此轮没有做 GPU/CPU 性能基准测试。

## 公开实现参考

参考 [Epic Platformer Game Sample](https://dev.epicgames.com/documentation/unreal-engine/platformer-game-sample?application_version=4.27) 中按障碍高度、移动状态和攀爬接触点选择动作的做法，以及 [Character Movement Component](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Engine/UCharacterMovementComponent) 对游泳、浮力与行走台阶参数的分离。这里在现有 Unity CharacterController 上实现相应的状态分流，没有移植 Unreal 代码，也没有声称复刻某款商业游戏的内部系统。

## 验证

- `Baseline.txt`：修改前 0.6 m 台阶触发攀爬，以及前进游泳时头部低于原水线约 0.56 m 的复现。
- `RegressionChecks.txt`：7 项原生物理/Animator 检查，覆盖矮障碍跳跃、高边缘、转向缩放楼梯、涉水楼梯、浮水动画过渡、不按空格上浮板，以及潜水/高墙/低顶限制。
- `MainSceneChecks.txt`：主地图真实水池的整段楼梯上岸、游泳和旧浮力升降台抓边；不是替代测试场景。
- `ShowroomStairChecks.txt`：试验场缩放后的楼梯实际上下行，均出现对应动画且没有触发攀爬。
- `MotorAndPlatformChecks.txt`：19 项 30/60/144 Hz 移动/空中惯性检查，以及 26 项导轨浮力/角色随动检查。
- `SavedAssetChecks.txt`：重新读取保存的场景和预制体，核对组件、动画绑定、缺失脚本和当前主地图。
- `01-MainMap.png` 至 `06-OnMainPoolLift.png`：主地图渲染截图。测试回调中的动作通过原生 Animator 求值并 BakeMesh 捕捉，避免同一编辑器帧内 GPU 重用旧姿态。

以上为 Unity 编辑器运行检查和渲染核验，未重新打包 Windows 试玩程序。原有 Package Manager 的 `The "path" argument must be of type string. Received undefined` 提示仍存在；没有阻止本次脚本编译和运行。
