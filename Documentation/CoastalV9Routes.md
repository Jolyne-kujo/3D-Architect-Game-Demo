# 海岸 V9：断路玩法与编辑说明

三段路线位于 `Assets/Scenes/CoastalTemple.unity` 的 `40_Tutorial` 下。原背景地形和岩石已按断口切除；机关改变的是脚下能否站立、平台高度和光线路径，而不是在完整道路上开三扇门。

## 玩家先知道这些

- WASD 移动，Shift 快走，空格跳跃 / 水中上浮，Ctrl 下潜；C 切换视角，Tab 全景观察。
- E 操作最近且真实可达的控制台；实体遮挡和距离会影响操作。L 开关已拾取的提灯，普通提灯仅照明，不给光机关供能。
- H 是可选的渐进提示：先提醒观察，再指出相关结构，最后给出更具体的思路。读不读提示都不影响机关。
- **失足返回本段入口，机关保留当前状态。** 当前站点 HUD 常驻显示这条规则，不需要先按 H。恢复盒覆盖断口下方，不重置本次运行中已经搬好的桥、光门高度或提灯。Home 则回最初的岸边出生点。退出 Play 会恢复编辑器中保存的初始场景。

| 看到的结构 | 它遵循的规则 |
|---|---|
| 金色光桥 | 受光器真正受光后出现；失光时桥面和碰撞一起消失 |
| 带双条纹的红色承重板 | 受光消失，失光后恢复；恢复位置有人或物体占据时会等待 |
| 单道破裂纹的永久红石幕 | 持续照射后消散，移光后不重新封住光口 |
| 光驱动的墙 / 石桥 | 光决定移动方向；移光后停在当前位置 |
| 手动卷扬 | 不需要光；多处手柄共用正向→停止→反向→停止的状态 |

## 三段的不同判断

**01 借光断廊**：先让光通过破裂的光口和红色石板，到达受光器，借金色桥到稳定中岛；再从中岛收光，让下一段红色承重板恢复，走向提灯。关键是同一束光交换两种支撑，稳定中岛是重新判断的位置。入口、中岛和另一端都有共用光镜手柄。

**02 断阶工坊**：移动石墙的顶部就是可乘坐的台面。先把墙停到中层侧廊，操作不依赖光的横向卷扬，将石桥中线对准金色刻线后停车；再乘墙到上层经过接好的桥。墙上有随行控制，固定落脚点也能召回。升得更高本身不能补上上层缺口，垂直运送和水平搭桥是两项不同操作。

**03 折光修桥**：先用经过光门的光搬动红石桥 A，把它停在中线上，再收光走到中墩。直接用低位出口送光，会消去红板并撞到实体中墩，远端桥 B 仍未获能；抬高出口，让光越过这些结构，才会既保住 A 又构成上升光桥 B。入口、中墩和出口均有光路控制与高度卷扬。这里组合了位置保留、受光显隐和真实遮挡，光门只传光，不传送角色。

以上是布局和预期解题关系；整段无跳跃 / 无传送通行与回程效果，以独立路线验收报告为准。

## 水池和游泳仍保留

原庭院排水实验已拆成 `CourtyardDrainExperiment`，水量、闸门和浮物由这个独立组件控制。旁边的 `05_Optional_WaterMirage` 水投影实验保留为可选观察内容，不是三段主路的进度门槛。

角色仍可游泳：空格上浮、Ctrl 下潜。局部跌落恢复盒只服务断路失败；布置或修改它时不要覆盖整片允许游泳的海域。海湾边界的回岸规则与各段局部恢复是两个独立功能。

## 直接修改已经保存的场景

**正常编辑不需要运行生成器。** 在层级中展开 `40_Tutorial`，修改已有模型、碰撞、灯光、受光器或组件引用，保存场景即可。没有在 Awake 中重建这些关卡的逻辑。

- 移动桥 / 墙：编辑对应 motor 的 `platformBody`、`bottom/top`、`positionsAreLocal` 和 `speed`。手动改变初始平台位置后，也应检查起点和端点是否仍与结构对齐。
- 增减光桥段：修改 `SolidPath.renderers/colliders` 数组，保持可见与承重对象一致；受光器自身不要纳入会消失的桥面数组。
- 移动光门：调整 `PortalRailConsole.docks` 和相应高度电机；保持一个 Transform 只有一个位置控制源，额外自由度使用父子支座。
- 增加操作点：Aim / Portal 的 relay 引用原控制台；卷扬手柄引用同一 motor。不要复制独立的状态来模拟“同一个手柄”。
- 改提示与奖励：编辑 `TutorialJourney.lessons`。`PlayerReachedCondition` 只观察到达范围，不开启机关。
- 改恢复：编辑各段 `FallRecoveryRegion.center/size` 与 `safePoint`。安全点应在稳定地面、区域之外；保留入口常驻的恢复说明。恢复盒必须在原有世界高度 `-12` 回出生点保护之前接住角色。

## Prefab 拖入重用

可将以下任一 prefab 拖入已有场景，再移动整个根对象：

- `Assets/Prefabs/Tutorial/V9/01_BorrowedBridge.prefab`
- `Assets/Prefabs/Tutorial/V9/02_StoneWorkshop.prefab`
- `Assets/Prefabs/Tutorial/V9/03_FoldedBridge.prefab`

内部光路、桥墙电机、共用手柄和停靠点引用随 prefab 保留。**跨场景的玩家引用不能保存在这些资源里**：拖入后手动把根上的 `FallRecoveryRegion.walker` 指向当前角色，把 `PlayerReachedCondition.player` 指向该角色 Transform；保存的 prefab 中这两项本来就是空引用。到达奖励是可选的，使用它时再把条件挂到当前场景的 `TutorialJourney.lessons`。

当前玩家需要配置 `PlayerInteractor`；拾取提灯还需要它绑定真实 `PlayerLantern`。已有 V9 玩家已具备这些模块，新场景可按 [模块架构说明](CoastalV9Architecture.md) 绑定。没有教学 HUD 也能操作机关。

Prefab 不会自动挖空放置地点的地形，也不包含整片海岸。若地面仍从桥下连通过去，需要由作者编辑地形和碰撞，否则断路会成为可绕过的装饰。复用时建议保持根缩放为 1，先移动 / 绕竖轴旋转，并重新检查落脚点、光路及控制台遮挡。

## 显式重建 recipe 与地形切口

菜单 **Coastal Temple → Routes → Author V9 Broken Routes (replaces tutorial)** 调用 `CoastalTemple.Editor.TutorialV9Authoring.Build()`。只有显式执行它才重建：它会替换 `40_Tutorial`，写回 `TutorialV9` 下的生成资源，并重新应用地形切口。它不是普通保存或进入 Play 必须运行的步骤；重新生成会覆盖生成部分的手工改动。

当前 recipe 只查找活动场景中的玩家、旧教学根和地理根。旧 V8 authoring 已从菜单退役，不应用它重建 V9 布局。

Recipe 在 `Assets/Scripts/Editor/Gameplay/Routes/`；实际几何、场景引用和 prefab 已保存，可直接维护。切口的原始资源引用与备份位于 `Assets/Objects/LevelGeometry/Tutorial/V9/Excavation`，恢复清单名为 `TutorialGapRestore.json`。

要恢复原背景地形 / 岩石，在 Edit 模式打开原 `CoastalTemple.unity`，明确执行：

```csharp
CoastalTemple.Editor.TutorialGapExcavator.Restore(
    CoastalTemple.Editor.TutorialV9Authoring.ExcavationFolder);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(
    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
```

这会恢复清单记录的原始地形和网格碰撞引用，**不会删除三段机关**。派生切口资源与备份会保留。修改切口位置时应从原始数据开始：`Begin(folder, geographyRoot)` 会先恢复上轮清单，再按新边界重新调用 `Cut(frame, bounds, id)`；不是只移动桥模型就自动移动已挖掉的地形。切割 frame 要求世界缩放为 1，且只绕竖轴旋转。

## 验证结果

Unity 已独立通过：交互 40/40、排水与迁移 20/20、升降组件 11 项、切口几何 20 项；Play 模式独立物理场景内机关 31/31、跌落恢复 15/15。

最终场景已通过从原出生点到半山腰的连续自动实走：主线 89 条记录、19 次正常近距交互、0 次主线传送。另加真实跌落恢复验证后，共 103 条通过；4 次测试摆位与 3 次实际自动回点单独记录。网格缓冲 19 项、场景绑定 6 项、游泳摄像机 5 项也已通过。

- [Edit 组件记录](CoastalV9Verification/ComponentChecks.txt)
- [Play 物理组件记录](CoastalV9Verification/PhysicsComponents.txt)
- [连续路线与恢复记录](CoastalV9Verification/PlayChecks.md)
- [全部验收结果和验证范围](CoastalV9Verification/README.md)

自动实走使用真实 CharacterController、帧更新、移动平台物理与正常交互检测，没有注入实体键盘鼠标事件，不等同于完整人工试玩。第一段断口另做了真实跑跳与恢复测试，未声称逐一跑跳测试其他断口。

Windows 构建成功（0 错误、486 条警告）；试玩压缩包位于 `Builds/CoastalRoutes-Windows.zip`，完整性和内容检查通过。独立程序的启动检查见验收目录，不能代替 Windows 中全程手动试玩。
