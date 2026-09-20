# 红色 Bot 与新动画

当前主场景使用 `Assets/Prefabs/Player/RedBot.prefab`。完整人物取自用户提供的 `Assets/Animations/Character/X Bot@Slow Run.fbx`，包含 `Beta_Surface` 和 `Beta_Joints` 两个蒙皮网格；全部七份 FBX 有相同的 65 个骨骼关节。其他六份动画使用这份模型的 Humanoid Avatar，无需再下载皮肤。

| 操作 | 新动画 |
| --- | --- |
| 停止移动 | Idle，循环 |
| WASD | Slow Run，循环 |
| WASD + Shift | Fast Run，循环 |
| 空格跳跃 | Jump，非循环 |
| 拾取提灯后移动 | Lifting 的右臂举起姿态叠加在移动动作上 |
| 水中停留 / 游动 | Treading Water / Swimming，循环 |

动画控制器是 `Assets/Animations/Player/RedBot.controller`。跳跃使用源动画第 21–45 帧的起跳到落地片段，使动作跟上角色按空格后立即起跳的物理时机；原始 FBX 文件未改动。角色位移和跳跃高度仍由 `CourtyardWalker` 负责，Animator 不应用根运动。

Lift 的 1.4 秒处为稳定举起姿势，生成 `Assets/Animations/Player/LiftHold.anim`，并调整上臂与肘部弯曲以补偿跑步时的身体前倾。动画蒙版只覆盖右臂和右手手指，因此左臂与腿继续播放慢跑 / 快跑。提灯及光源绑定右手，游泳时淡出举灯层，避免破坏划水动作。独立运行组件为 `Assets/Scripts/Gameplay/Player/PlayerCarryAnimation.cs`。

水中停留和游动使用后续加入的 `X Bot@Treading Water.fbx`、`X Bot@Swimming.fbx`。旧人偶与第一人称手臂已从当前玩家实例移除，历史场景和旧源资产保留。

## 贴图与文件位置

用户提供的 Slow Run.zip 内只有 Slow Run.dae，没有 texture 文件夹。FBX 和 DAE 均未引用外部贴图；红色来自原始材质颜色。`RedBot_Surface.mat` 和 `RedBot_Joints.mat` 使用这些颜色并适配 URP，因此不存在缺贴图问题。

DAE 已解压到 `SourceAssets/Characters/RedBot/Slow Run.dae`，逐字节校验后清理 Assets 里的 ZIP。七个原始 FBX 保持内容不变。详见 [原文件审查](SourceInventory.json)。

重新应用整套导入设置、预制体和场景绑定的入口是 `Coastal Temple > Player > Apply red bot and supplied animations`，对应 `Assets/Scripts/Editor/Gameplay/RedBotAuthoring.cs`。旧 Quaternius 配置菜单移至 `Legacy Quaternius`，避免与当前玩家配置混淆。

替换前场景保存在 `Assets/Scenes/Archive/CoastalTemple_BeforeRedBot.unity`。第三人称、近身交互和水体机关继续沿用现有实现。

## 验证材料

Unity 6000.5.9f1 编辑器中，24 项导入与绑定检查、36 项实际运行检查全部通过。主场景保存后确认只有一个红色 Bot，缺失脚本和失效引用均为 0，临时测试对象为 0。慢跑 / 快跑举灯时手部持续位于胸前；游泳朝向、入水后的举灯层淡出、跳跃落地及摄像机绕转均已检查。此轮没有重新打包 Windows 版本。

- [导入和绑定检查](Assets-After.txt)
- [实际运行检查](PlayChecks.txt)
- [保存后的场景检查](Scene-After.txt)
- [模型正面](01-Idle-Front.png)
- [慢跑](02-SlowRun-Front.png)、[快跑](03-FastRun-Front.png)、[跳跃](04-Jump-Front.png)
- [慢跑提灯](05A-SlowRunLift-Front.png)、[快跑提灯](05B-FastRunLift-Front.png)、[原地踩水](06-Swimming-Game.png)、[向前游泳](07-ForwardSwimming-Game.png)
- 运行检查使用的灰色地面与观察相机均为临时对象，不保存在关卡。

编辑器的 Package Manager 窗口仍报告已有的 `path argument ... undefined` 错误；当前角色与动画运行期间没有新增游戏脚本异常。
