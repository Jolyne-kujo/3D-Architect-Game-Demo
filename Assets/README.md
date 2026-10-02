# Assets 资源目录

打开 `Scenes/CoastalTemple.unity` 开始开发。所有项目代码都在 **Scripts**，不再分散在资源包目录里。

切换场景：停止 Play 后，用 `Coastal Temple > Scenes > 主地图 CoastalTemple / 机关实验场 MechanismPlayground`，也可以直接双击 `Scenes` 中的对应文件。编辑后 Ctrl+S 保存。Scenes 菜单只打开现有场景，不重建地图。

当前默认第一人称：镜头绑定角色眼部，视野中隐藏身体和双手，仅拾取提灯后显示灯。可复用玩家在 `Prefabs/Player/ExplorerFirstPerson.prefab`。操作机关会切换到关卡俯视观察，继续按 E 操作同一机关，WASD / 空格 / Esc 返回第一人称。

主地图起点的广场整套预制体在 `Prefabs/地形建筑/关卡片段/广场_新手关.prefab`；场景中展开 `20_Architecture > 01_广场新手关`，可分别修改原模型、机关以及出生点。原 FBX 在 `Objects/Environment/广场.fbx`。

| 一级目录 | 放什么 |
| --- | --- |
| `Animations` | 动作 FBX、动画片段、Animator Controller；玩家动作在 `Player` |
| `Materials` | 材质，按环境、玩家、水体、机关分类 |
| `Objects` | 人物和环境源模型、派生网格、Terrain 数据及图层 |
| `Prefabs` | 可拖入场景复用的完整对象：建筑、玩家、红石幕、水体、教学机关 |
| `Scenes` | 正式关卡、独立实验场景；历史备份在 `Archive` |
| `Scripts` | 运行代码、编辑器工具和检查代码 |
| `Settings` | 渲染管线、灯光后处理、输入配置 |
| `Shaders` | 水面和海面着色器 |
| `Textures` | 图片和程序生成纹理，包括法线与地形颜色 |

## 常用代码在哪里

| 内容 | 位置 |
| --- | --- |
| 摄像机、人物动画、身体定位、举灯叠加 | `Scripts/Gameplay/Player` |
| 移动、跳跃惯性、游泳控制 | `Scripts/WaterDemo/Runtime/CourtyardWalker.cs` |
| 激光、红石幕、光线空间连接 | `Scripts/Gameplay/Light` |
| 升降平台、光桥、排水、虚影实体 | `Scripts/Gameplay/Mechanisms` |
| 教学关卡和提示界面 | `Scripts/Gameplay/Tutorial` |
| 独立水体模拟与浮力 | `Scripts/WaterSystem` |
| 编辑器建模、组装和动作绑定工具 | `Scripts/Editor/Gameplay` |
| 运行验证工具（仅编辑器生效） | `Scripts/Gameplay/Diagnostics` |

`WaterDemo` 保留自己的程序集，避免整理目录时破坏独立水系统和实验场景的依赖。找玩家移动参数时可直接搜索 `CourtyardWalker`。

## 玩家动作和预制体

- 当前角色：`Prefabs/Player/RedBot.prefab`，红色 X Bot，模型来自带皮肤的慢跑 FBX。
- 新动作原件统一在 `Animations/Character`：`X Bot@Idle.fbx`、`X Bot@Slow Run.fbx`、`X Bot@Fast Run.fbx`、`X Bot@Jump.fbx`、`X Bot@Lifting.fbx`、`X Bot@Swimming.fbx`、`X Bot@Treading Water.fbx`。
- 当前动画控制器：`Animations/Player/RedBot.controller`。
- 举灯保持姿态：`Animations/Player/LiftHold.anim`；右臂独立叠加，由 `Scripts/Gameplay/Player/PlayerCarryAnimation.cs` 控制。
- 红色外壳与关节材质：`Materials/Player/RedBot_Surface.mat`、`RedBot_Joints.mat`。
- 新版机关：`Prefabs/Mechanisms`；试验场：`Scenes/MechanismPlayground.unity`；使用说明：项目根目录 `Documentation/Mechanisms/README.md`。
- 旧版红石幕：`Prefabs/Light`。旧 Quaternius 模型、手臂和控制器用于历史场景，不再出现在当前玩家身上。

普通移动使用慢跑，按住 Shift 使用快跑，空格跳跃，停止时播放新待机循环。新控制器保留游泳，拾取提灯后在移动动作上叠加右臂举灯姿态。导入动作的轨迹不会移动角色；位移仍由角色控制器负责。

重新导入与绑定入口：`Coastal Temple > Player > Apply red bot and supplied animations`。原待机、跑步和跳跃直接使用红色 Bot 的骨架；游泳与踩水使用同目录的新 Bot 动画。不要通过移动人物根节点补偿动画朝向。

压缩包中的 DAE 原件已解压至项目根目录的 `SourceAssets/Characters/RedBot`；之前的下载原件在 `SourceAssets/Characters/Mixamo`。新 ZIP 没有 texture 文件夹，模型使用原文件中的红色材质，不依赖图片。Unity 只导入 FBX，ZIP 已在校验解压结果后清理。第三方模型的来源和许可文件与源模型一起保留。

新增资源按用途放入这些目录；在 Unity 内移动现有资源，提交时一并提交 `.meta`。有实际运行时加载需求时再创建 `Resources`，不要为空目录预留占位文件。
