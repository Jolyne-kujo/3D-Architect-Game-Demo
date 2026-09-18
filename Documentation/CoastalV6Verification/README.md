# V6 · 模型拼装尖岬

场景：`Assets/CoastalTemple/Scenes/CoastalTemple.unity`。本机试玩：`Builds/CoastalHeadland-Windows/CoastalTemple.exe`；可分享试玩压缩包：`Builds/CoastalHeadland-Windows.zip`。

## 地形与路线

按三张摘星崖参考图的路线与尖岬轮廓重新构建，使用用户提供工程中的 PureNature 岩石，未使用原神原版地形文件。

- 地图上方为北（Unity +Z）：起点与水池遗迹位于北岸沙滩。
- 绕西侧向南上山，在约 23 米的半山形成较宽的缓坡地带。
- 从侧面转向东北，沿三角形山体较低的左边进入神庙区域，再走向约 38 米高的尖端。半山到尖端高差约 15 米，神庙基础约 31 米。
- 旧的两个方台已停用。尖岬、山肩、崖脚使用 13 个已有模型派生的静态网格；Terrain 仅负责沙滩、路面和衔接。

所有环境已保存为场景和资源，正常游玩不执行建模脚本。`09_ModelHeadland` 内各模型可以分别移动、缩放、更换材质。后续美术可替换单个模块，并保留对应行走面与碰撞。

## 实际 Unity 渲染

![整体](01-Overview.png)
![尖岬岩壁](02-PointedSummit.png)
![北朝上的俯视地图](03-TopMap.png)
![出生点仰望](04-Spawn.png)
![半山连接缓坡](05-HalfwaySlope.png)

另有 `06-SouthboundTrail.png`、`07-NorthSea.png` 和用于选择原始岩石的 `SourceModels.png`。这些图均来自 Unity 编辑器实际渲染；不是概念生成图。

## 验证范围

- `RouteChecks.json`：真实 CharacterController.Move 的主路和尖脊往返，共 42 段通过；起点能看到神庙。每段行走中没有跳跃或瞬移。属于自动运行检查，不等同手动键盘试玩。
- `RuntimeChecks.json`：19 项海浪、游泳水体选择、近远海交界死亡、返回新出生点等检查通过。近岸仍为 6192 格、15 Hz；本机编辑器测得模拟与网格更新合计约 3.95 ms/次，折算 60 FPS 约 0.99 ms/帧，仅此组件开销，不是整个游戏帧耗时。
- `PoolChecks.json`：8 项搬迁后的折射解锁、排水守恒、海水独立、浮台下降与楼梯进出通过。
- `SceneChecks.json`：重新载入并进入 Play 后，13 个模型、98,921 个三角形、23 个材质槽；碰撞与可见网格匹配，无缺失脚本或活动材质错误。四张导入纹理均为 1024，SRP Batcher 保持开启。
- `BuildChecks.json` 与 `PlayerStartup.json`：Windows 构建成功，0 错误；Direct3D 12 实际启动并持续运行 134 秒，无异常日志。启动检查不包含手动键盘试玩。58.84 MB 压缩包通过完整 CRC 检查。构建仍有 486 条包级警告，样例来自 Unity AI Inference 的着色器变体及未配置的 Pipeline 运行资产，保留在 `BuildReport.json`，本次未改动这些依赖。

## 控制与编辑

WASD 行走/游泳，Shift 快走，空格跳跃/上浮，Ctrl 下潜，Tab 观察；F1 全景、F2 半山、F3 神庙；Home 回岸边。1 开启水池排水，R 重新蓄水，V 显示水流。

编辑源文件：`Tools/Terrain/AssembleHeadland.cs.txt` 是一次性编辑器建模配方，运行会重建相关网格和 Terrain；正常打开项目无需执行，手工美术调整后也不要直接重跑覆盖调整。`VerifyHeadlandRoute.cs.txt`、`VerifyHeadlandPool.cs.txt` 和 `CaptureHeadland.cs.txt` 分别用于碰撞、水池和画面检查。

本版是可玩地形原型：建筑仍为白模，草顶与岩壁采用静态材质分区，尚未进行植被、碎石和接缝细节美术。近岸水仍为高度场模拟，远海为轻量波浪表面，未改成完整三维液体模拟。
