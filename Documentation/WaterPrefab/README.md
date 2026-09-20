# 可复用实验水体

入口：`Assets/Prefabs/Water/WaterVolume.prefab`。实验场 `MechanismPlayground` 中的水体已连接到这个预制体，原有角色、排水台和光路引用保留。

## 拖入使用

1. 拖入 `WaterVolume.prefab`，移动到水池位置。编辑状态已有可见水面，运行时切换为独立的高度场水位模拟。
2. 在 `WaterVolume` 组件里设置 `Size X / Size Z`、`Bottom`、`Initial Level`。底部和初始水位都是相对水体根节点的高度。默认池域 21 × 20.5 米，底部 −4 米，初始水位 −0.3 米，网格间距 0.5 米。
3. **用尺寸字段调整范围，保持 Transform 的 Rotation = 0、Scale = 1，包括父节点。** 当前求解器支持平移和水平轴对齐水域。Scene 中水域边界框可辅助对齐池壁。
4. 场景仍需池底、岸边和池壁碰撞体；水面不是实心地板。`Bed Terrain` 或 `Bed Blocks` 可描述水底高度，任意新放置的石块碰撞体不会自动刻入水底网格。

水位、尺寸可在场景实例或 Prefab Mode 中编辑；预览在编辑器刷新，也可点击“更新并保存水面预览”。改一个副本的尺寸不会修改另一个副本的网格。修改地形绘制后请手动更新预览。尺寸按 `Cell Size` 对齐；网格越密，模拟成本越高。

## 已接入的行为

| 功能 | 用法 |
| --- | --- |
| 玩家游泳 | 现有角色默认开启 `Discover Scene Water`，进入新水体自动识别；保留明确指定的水体引用和等高优先级。 |
| 导轨浮板 | 拖入 `Assets/Prefabs/Mechanisms/VerticalBuoyantPlatform.prefab` 或 `HorizontalBuoyantPlatform.prefab`，`Water` 留空时自动采样所在水体。 |
| 普通漂浮物 | 拖入同目录的 `BuoyantBlock.prefab` 即可测试：1 立方米方块、400 kg 刚体、碰撞体及 `BuoyantBody`。自制物体需要这三个组件，设置实际质量和 `Displacement Size`；水体不会自动给所有模型添加浮力。 |
| 水密度 | 水体 `Density` 默认 1000 kg/m³。浮板和 `BuoyantBody` 默认读取实际采样水体的密度；需要旧的逐物体覆盖值时关闭 `Use Water Density`。 |
| 视觉透视与折射 | 水材质已绑定。使用现有 URP 的 Opaque Texture 和 Depth Texture；当前 PC 配置开启，Mobile 配置未开启。颜色可在水材质中调整。 |
| 玩法光路折射 | 配合 `Assets/Prefabs/Mechanisms/WaterMirage.prefab`，放置其水下样本和入射光源；它自动绑定本地水体，默认读取水体 `Refractive Index`（1.333）。旧庭院折射排水光束也读取此值。单独放一片水不会创建激光发生器或投影机关。 |
| 排水与复位 | 拖入 `DrainGate.prefab` 或 `PoolsideDrainConsole.prefab`，指定 `Water`；自动查找要求装置位于池域 XZ 范围内，因此岸外控制台建议明确拖入水体。`Drain Position` 是水体局部 XZ 出口，`Drain Speed Multiplier` 控制开闸后的速度。 |

普通漂浮方块的缩放会改变排水体积，Rigidbody 的质量仍由你设置；倾斜物体用四点浮力近似。导轨浮板按自身 `Body Density` 保持缩放后的密度。角色继续使用独立的表面游泳控制，不是刚体浮力。

每个水体实例拥有独立网格、水量和排水状态。水体之间不会自动连通交换水量。视觉折射仍为屏幕空间近似；玩法折射使用斯涅尔定律。该系统保留原高度场边界，不是完整三维液体求解器，也不会让通用激光自动穿水折射。

## 代码入口

- `Assets/Scripts/WaterSystem/WaterVolume.cs`：求解器、介质参数、水面采样及运行时水域注册。
- `WaterVolume.FindAt(...)`：按调用者的物理场景查找当前有效水域，排除禁用水体及角色脚下无法接触的上层水池，无逐帧全场景搜索。
- `Sample(...)` / `SurfaceNormal(...)`：实际水面高度、流速、水深及法线。
- `OpenDrain()` / `ResetWater()`：排水、复位；`Disturb(...)`：局部扰动。
- `Assets/Scripts/Editor/Gameplay/WaterSurfacePreviewEditor.cs`：编辑器预览，独立于运行时模拟。

## 本次验证（Unity 6000.5.9f1）

- 水体新用例 11 项通过：自动游泳采样、独立副本、普通刚体漂浮与密度改变、独立排水、禁用/恢复、物理场景隔离、折射率改变、导轨浮力读取密度。
- 编辑器验证通过：预制体已带材质、持久网格和水深数据；Prefab Mode 可重建预览；重复重建复用相同网格；修改副本尺寸不污染源网格；实验池四边对齐。
- 原有回归通过：水机关 10 项、水中上下岸 11 项、实验场排水/光路 21 项、实验场移动 23 项、主地图移动 6 项、机关预制体 36 项。
- 实验池开闸 22 秒：水位 −0.30 → −3.12 米；浮板 −0.20 → −3.30 米。复位后浮板回到 −0.20 米。主地图与实验场都能从楼梯下水、游回岸边并跑上岸。
- 本次完成编辑器和 Play Mode 验证，未重新打包 Windows 程序。Unity 的既有 Package Manager `path` 异常仍会出现；这次没有新增脚本编译错误。

本次检查输出见 [RegressionChecks.txt](RegressionChecks.txt)。

![蓄水状态](01-PoolFull.png)

![排水后的水位和浮板](02-PoolDrained.png)

![蓝色折射光路](03-BlueRefraction.png)
