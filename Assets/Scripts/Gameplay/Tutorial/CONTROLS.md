# 独立交互、提灯与教学观察

- WASD 慢跑，Shift 快跑；空格跳跃 / 水中上浮；Ctrl 下潜。
- E 由 `CoastalTemple.Interaction.PlayerInteractor` 处理，操作最近且可达的对象。交互从 `interactionOrigin`、角色眼部或组件原点发出，到目标 `interactionPoint`；默认范围 2.8 米。真实距离、实体墙、玩家行动许可、全景模式统一控制输入与直接调用。忽略玩家和目标自身碰撞、忽略触发器；64 命中缓冲填满时查询完整结果。
- L 由 `CoastalTemple.Player.PlayerLantern` 处理。拾取后可反复开关照明；提灯不发出机关激光。`pickedUp` 仅在首次获得时触发。该组件文件位于 `Scripts/Gameplay/Interaction`，不依赖教学组件。
- H 由可选 `TutorialInput` 读取观察线索。当前默认第三人称，鼠标环绕观察，WASD 按镜头朝向移动并转身；关闭全景切换快捷键，Home 回到起点。

## 独立调用

`PlayerInteractor.CanInteract(target)`、`Interact(target)`、`TryInteract()` 共用完整行动策略。`RefreshNearby()` 可在移动后立即刷新交互提示；关闭 `readKeyboardInput` 后可以由其他输入系统调用。`allowInteraction` 负责明确暂停玩家交互。没有 `CourtyardWalker` 时仍可给通用角色设置 `interactionOrigin` 并调用。

旧名 `TutorialInteractable` 保留序列化 GUID，所有机关改为 `Use(PlayerInteractor actor)`。默认 `CanUse(actor)` 只检查机关可用状态；`LanternPickup` 才检查真实提灯能力。不含进度、奖励或教学条件。继承此类的控制台可以放入完全没有 `TutorialJourney` 的场景。`AimConsole.Select(int)`、`PortalRailConsole.Select(int)` 等机制入口也可直接调用。

`PlayerLantern.GiveLantern()` 返回是否首次获得，`SetLantern(bool)` 和 `Toggle()` 控制已有物品；随身模型与 Light 由同一个组件管理。重复拾取不重复发事件，也不改变已关闭的灯。`LanternPickup` 在角色缺少此能力时不会被消耗。

## 站点、提示与奖励

`TutorialJourney.lessons` 是任意数量的 `LessonStation[]`，每项具有 `name`、`title`、`marker`、`radius`、`hints[]`、可选 `completionCondition`、`completionMessage`、`onCompleted`。半径内距离最近的站点提供提示；离开所有半径后不显示任意远处关卡标题。`H` 依序显示作者写下的提示，到最后一条后保持该条。

可选 `LessonCompletionCondition` 只读真实世界状态的 `IsComplete`。观察器在首次成立时发布反馈并调用事件；重新失光、重新通电不会重复奖励。阅读提示与靠近站点从不完成机关。需要外部实际成功事件时，将事件接到 `TutorialJourney.NotifyReward(string)`。教学没有写死红幕、升降台或站点编号的完成逻辑。

`TutorialHud` 独立负责 OnGUI、奖励展示队列和轻提示音。取消教学或 HUD 后机关、交互、提灯仍可工作。音源必须在编辑器中配置，运行时不添加组件或环境。

## 显式迁移与检查

新场景直接绑定模块。旧 authoring 在全部玩家/提灯字段赋值之后调用 `CoastalTemple.Tutorial.Editor.TutorialModuleMigration.Bind(journey)`，或菜单 **Coastal Temple → Tutorial → Bind Independent Player Modules**。此操作显式创建并保存缺失的独立组件、HUD、输入和音源，绑定提灯拾取反馈。再由作者填写 `journey.lessons`；迁移不复用旧四阶段的硬编码叙事。

`TutorialJourney` 的旧公开字段以及 `Interact/TryInteract/CanInteract/GiveLantern/SetLantern` 是兼容入口，只向绑定模块委派。没有任何 Awake 自动迁移，未绑定旧场景必须先执行编辑器迁移。保留字段用于旧 authoring 和 V8 诊断脚本编译，不再持有库存或执行物理交互。

编辑器行为验证：`CoastalTemple.Tutorial.Editor.TutorialInteractionChecks.Run()`，菜单 **Coastal Temple → Tests → Tutorial Interaction**。检查独立交互、教程禁用、实体墙和超距、64 命中饱和、观察模式、能力缺失的拾取、提灯事件、门户轨道位移以及站点半径。

独立数值行为检查：`dotnet run --project Tools/Gameplay/InteractionChecks/InteractionChecks.csproj`。覆盖距离权限、非法值、独立提灯库存、分站点提示和实际状态一次奖励。离线编译入口：`Tools/Gameplay/InteractionCompile/InteractionCompile.csproj`；编译不替代 Unity 场景物理运行验证。
