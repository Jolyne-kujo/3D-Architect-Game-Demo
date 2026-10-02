# 第一人称与机关观察视角

主地图 `Assets/Scenes/CoastalTemple.unity` 和实验场景 `Assets/Scenes/MechanismPlayground.unity` 已保存此配置。镜头绑定角色固定的 Eye；俯仰只转动 Eye，左右观察在原地转身。完整角色保留动画和世界阴影，镜头内不显示身体或手臂。

获得提灯后，右下角只显示灯。`LanternViewAnchor` 控制位置和角度，`LanternOnlyOverlay` 只渲染灯的层，未拾取时关闭这一额外渲染。L 切换照明；灯使用一个有两个材质的低面数网格，场景点光源仍负责实际照明。

E 操纵机关后，摄像机升高到对应解谜区的俯视位置，角色输入暂停；继续按 E 操作同一个机关。WASD、空格或 Esc 返回第一人称。拾取提灯不会触发俯视。机关仍使用玩家 Eye 检查距离和遮挡，俯视摄像机不能隔墙远程操作。

新的可复用玩家预制体是 `Assets/Prefabs/Player/ExplorerFirstPerson.prefab`。它自动发现场景中的水体，保留原角色动画、游泳和攀爬组件。

后续搭建时，在关卡父对象添加 `MechanismObservationArea`，调整 `Local Center`、`Local Size` 和 `Yaw`；选中后可看到线框范围。这个范围内的机关共用取景，摄像机会按屏幕比例计算距离，使整块范围进入画面。没有指定范围的机关使用局部观察范围。玩家上的 `CoastalPlayerCamera` 可调 `Mechanism Pitch`、`Mechanism Field Of View` 和 `Fallback Observation Radius`。单个机关取消 `Observe While Operating` 可关闭自动俯视。

已验证：第一人称/镜头位置/提灯/机关观察的 14 项检查、原相机回归的 12 项检查、40 项交互回归，以及主地图和实验场景各 16 项 Play 检查。Play 检查通过临时输入配置接收无窗口焦点的测试键盘事件，结束后恢复原配置；它不会修改项目输入设置。`Main/`、`Lab/` 保存实际游戏截图和检查记录，`PrefabChecks.txt` 验证玩家预制体的持久引用。

修改前的两个场景和核心脚本保存在 `Logs/FirstPerson20261002/`。实验场景原先未保存的布局也包含在备份和本次保存的场景中。
