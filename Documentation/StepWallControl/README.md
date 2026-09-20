# 自动跨步上限与防蹭墙

2026-09-20，Unity 6000.5.9f1。

角色主体使用 **CharacterController**，由 `CourtyardWalker` 调用 `CharacterController.Move`。重力、加速、空中惯性和游泳由控制器代码计算；角色根节点没有 Rigidbody。浮板和其他物理物体仍可使用 Rigidbody。

## 当前参数

主地图、实验场以及 `Assets/Prefabs/Player/ExplorerThirdPerson.prefab`：

| CharacterController 参数 | 值 |
|---|---|
| Height | 1.80 m |
| Radius | 0.28 m |
| Center | (0, 0.90, 0)，几何底部在角色原点 |
| Skin Width | 0.028 m，即 Radius × 0.1 |
| Step Offset | 0.30 m |
| Min Move Distance | 0 |
| Slope Limit | 50° |

在停止 Play 后修改玩家 CharacterController 的 Step Offset 可改变自动跨步上限。运行时控制器会缓存该上限，游泳与攀爬期间临时调整原生 Step Offset，回到地面后恢复。

## 实现

新增独立的 `Assets/Scripts/WaterDemo/Runtime/CourtyardStepGuard.cs`，在着地且未跳起时，对本帧水平位移做一次预检查：

1. 根据脚下实际支撑平面求高度，避免把胶囊 Skin Width 的离地余量算成额外可跨步高度。
2. 在最大台阶高度略上方，用覆盖角色脚部宽度的薄横向胶囊扫掠前方。连续覆盖避免细柱、斜墙转角漏在射线间隙。
3. 碰到超过台阶上限、且法线不属于可行走斜坡的面，只保留安全接近距离和沿墙的水平滑动；不把方向投影到完整三维斜面上，不因此生成向上分量。
4. 最终仍由 CharacterController.Move 执行碰撞移动。合法低台阶由原生跨步加既有顶部/净空检查处理，楼梯保留连续斜坡碰撞。跳跃、深水游泳和主动高台攀爬使用各自的规则。

新增检查每个符合条件的运动帧执行一次 CapsuleCast，复用 24 个结果的缓冲区，不使用 CastAll 或逐帧创建数组。缓冲区满时保守停止该帧水平移动。脚部 IK 继续负责左右脚贴合外观台阶，不向身体施加额外的刚体力。

物理材质降摩擦和 Rigidbody.velocity 不用于此角色的移动驱动。实际浮板的刚体系统不受本次修改影响。

## 关卡制作

- 常规可跨越小台阶建议不高于 0.25 m，与 0.30 m 上限留余量。
- 需要阻挡的矮墙建议从 0.45～0.50 m 起，避免关卡依赖恰好卡在阈值的浮点接触。
- 可跑楼梯继续使用隐藏的连续斜坡碰撞，外观可保留逐级台阶。
- 真正的坡面由法线和 50° Slope Limit 判断。不要用“加一个可行走倒角”作为禁止攀登的保证。

## 验证

`Checks.txt`：40 项原生物理自动检查全部通过。包含 30 / 240 / 500 FPS 下的 0.12、0.25、0.30 m 台阶（薄厚两种），0.32、0.35、0.50、0.60 m 直墙阻挡，沿墙滑行、正常跳过 0.60 m 障碍、抬高后的地面、25° 旋转墙角、偏离正中心的 5 cm 细柱、零跨步设置，以及 40° / 45° 可走坡、60° / 70° 不可走坡。

`Before.txt` 保留修改前原始输出：即使参数先设成 Step Offset=0.30、Skin Width=0.028，在三种帧率下仍会跨过 0.32 m 台阶。该旧报告的跳跃失败是测试仅观察跳起后 22 帧导致的误判，后来改为 34 帧，以覆盖原有空中加速过程；没有因此提高角色跳跃速度或削弱碰撞。

实际实验场四块投影台阶另用 30 / 60 / 144 / 240 / 500 FPS 的共享输入入口验证，仍可连续走过和跑过；在原卡点停下再起步也通过。主地图与实验场的楼梯、水中返回岸边、浅水跳跃、浮板攀爬和左右脚 IK 检查通过。惯性检查 19 项通过，空中松开方向键仍保留速度。报告位于 `../WaterTraversal`、`../StairFootPlacement`、`../WaterExit`、`../TraversalV2` 及 `../CoastalV12Verification/FrameRateChecks.txt`。

这些是 Unity 原生物理、真实场景和共用运动入口的自动验证，不替代完整人工手感验收。编辑器原有 Package Manager `path ... undefined` 窗口错误仍单独存在。

依据：[Unity CharacterController 参数说明](https://docs.unity3d.com/6000.0/Documentation/Manual/class-CharacterController.html)、[CharacterController.Move](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/CharacterController.Move.html)。胶囊圆底和自动跨步组合可能超出 Step Offset 的现象也见 [PhysX Climbing Mode 文档](https://nvidia-omniverse.github.io/PhysX/physx/5.4.1/docs/CharacterControllers.html#climbing-mode)。本项目是否存在该问题以以上实际复现为依据。
