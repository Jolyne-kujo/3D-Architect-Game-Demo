# V14 第一人称身体与世界阴影

当前场景：`Assets/Scenes/CoastalTemple.unity`。

这次修复以摄像机、世界人偶和真实阴影的位置为准，不调整手部动作。

## 原因

场景中的 `QuaterniusMannequin` 局部位置为 `(0.0207, 0, -1.7549)`，局部朝向还多转了约 23.3°。它作为玩家子物体，左右看时会围绕玩家走半径约 1.75 米的圆。原位转 90° 的实际测量：玩家根节点位移为零，人偶根位移 2.48194 米，双脚中心位移 2.48660 米，见 [修复前记录](CoastalV14Verification/BeforeFix.json)。

同时，`RiggedPlayerAnimation` 会朝水平速度转动模型，侧移后留下另一层局部朝向。原模型还用动画裁剪包围盒校准身高，把脚底额外抬高了约 10 厘米；与碰撞体离地余量叠加后，实际脚底约离地 13 厘米。

## 当前结构

```text
Player_ShoreStart             角色位移、碰撞、左右转向
├─ Eye                       固定眼高，只负责俯仰
│  └─ Main Camera            局部位置与旋转均为零
│     └─ FirstPersonHands    镜头内显示，不向世界投影
└─ QuaterniusMannequin        局部位置/旋转为零，世界身体与阴影
   └─ Imported_Model_And_Skeleton
```

`PlayerBodyAnchor` 负责把世界身体固定在控制器脚下，并应用导入模型的 180° 朝向修正。`RiggedPlayerAnimation` 只输入速度、落地和游泳状态，不再根据速度另转身体。模型身高校准使用原始网格顶点，避免动画状态或裁剪包围盒影响校准。

所有镜头内物体，包括提灯，均关闭世界投影。正常游戏下完整人偶使用 `ShadowsOnly`；世界太阳光负责产生真实阴影。调整整个人物位置应选 `Player_ShoreStart`，不要把投影身体移到摄像机后面。

俯仰镜头不会改变世界身体和阴影。左右转向使身体绕脚下原点旋转，阴影轮廓会随真实身体朝向和动作变化；阴影不是锁定在屏幕上的图案。转动光照方向会正常改变阴影方向。

采用了 [Epic 官方第一人称渲染文档](https://dev.epicgames.com/documentation/en-us/unreal-engine/first-person-rendering) 中将镜头表现与世界身体分开的原则：世界表示用于真实阴影，镜头手臂不向场景投影。

## 验证

专项检查使用冻结的实际骨骼姿态、固定观察摄像机与固定方向光，对比完整蒙皮顶点及实际渲染阴影。它覆盖俯仰、360° 原地旋转、单独旋转观察视角、改变光照、侧移和脚底位置，不只检查根节点。

最终结果：专项检查 27 项通过，第一人称回归 35 项通过，无失败。±75° 俯仰时固定观察镜头的阴影图逐像素一致；90°、180°、270°、360° 转向时身体根与摄像机位移均为零，蒙皮顶点符合绕玩家原点的旋转，最大数值误差约 0.000035 米。脚底距测试平面约 0.029 米，接近控制器 0.025 米的碰撞余量。贴墙、浮水、游泳和下潜回归通过。

- [专项检查记录](CoastalV14Verification/ShadowChecks.txt)
- [固定阴影基准](CoastalV14Verification/01-FixedShadow-Level.png)
- [抬头阴影](CoastalV14Verification/02-FixedShadow-Pitch-75.png)
- [低头阴影](CoastalV14Verification/02-FixedShadow-Pitch75.png)
- [世界人偶与脚下阴影](CoastalV14Verification/06-BodyAndShadowAtFeet.png)
- [真实游戏视角阴影](CoastalV14Verification/07-GameplayShadow.png)
- [摄像机与游泳回归记录](CoastalV14Verification/FirstPersonRegression.txt)

回归工具的等待时间改为累计角色实际模拟的步长。原检查在编辑器低帧率时按经过的时间结束，角色每帧最多模拟 0.05 秒，可能还没完成下潜就提前断言；初次记录保留在 `FirstPersonRegression-initial-lowFPS.txt`。

修改前场景已备份为 `Assets/Scenes/Archive/CoastalTemple_V13_BeforeShadowFix.unity`。测试用平面、观察镜头和光源仅存在于运行检查中，不保存在游戏场景。
