# 可复用机关预制体

入口目录：`Assets/Prefabs/Mechanisms`。直接拖入 Hierarchy 或 Scene 使用。独立试玩场景：`Assets/Scenes/MechanismPlayground.unity`；场景内所有机关都是这些 prefab 的实例。完整红色 Bot 玩家（含摄像机、E 交互）另存为 `Assets/Prefabs/Player/ExplorerThirdPerson.prefab`。

## 预制体目录

| 文件 | 使用方法 |
| --- | --- |
| `LaserDevice.prefab` | 靠近按 E：关闭 → 黄 → 蓝 → 红 → 关闭。局部 +Z 发射；旋转根节点调整方向。初始关闭。背面上方大圆灯 NOW 显示当前光色，下方小圆灯 NEXT 显示下次光色。 |
| `RedStoneCurtain.prefab` | 同一墙：红光连续照射 0.35 秒后永久消失；黄光照射时消失，移开后恢复；蓝光不影响它。 |
| `VerticalBuoyantPlatform.prefab` | 放在水域里，浮板沿自己的竖轨受浮力升降；水位下降后自然下落；轨架固定。 |
| `HorizontalBuoyantPlatform.prefab` | 放在水面；靠近平台按 E：前进 → 停止 → 返回 → 停止。横向行程与水位浮动独立，端点自动停止。 |
| `PortalPair.prefab` | 推荐直接拖这一整对。展开后分别移动 Portal1、Portal2，内部连接已经配置；复制整对会得到互不串线的新门对。 |
| `Portal1.prefab` / `Portal2.prefab` | 单独放置时，从 Hierarchy 将另一扇门的根拖入 `LightPortal.Paired`，双向各设置一次，不要拖 Project 中的资源文件。蓝色 / 土黄色仅作两端识别。 |
| `RedLightReceiver.prefab` / `YellowLightReceiver.prefab` / `BlueLightReceiver.prefab` | 三种独立受光器，只认同色激光。选中其 `LightReceiver` 组件可修改 `Required Color`，并用 `Activated` / `Deactivated` 绑定机关效果。 |
| `LightReceiver.prefab` | 保留原文件入口，当前默认蓝色，使用相同的单色匹配规则。 |
| `LightBridge.prefab` | 蓝色受光器和光桥已绑定为一整套；只有蓝光能开启桥面及碰撞，移开光线或换成红 / 黄光立即消失。复制整套后引用仍指向自己的受光器和桥面。 |
| `LightDrivenLift.prefab` | 红色接收器受红光照射时升，黄色接收器受黄光照射时降；同时有效则停住。自带 E 手柄，可手动升、停、降。 |
| `DrainGate.prefab` | 放进已有水池底部；黄色接收器连续受到黄光 1.5 秒后开闸，或靠近按 E 手动开闸 / 重置。自动寻找所在水池。 |
| `PoolsideDrainConsole.prefab` | 岸边排水 / 补水台。放到岸上后，把场景的水池拖入 `DrainGateDevice.water`；E 开闸，再按 E 补水。实验场已经绑定完成，也可用黄光照其黄色受光器开闸。 |
| `WaterMirage.prefab` | 根节点放在水面附近、样本浸入水中；蓝色入射光和水下折射光，E 切换照射角度，对齐投影后生成有碰撞的台阶。自动绑定所在水层。 |

永久消失指当前一次运行持续有效；再次进入 Play 或明确重置关卡会复原，没有增加存档系统。临时墙恢复前会检查玩家 / 刚体占位，避免在物体内部突然生成碰撞。

编辑器和 Development Build 可按 **F6** 开启玩家调试激光，**F7** 在红 → 黄 → 蓝之间切换，按住**鼠标右键**从摄像机中心发射。使用普通激光的遮挡、传送门及同色受光规则；默认关闭。主地图、实验场和完整玩家 prefab 均已绑定。岸边排水用于测试浮力板随水位升降；红 / 黄受光器驱动的升降台用于测试光控运动。详细操作与本次检查见 [DebugWater](../DebugWater/README.md)。

发生器根上的 `LaserEmitterConsole.colorCycle` 可按实例设置循环顺序，提示文字、两盏指示灯与 E 的下一档使用同一个数据源。默认黄光先测试临时消失，再切蓝光看石幕恢复，最后才用红光测试永久消失。指示灯是共享材质的发光外观，不增加实时点光源；仅光色变化时更新显示。

玩家的水下着地、楼梯动画和边缘攀爬已同时接入主场景与 `ExplorerThirdPerson`。在水面朝浮板前进即可自动抓边；脚踩到水下楼梯后切回站立，可直接走出或跳跃。可复用楼梯在 `Assets/Prefabs/Architecture/Staircase.prefab`，参数及当前验证见 `Documentation/TraversalV2/README.md`。

## 调整外观与摆放

移动、旋转和正值缩放请操作 prefab 的**最外层根节点**。轨道、发射点、接收器、碰撞与内部引用都随根变换。非均匀缩放已纳入运行检查。不要使用 0 或负缩放，也不要将另一层带非均匀缩放的父物体再倾斜形成剪切碰撞体。

换美术时优先在 `Replaceable…` / `…art` / `…face` 原子物体上更换 Mesh、Material；保留带功能组件的根、`Beam origin +Z`、轨道及运动平台的 Rigidbody。如果删除旧对象重建，需重新绑定对应的 Visuals / Renderer / Collider 数组、indicator 等引用。浮板的物理根保留 scale=1，外观尺寸与 `displacementSize` / BoxCollider.size 一致。整套缩放用外层轨架完成。

导轨路径是局部坐标 `pathStart → pathEnd`。旋转竖轨后，浮力只在轨道允许的方向做功；水平放倒的轨道不会凭空垂直抬升。采用水面采样、排水体积、密度、重力与阻尼的浮力积分，理想导轨通过运动学刚体承载人物；暂不按玩家体重增加吃水，也不会被碰撞推离轨道。

浮板、排水闸和幻影需要场景中的 `WaterVolume`；默认自动发现，也可显式指定。水域自身用 `sizeX / sizeZ / bottom / initialLevel` 调整大小，保持水体 Transform 的旋转为 0、缩放为 1。此限制属于水平流体模拟域，不影响独立机关根节点的旋转缩放。

水中投影首次绑定水体时，会根据默认角度与真实折射交点标定一次目标；之后操作 E、排水不会让目标跟着光点走。若光源在水下、样本没浸水、投影面方向不对、光路受阻或水深不足，保留人工目标。可关闭 `MirageWaterBinding.calibrateTargetOnBind` 手工设计目标。

## 传送门

门根的局部 XY 面是开口，+Z 标记正面，默认 **Two Sided** 开启，两面都显示实时画面并允许玩家 / 激光穿过。明确要做单面门时才关闭 `LightPortal.TwoSided`。门中心为根坐标原点；可通过 Scene 的蓝色方向箭头检查朝向。门对可以放在相距很远的两个空间，玩家和激光都会按入口到出口的同一坐标变换穿过。光路总射程不把两门之间的地图距离计入。

窗口显示另一端实时透视，出口裁剪方向随观察面改变。默认递归深度 2、最高 4，只有视锥内窗口渲染，图像最大边长 1024；递归和分辨率在 `PortalSurface` 中调整。先保证每个直接可见窗口当帧更新，再将 12 次总请求目标中的剩余次数用于嵌套窗口；可见窗口超过 12 个时保留它们的第一层更新、暂停额外递归。因此常规上限为 `max(12, 直接可见窗口数)`，不会用静止旧图假装实时画面。未渲染的深层窗口使用封底。Observer 留空时使用带 MainCamera 标签的相机，也可显式拖入相机。单观察相机，当前不支持 XR / 分屏，运行画面以 Game 摄像机为准。普通刚体加 `PortalTraveller` 即可参与穿越；玩家自动注册。

第三人称红 Bot 在跨面期间使用互补裁剪的入口身体与出口影像，影像同步当前骨骼姿势，离开门后恢复原材质。影像只含渲染组件，不复制玩家脚本或碰撞。支持当前不透明 URP/Lit 材质；自定义透明材质需另写对应裁剪版本。

2026-09-20 修复远处背面黑屏、旧画面滞留、近裁剪空白和单面穿越；主地图、实验场及门户相关 prefab 已更新。复现、原生画面和验证记录见 [PortalContinuity](../PortalContinuity/README.md)。

`PortalPair` 共同父根宜保持单位或均匀缩放；独立调整两门时操作各自 Portal1 / Portal2 根，避免父子变换产生剪切。不同尺寸的门会映射位置、方向、速度和视图，但不会改变人物或刚体自身的体型；要求原比例无缝行走时使用两端相同尺寸。

现有玩家继续使用世界 Y 方向重力，斜门 / 地板门会转换运动速度和视线，但不会让人物长期横着站在墙面上。传送门应放在实际留出的洞口；不会自动挖穿现有墙体碰撞。

## 与旧教学关卡的关系

`CoastalTemple.unity` 的教学布局保留。主地图及 V9 教学预制体中，两条光桥的受光器和对应固定光源均改为蓝色，第三关同一条穿门光线驱动的移石桥受光器同步为蓝色；第二关共用红色光源的升 / 降受光器都保持红色。新红墙使用 `ByLightColor` 模式；旧教学石桥的 `Permanent` / `WhileIlluminated` 规则保留。

源码位于 `Assets/Scripts/Gameplay/Light`、`Mechanisms`、`Portals`。日常切换场景使用 **Coastal Temple > Scenes > 主地图 CoastalTemple / 机关实验场 MechanismPlayground**，或在 Project 的 `Assets/Scenes` 中双击对应 `.unity` 文件。先停止 Play，修改后 Ctrl+S 保存；切换菜单会询问是否保存未保存的修改。

`Coastal Temple > Mechanisms > Rebuild showroom (overwrites layout)` 是重新生成实验场布局，和打开场景分开，并有覆盖确认。重建 prefab 会覆盖标准资源的手工修改，日常换美术请创建 prefab Variant。

## 原理参考与验证

- 用户指定：[How do non-euclidean games work? | Bitwise](https://www.youtube.com/watch?v=lFEIUcXCEvI)。实现采用空间入口间的坐标连接与实时窗口，不引入全局弯曲空间模拟。此次能取得视频的标题与简介，未获取完整字幕。
- [Unity 6 URP 自定义渲染请求](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/User-Render-Requests.html)：当前采用 `SingleCameraRequest`。
- [Sebastian Lague 的 Portals 项目](https://github.com/SebLague/Portals)：用于核对门户坐标映射、递归窗口与裁剪思路；本项目运行代码为独立实现。

验证报告：`Documentation/CoastalTemple/MechanismPrefabChecks.json`、`MechanismPrefabAssetChecks.json`、`MechanismLightChecks.json`；本目录还有门户变换和真实渲染检查。保存资产的失效 / 外部对象引用均为 0。验证使用 Unity 6000.5.9f1 编辑器，未重新打包 Windows 版本。

2026-09-20 颜色版本更新后的验证见 [ColorMechanisms](../ColorMechanisms/README.md)：15 个真实预制体、35 项运行检查通过，另包含主地图光路与实验场实际 E 切色验证。
