# 单色受光器、蓝色光桥与发生器指示灯

2026-09-20，Unity 6000.5.9f1。主地图和实验场、独立机关 prefab 及 V9 教学 prefab 已保存更新。

## 拖入即可用

目录：`Assets/Prefabs/Mechanisms`。

- `LightBridge.prefab`：一整套蓝色受光器、光桥、碰撞与驱动绑定。只有蓝光可以开启。直接复制、移动、旋转及正值缩放整个根节点，接收器与桥面仍属于自己的那一套。
- `RedLightReceiver.prefab`、`YellowLightReceiver.prefab`、`BlueLightReceiver.prefab`：独立受光器。选中内部带 `LightReceiver` 的节点，在 Inspector 的 `Required Color` 单选颜色；外观会随颜色显示。`Activated` 和 `Deactivated` 事件可绑定其他机关效果。
- `LightReceiver.prefab`：保留旧路径，默认改为蓝色。所有受光器都按相同颜色匹配，不再接受任意颜色。
- `LightDrivenLift.prefab`：红光升、黄光降。`DrainGate.prefab`：黄色受光器接受黄光开闸。手动控制入口继续可用。
- `LaserDevice.prefab`：背面上方大圆灯为 NOW 当前光色，下方小圆灯为 NEXT 下一次 E 的光色。关闭时暗色。E 顺序仍是黄 → 蓝 → 红 → 关闭；`colorCycle` 可按实例修改。显示和实际切色使用同一数据源，缩放旋转不会改变含义。

错色光不会积累充能、不会触发 `Activated`，并会中断尚未完成的同色充能。多束光同时照射时，只要其中确实有同色光即可充能。受光器本身的延迟、离光宽限与锁定参数保留；光桥和光驱动平台另外要求当前仍有匹配的光，不能靠宽限期或错色光维持供能。光桥还明确只允许蓝色受光器，不能将其改红后用红光开启。

颜色反馈由 `LightTargetFeedback` 按受光器的身份颜色显示；错误颜色不会被当作匹配受光反馈。指示灯使用共享材质与 MaterialPropertyBlock，没有增加实时点光源，也不为每次切色新建材质。

## 主地图兼容

保留关卡布局并修改现有实例。第一关 SunLens、第三关 Fixed_Sun 改为蓝光，两条光桥受光器也为蓝色；第三关共用穿门光源的 StoneDrive_Receiver 同步为蓝色。第二关原有单束红光通过调整方向分别驱动升 / 降，因此这两个旧关卡受光器都为红色。独立新版升降台则使用红、黄两个受光器。

原有红墙 prefab 的红光永久消失、黄光临时消失、蓝光无效规则没有改变；旧教学中的颜色无关石幕模式仍保留。

## 切换场景

1. 停止 Play，在编辑模式修改和保存。
2. 菜单 `Coastal Temple > Scenes > 主地图 CoastalTemple` 打开 `Assets/Scenes/CoastalTemple.unity`。
3. 菜单 `Coastal Temple > Scenes > 机关实验场 MechanismPlayground` 打开 `Assets/Scenes/MechanismPlayground.unity`。
4. 也可直接在 Project 的 `Assets/Scenes` 双击对应场景。Ctrl+S 保存当前场景；切换菜单会处理未保存修改的保存提示。

这是打开两个已存在的场景，不是把一个地图删除后重新生成。旧的实验场生成菜单改名为 `Rebuild showroom (overwrites layout)` 并加入覆盖确认，日常切换无需使用它。

## 验证与范围

- `Baseline.json`：修改前，标准、旋转、非均匀缩放的光桥都被红光开启，3 项预期失败。
- `PrefabChecks.json`：35/35 通过；15 个资产的拖入和复制绑定、三色各自匹配、错色中断充能、E 全循环双灯、蓝光桥排除红 / 黄及错色维持、旋转缩放后的机关功能。
- `MainSceneLightChecks.txt`：7 项主地图实际光路检查通过，包含第一关借光开桥、第二关红色升降接收器、第三关穿门蓝光驱动、低光路仍被中间墩挡住、抬高后到达远端蓝色受光器。此检查改变测试运行中的导光朝向和载架高度来核验光路，不是重新完整步行通关。
- `ShowroomChecks.txt`：真实实验场 E 第一次黄光不开桥，第二次蓝光产生实体桥，第三次红光立即撤桥。
- `ComponentChecks.txt`：31 项机关组件及 11 项升降驱动检查通过。原有红墙与光源 12 项检查也通过，报告在 `Documentation/CoastalTemple/MechanismLightChecks.json`。
- `SavedAssetChecks.txt`：最终保存的组件、圆灯绑定和编辑器菜单核对。
- `01-YellowBridgeOff.png`、`02-BlueBridgeOn.png`、`03-CurrentBlueNextRed.png`、`04-ColoredLiftReceivers.png`：Unity 实际渲染。

本次验证在 Unity 编辑器完成，未重新生成 Windows 试玩包。编辑器原有 Package Manager 的 path 提示及字体引用提示仍可见；本次脚本已编译，上述运行与渲染检查通过。
