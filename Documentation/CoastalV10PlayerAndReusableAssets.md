# 下载人偶、角色摄像机与可复用石幕

> 这是 V10 历史记录。当前玩家摄像机已改为固定眼部的纯第一人称，屏幕只显示手臂，C / Tab / F1–F3 不再切换游戏视角。当前设置和操作以 [V11 第一人称说明](CoastalV11FirstPerson.md) 为准；下面的第三人称、右手持灯及相关检查只记录当时的实现。

修改已保存到 `Assets/Scenes/CoastalTemple.unity`。修改前的 V9 场景保留在 `Assets/Scenes/Archive/CoastalTemple_V9.unity`。

## 角色和摄像机

当前角色采用作者 Quaternius 发布的 [Universal Animation Library Standard](https://opengameart.org/content/universal-animation-library) 中的骨骼人偶，CC0 授权。模型、蒙皮、骨骼和动作均来自下载资源；项目只配置导入、材质、动画状态和玩家控制器的连接。

- 原始资源：`Assets/Objects/Characters/Quaternius`，包含原作者许可与来源记录。
- 人偶预制体：`Assets/Prefabs/Player/QuaterniusMannequin.prefab`。
- 动画控制器：同目录的 `MannequinLocomotion.controller`，使用待机、行走、慢跑、空中、浮水和前进游泳六段原始动画。
- 显示模型约 1.8 米高，使用象牙白材质；53 根蒙皮骨骼，8462 个导入顶点，单个 SkinnedMeshRenderer。
- 移动仍由 `CourtyardWalker` 和 CharacterController 负责。`RiggedPlayerAnimation` 只把实际速度、着地和游泳状态传给 Animator，并修正朝向；关闭 Root Motion，避免动画与控制器重复移动角色。
- 已拾取提灯的外观挂到新人偶的右手骨骼，照明与持有状态仍由独立 `PlayerLantern` 管理。替换角色时会先保留场景附件，再重新绑定，避免连同旧身体一起移除。

层级路径为 `50_Player_And_Cameras → Player_ShoreStart → CameraFollow_稳定跟随点 → Main Camera`。跟随点绑定角色根，不绑定会摆动的头部骨骼。拖动角色会带动摄像机；C 切换第一 / 第三人称，第一人称隐藏自身模型；Tab 或 F1–F3 进入全景时暂时解绑，返回行走后自动重新绑定。

旧的拼接角色仅保留在历史归档与旧 authoring 源码中，当前场景不使用它的程序摆动骨骼。

## 这些场景对象为什么移动后没有效果

| 对象 | 实际作用 | 应该改哪里 |
|---|---|---|
| `60_Lighting / Sun` | 全局方向光，模拟无限远太阳；原来位置是世界原点，并不是刻意放在山后 | 旋转决定光向和阴影；Light 的 Intensity / Color 决定强度和颜色。平移无效 |
| `60_Lighting / Courtyard tone` | 全局 Volume，使用 `CoastalTone.asset` 中的 Tonemapping 和 ColorAdjustments | 编辑 Profile 的色调映射、曝光、对比度、饱和度等；Is Global 开启时位置无效，不存在区块交界 |
| `11_CoastalLandmarks / Halfway_JoinedArea` | 半山腰规划定位点，原坐标 `(-3, 41, 121)` | 只移动标记，不移动半山腰模型 |
| `11_CoastalLandmarks / HiddenBeach_LevelArea` | 隐藏海滩关卡规划点，原坐标 `(58, 0, 155)` | 只移动标记，不移动沙滩或海水 |
| `11_CoastalLandmarks / Summit_Point` | 山顶规划定位点，原坐标 `(57, 75, 148)` | 只移动标记，不移动山顶或神庙 |
| `View_01…View_03` | 全景摄影机预设 | 改位置和朝向后，再按对应 F1–F3 取景才会应用 |
| `CompletionPoint` / `RecoveryPoint` | 关卡实际使用的到达检测点 / 失足恢复落点 | 移动会影响关卡判断或回点，不能当无用标记删除 |
| `Summit_CompactPoint` | 真正带 MeshRenderer 与 MeshCollider 的山顶几何对象 | 与空的 `Summit_Point` 不同，修改它会改变实际山体 |

已确认三个 CoastalLandmarks 没有运行时组件引用；该组标为 `EditorOnly`，不会进入构建。选中对象可看中文说明，Scene 视图开启 Gizmos 可见规划范围及标签。Sun 与全局色调图标已整理到岸边附近方便选择，旋转、亮度和色调参数保持原设置。

## 石幕直接复制使用

在 Project 中打开 `Assets/Prefabs/Light`，把以下预制体拖入场景：

- **`RedStone_Permanent.prefab`**：单条金色标记，连续受关卡光束照射 0.35 秒后消失；本次运行中移光不恢复。
- **`RedStone_WhileLit.prefab`**：双条金色标记，受关卡光束照射后消失；失光超过 0.15 秒恢复。内部有角色或刚体时等待其离开再恢复碰撞。

两者都把 `RedStoneCurtain` 行为、显示模型、光学检测形状和物理碰撞绑在同一预制体内。拖入后可以 Ctrl+D 复制，移动、绕轴旋转和缩放；每个实例拥有独立状态，无需注册到教学关卡管理器。主场景现有红墙也可以直接复制，功能组件已经在红墙自身上。

`Mode` 决定永久或可恢复，`ContinuousHitSeconds` 调永久石幕照射时长，`ReturnGraceSeconds` 调恢复延迟。根节点脚底为零高度，默认宽 4 米、高 3 米、厚 0.45 米。

石幕响应的是本项目 **LaserEmitter 关卡光束**，不是 Sun、普通 Point Light 或提灯照明。在已有关卡光源前摆放即可实验；新场景需要自行放置 LaserEmitter 光源，但不需要 TutorialJourney 或额外 LightPuzzleWorld 对象，组件会自动注册。实体遮挡仍会挡光。

后续换美术时替换 `Replaceable_Art` 子对象，保留根上的 RedStoneCurtain 和 BoxCollider，并在 `Visuals` 数组中指定新的 Renderer；`PhysicalCollider / OpticalCollider` 使用根上的 BoxCollider。新的模型尺寸不同时同步调整碰撞大小，确保显示、承重和受光范围一致。

## 本次验证范围

- [资源与摄像机绑定](CoastalV10Verification/AssetChecks.txt)：14 项通过，包含实际 Humanoid、骨骼、预制体内部引用与摄像机父子关系。修改前的预期失败记录也已保留。
- [实际光路与复制](CoastalV10Verification/CurtainPhysicsChecks.txt)：15 项通过，包含旋转缩放、遮挡、复制后独立状态、永久 / 恢复差异和占位保护。
- [角色骨骼与镜头](CoastalV10Verification/PlayerAnimationChecks.txt)：15 项通过；在临时平面上检查真实角色移动、骨骼动作和跳跃，在原水池检查实际浮水及前进游泳，并验证 C / 全景切换对应的视角行为。
- [提灯回归检查](CoastalV10Verification/LanternChecks.json)：5 项通过；右手骨骼挂点、未拾取隐藏、拾取显示、关闭和重新开启照明。

以上为 Unity Play 模式自动检查和渲染画面核验，没有注入实体键盘事件，不等同于完整人工试玩。本次修改不重新设计 V9 关卡；之前的 V9 完整路线报告保留其原验证范围。
