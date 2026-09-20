# 动作绑定与 Assets 整理

主场景：`Assets/Scenes/CoastalTemple.unity`。日常导航见 [Assets 目录说明](../../Assets/README.md)。

## 动作

使用用户提供的 `X Bot@Slow Run.fbx`、`X Bot@Fast Run.fbx`，整理为 `Assets/Animations/Player/Locomotion/SlowRun.fbx` 和 `FastRun.fbx`，原资源 GUID 保持不变。

两份动画已导入为 Humanoid 循环动作，重定向到现有 Quaternius 人偶和第一人称手臂。普通移动 3.4 m/s 使用慢跑，Shift 5.3 m/s 使用快跑。原有待机、跳跃、落地、游泳、放松手指及持灯层继续保留。

导入设置中的 180° 旋转偏移补偿 Mixamo 与现有人偶原动画的正面差异。原动画的水平轨迹被提取为根运动，目标 Animator 关闭根运动应用，保证角色位移只由碰撞控制器处理；不能把前进轨迹直接烘进骨骼姿态，否则模型可能在胶囊体里跑出去。配置依据 [Unity Root Motion 文档](https://docs.unity3d.com/6000.0/Documentation/Manual/RootMotion.html)，实际结果另外通过连续动作循环检查。

动作导入与重绑入口：`Scripts/Editor/Gameplay/PlayerLocomotionAuthoring.cs`，菜单 `Coastal Temple > Player > Apply imported slow and fast run`。原来的预制体生成工具也会应用这两个动作，避免重新生成后退回旧动画。

## 整理方式

316 个文件通过 Unity AssetDatabase 移动，保留全部被保留资源的 GUID。一级目录统一为九类：Animations、Materials、Objects、Prefabs、Scenes、Scripts、Settings、Shaders、Textures。

同步更新生成工具、测试工程、关卡挖洞恢复清单、构建场景列表和文档路径。独立水系统的程序集边界保留，所有 `.cs` 和程序集定义均在 Scripts 下。原来的海岸、水庭院、第三方根目录移动后已清除空壳。

两个 ZIP 内的 DAE 解压到 `SourceAssets/Characters/Mixamo`，校验通过后删除 ZIP。空 Resources 占位目录和没有其他资源依赖的 Unity 模板欢迎页已删除。模型原件、许可说明、历史场景及用户已有材质均保留。

修改前场景：`Assets/Scenes/Archive/CoastalTemple_V14_BeforeAssetOrganization.unity`。所有旧场景依然使用原有 GUID 引用，因此能随资源移动继续找到依赖。

## 验证记录

最终检查：动作资产 14/14、实际动作播放 15/15、阴影 27/27、第一人称回归 35/35 通过。主场景及 16 个预制体共检查 2745 个组件，没有丢失脚本或组件引用，见 [场景检查](SceneValidation.json)。测试用临时对象已移除，编辑器停在保存后的主场景。

资源迁移检查 908/908 通过。主场景保存时，Unity 更新了 74 处 ProBuilder 内部版本计数；整理前备份的哈希与原始记录一致，排除这些数字后整个场景文本完全相同，布局和对象配置未变。

- [动作资产检查](LocomotionAssets.txt)：两个 Humanoid、慢跑/快跑绑定与原状态保留。
- [实际运行检查](LocomotionPlay.txt)：身体与手臂实际播放的动画、骨骼动作、循环时身体位置、朝向及停止后的待机。
- [资源迁移检查](AssetIntegrity.txt)：资源 GUID、导入资产内容、目录规则、压缩包清理和新增悬空引用。
- [阴影专项](ShadowVerification/ShadowChecks.txt)：摄像机俯仰、360° 原地旋转和真实投影。
- [第一人称回归](FirstPersonVerification/FirstPersonRegression.txt)：贴墙、游泳、下潜、持灯和摄像机绑定。
- [慢跑预览](AnimationPreview/01-SlowRun-A-Body.png)、[快跑预览](AnimationPreview/03-FastRun-A-Body.png)。预览中的灰色地面是临时验证场地，不保存在主场景。

`Tests/Water/WaterChecks.csproj` 的路径已更新，水体数值回归 29/29 通过。

资源扫描另记录了原有两份渲染配置中的 11 条旧包引用，见 [记录](PreexistingPackageReferences.json)。这两份文件与迁移前的 SHA-256 一致；这些引用并非移动导致。迁移没有新增丢失的 GUID。

`MigrationPlan.json`、`Before.json`、`MigrationLog.txt` 是此次迁移的固定审计记录。`Tools/Project/prepare_asset_layout.py` 和 `stage_asset_path_updates.py` 是一次性准备工具；`verify_asset_layout.py` 用于核查此次快照，不作为限制后续正常美术修改的持续测试。
