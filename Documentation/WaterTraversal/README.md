# 投影台阶、水中站立与水面编辑预览

2026-09-20，Unity 6000.5.9f1。主地图 CoastalTemple 与实验场 MechanismPlayground 均已保存。

## 本次问题与修复

此前的 50 FPS 检查没有覆盖高帧率故障。同一组实际投影台阶在 240 FPS 下，4 秒后仍卡在第一块边缘（Z=14.18）。原因是角色撞到低台阶侧面时水平速度被当成撞墙清零，加上 CharacterController 的 0.001m 最小位移，使每帧很小的加速无法累积。

现在最小位移为 0；前方被验证为可跨越的低台阶时保留水平速度。检查仍要求顶部可站立、头顶无阻挡，0.60m 障碍不会自动跨上。并保留普通墙面的阻挡、沿墙滑行。实际投影台阶在 30 / 60 / 144 / 240 / 500 FPS 下，步行均在 0.78～0.80 秒跨过，Shift 跑步在 0.57～0.58 秒跨过；包括在原卡点停下后重新起步及左右 5 度接近。

原来的脚下着地规则无条件优先于游泳，导致角色沿楼梯走到水底。现在只允许浅水站立：脚底水深超过 `CourtyardWalker.maximumWadingDepth`（默认 1.1m，另容纳碰撞胶囊 Skin Width 接触余量）时，即使碰到深处台阶或池底也继续浮游。游回浅处且脚底有可靠支撑时才站立，可以直接跑上斜坡楼梯或按空格跳跃。Ctrl 仍可主动下潜，松开后自动回浮。

## 水与浮力如何工作

- **水本身**：WaterVolume 是网格高度场浅水模拟，计算每格水深、水平流量和排水；可改变水位、产生扰动。它不是三维体积流体，不能表现任意翻卷、破碎浪花或完整水下涡流。
- **角色**：CharacterController 没有刚体质量，不直接施加阿基米德浮力。游泳控制根据局部水位调整竖直速度；CourtyardSurfaceSwimmer 再按实际头部骨骼姿态补偿，使头部骨骼稳定高于水面约 0.24m。主地图实测 0.234m，实验场实测 0.253m。浅水步行与深水游泳按支撑和水深切换。
- **导轨浮板**：GuidedBuoyantPlatform 采样排水体积，以水密度、板密度、重力和水阻力计算运动，再沿允许的导轨方向积分。默认 `bodyDensity=350`、`waterDensity=1000`，自由平衡时约 35% 体积浸没；导轨端点可能限制最终位置。它通过运动学 Rigidbody 提供碰撞与搭载。
- **普通漂浮刚体**：BuoyantBody 在四个采样点调用 AddForceAtPosition，施加浮力与相对水流阻力。

本次实验场排水验证：22 秒内水位从 -0.30m 降到 -3.12m，浮板从 -0.20m 降到导轨底部 -3.30m；补水后回到 -0.20m。

## 在 Scene 中编辑水面

之前水池网格在 WaterVolume.Awake 中生成，所以编辑状态 MeshFilter 为空。现在每个已保存场景的 WaterVolume 都有持久化的初始水面网格，保存于 `Assets/Objects/Water/SurfacePreviews`；编辑状态不运行水模拟。Play 时替换成动态网格，退出后恢复预览，已验证顶点、三角形和水深数据与运行时初始水面相同。

实验场在 Hierarchy 搜索 **Showroom water - use dimensions to resize**。主地图庭院水池是 **WaterVolume — reusable simulation**，近海是 **Nearshore_SimulatedWater**。选中它后：

| 参数 | 用途 |
|---|---|
| Size X / Size Z | 水域宽长；会按 Cell Size 对齐网格，建议填其整数倍 |
| Initial Level | 相对水体物体位置的初始水位 |
| Bottom | 相对水体物体位置的底部高度 |
| Drain Speed Multiplier | 排水时间倍率；越大排水越快，同时模拟计算量增加 |
| 更新并保存水面预览 | 强制重建初始网格；手工雕刻绑定 Terrain 后用此按钮刷新 |

调整上述水域数值会自动更新预览。水体保持旋转为零、Scale 为 (1,1,1)，通过 Size 修改范围；它的模拟域是轴对齐的。水网格没有硬地板碰撞，角色和浮板通过水位采样感知水。

实验场的水域前沿已从 Z=7.5 延伸到岸边 Z=6；后沿仍为 Z=26.5，左右为 X=-26.5 / -5.5。同步延伸池底和侧壁，补齐前壁、东侧步道以及后侧衔接，并保持排水口的世界位置。四边水面均贴合池壁内侧。水位比池沿低 0.30m 是保留的池壁高度，不是水平缺口。

## 验证与证据

Unity 原生 CharacterController、真实场景碰撞、实际 RedBot 动画和渲染。自动测试调用与键盘共享的运动入口；不等同于完整人工手感验收。

- `MechanismPlaygroundChecks.txt`：23 项，含 5 种帧率、慢跑/快跑、边缘重新起步、入水回岸、深水池底浮起、主动潜水、浅水起跳及运行时网格核对。
- `CoastalTempleChecks.txt`：6 项，含三个实际水域初始网格、主地图楼梯入水和回岸。
- `*PreviewChecks.txt`：5 项，验证保存的网格、编辑时不启动模拟和实验场四边无水平缝隙。
- 回归通过：StairWalkChecks 7、StairFootSceneChecks 实验场 11 / 主地图 5、WaterExitRegressionChecks 11、TraversalV2Checks 7、TraversalV2SceneChecks 主地图 3、DebugWaterChecks 21、MechanismWaterDeviceChecks 10、PortalRuntimeChecks 11。
- 旧的“深水中只要脚着地就一直站立”的测试改为“浅水着地站立、深水仍浮游”；斜坡失去接触的检查限定在干燥路段，水下游泳姿态转站立允许短暂落向斜坡。另有专门的完整出入水检查。
- 预览及运行截图：`EditorPool.png`、`ProjectedSteps.png`、`MechanismPlayground-SurfaceSwim.png`、`CoastalTemple-SurfaceSwim.png`。
- 进入/退出 Play 后预览恢复通过，未出现项目脚本编译错误；编辑器原有的 Package Manager `path ... undefined` 窗口错误仍存在，本次没有将其表述为已修复。

初始备份仍是 `backup/pre-stair-foot-ik-2026-09-20`，本次继续使用 `codex/stair-ramp-foot-ik` 开发分支。

CharacterController 最小移动距离设置参照 [Unity 6 官方说明](https://docs.unity3d.com/kr/6000.0/ScriptReference/CharacterController-minMoveDistance.html)。
