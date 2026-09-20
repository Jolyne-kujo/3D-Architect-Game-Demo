# 池边排水与玩家调试激光

在 `Assets/Scenes/MechanismPlayground.unity` 进入 Play。日常切换仍使用 **Coastal Temple > Scenes > 机关实验场 MechanismPlayground / 主地图 CoastalTemple**，不要用重建场景菜单。

## 怎么测试

1. 到水池前沿岸边，找到写着 **DRAIN / REFILL** 的控制台。靠近按 **E** 开始排水，再按 **E** 立即补满水池并关闭排水口。竖直浮板随实际水位下降，补水后靠浮力回升；横向浮板也在自身竖向滑轨范围内跟随水面。
2. 按 **F6** 开启玩家调试激光，右侧会显示当前颜色和中心准星。按 **F7** 循环红 → 黄 → 蓝，按住**鼠标右键**向准星发射，松开停止，再按 F6 关闭。默认关闭，不会进场就用红光永久消除石幕。
3. 场内 `LightDrivenLift` 的**红色受光器 + 红光**向上驱动，**黄色受光器 + 黄光**向下驱动；错色、移开或遮挡均停止。若用过 E 手动操纵升降台，先用手柄切到停止，避免手动输入仍在驱动它。
4. 蓝光照蓝色受光器开启光桥。黄光还可远程照池边控制台的黄色受光器，连续照射 1.5 秒开闸；补水使用 E，补水时先移开黄光。

水面投影的入射段和水下折射段均已改为蓝色，保存到场景及相关预制体；排水使样本露出水面时，折射与实体幻影台阶会消失。

浮力板 `GuidedBuoyantPlatform` 与光控升降台 `LightDrivenLift` 是两类不同装置：前者用水位、排水体积、密度、重力和阻尼计算受导轨约束的浮力；后者由受光器驱动导轨电机。不要把两个运动脚本同时加到同一浮板刚体上。

## 修改和复用

- `Assets/Prefabs/Mechanisms/PoolsideDrainConsole.prefab`：可直接复制完整控制台；放到其他池边后，将该场景的 `WaterVolume` 拖到 `DrainGateDevice.water`。控制台位于池外，无法靠“所在水域”自动推断绑定关系。
- 实验池对象 `Showroom water - use dimensions to resize` 的 **Drain Speed Multiplier** 为 4；可在 `WaterVolume` Inspector 调节，当前仍使用实际体积排出计算，不直接移动水面贴图。
- `PlayerDebugLaser` 独立组件位于 `Assets/Scripts/Gameplay/Diagnostics`，已绑定到主地图、实验场及 `ExplorerThirdPerson.prefab`。支持 `SetDebugEnabled`、`SelectColor`、`CycleColor`、`SetFiring`；更换输入系统时关闭 `readInput` 后调用这些接口。
- 调试激光正常只在 Editor / Development Build 中可用；正式包默认不启用。确需公开时可勾选组件的 `allowInReleaseBuild`。
- 光线沿最终摄像机朝向追踪，受实际碰撞遮挡，并使用原有受光器和传送门逻辑；切到总览或释放角色控制后停止发射。

## 本次验证

Unity 6000.5.9f1 编辑器 Play 模式的场景自动检查见 [RuntimeChecks.txt](RuntimeChecks.txt)：21 项通过。排水 22 秒，平均水位 -0.30 → -3.22 米，池水体积 1476.3 → 313.1 立方米，竖轨浮板中心高度 -0.20 → -3.30 米；补水稳定后回到 -0.20 米。另验证普通 E 交互、错色、遮挡、松开即停和实际红 / 黄光升降。

[InputChecks.txt](InputChecks.txt) 的 4 项检查通过：使用 Unity Input System 注入键盘和鼠标事件，分真实帧执行 F6、F7、右键及释放，不是完整人工键鼠试玩。原有 10 项水机关检查通过；16 个机关预制体的 36 项回归检查通过，报告见 `Documentation/CoastalTemple/MechanismPrefabChecks.json`。保存资产检查另见 [SavedAssetChecks.txt](SavedAssetChecks.txt)。本次未重新打包 Windows 版本。

原生 Unity 渲染截图：[满水](01-PoolFull.png)、[排水后](02-PoolDrained.png)、[蓝色折射](03-BlueRefraction.png)、[调试激光界面](04-PlayerDebugHud.png)。
