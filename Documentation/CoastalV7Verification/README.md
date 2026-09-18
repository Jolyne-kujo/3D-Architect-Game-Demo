# V7：半山与尖顶连成紧凑山体

2026-09-18，场景 `Assets/CoastalTemple/Scenes/CoastalTemple.unity`。

半山搬到旧海湾位置，贴住山顶。北岸入口 → 南行凿路 → 约 41 米半山 → 侧面折返 → 约 64 米神庙台基；东北尖岬约 75 米。另一条支路从半山边缘向下，到被岩体遮住的新沙滩。沙滩是可到达的海边关卡预留区，尚未布置新的机关谜题。

## 实际画面

下图来自 Unity Play 相机。每次切换视角后等待真实帧更新，再截图，避免使用上一视角的 Terrain LOD 状态。配色保持暖白和淡土黄。

![连续山体全景](01-Overview.png)

![俯视路线](03-TopMap.png)

![起点看山顶](04-Spawn.png)

![凿入岩体的道路](06-CarvedTrail.png)

![半山转弯处显露沙滩](08-SeaReveal.png)

## 编辑与试玩

- `10_CompactMountain` 下 11 块模型分别可编辑；源 FBX 来自用户提供的 PureNature 资源，派生网格另存，不修改源工程。
- 原生 Terrain 承托路面、海滩和庭院；岩石网格也同步下切，保证视觉与碰撞一致。庭院及水池楼梯周围已清出空间。
- 半山中心约 `(-3,41,121)`，隐藏沙滩约 `(58,0,155)`，尖岬约 `(57,75,148)`。
- 模型和建筑已保存在场景，运行时不生成地形。11 块山体共 146,134 个三角形；没有新增流体域或持续地形运算。
- 在 Unity 打开场景 Play，或运行 `Builds/CoastalCompact-Windows/CoastalTemple.exe`。完整目录也打包为 `Builds/CoastalCompact-Windows.zip`。
- WASD 行走，Shift 快走；F1 全景、F2 半山看沙滩、F3 神庙，Tab 返回行走，Home 回起点；1 开启排水，R 重置水池。

## 验证范围

| 项目 | 结果与证据 |
| --- | --- |
| 行走与返程 | [RouteChecks.json](RouteChecks.json)：56 段通过；主路、沙滩支路和尖岬双向 CharacterController.Move，无跳跃、无段内传送，终点包含落地等待；起点看得到神庙。 |
| 几何与视线 | [GeometryChecks.json](GeometryChecks.json)：26 项通过；3 个沙滩目标在起点被遮挡，3 个在半山转弯处显露；镜头为实际步行高度，角色可走到观海边缘；12 处山内道路横断面低于两侧岩体，庭院入口保留一侧岩肩。 |
| 海水回归 | [RuntimeChecks.json](RuntimeChecks.json)：19 项通过，含边界死亡回点、泳水选择、海浪参数与独立水域；近海维持 6,192 格、15 Hz。 |
| 水池回归 | [PoolChecks.json](PoolChecks.json)：8 项通过，含受光开闸、折射、排水、守恒、海水隔离、浮台下降和楼梯往返。 |
| 构建与压缩包 | [BuildResult.json](BuildResult.json)：Windows 构建成功，0 错误、486 条警告；[PackageChecks.json](PackageChecks.json)：62.58 MB，215 个条目，CRC 检查通过。 |
| Windows 启动 | [PlayerSmoke.json](PlayerSmoke.json)：实际构建运行约 365 秒，初始化成功，未发现所检查的异常／崩溃日志；只验证启动和持续运行，不等同于键盘路线测试。 |

这些是编辑器运行时自动验证和实际画面复核，不代表已完成完整的人工键盘游玩或所有离路区域检查。道路入口、清理区、观海开口及沙滩过渡区不会全部封在两面高墙之间。水系统仍是原有高度场模拟，不是三维自由液体。

编辑器制作配方：`Tools/Terrain/AssembleCompactMountain.cs.txt`，只能在 Edit 模式通过 Unity MCP 执行，会重写这版网格、Terrain 与相关摆放。日常美术修改不必执行。验证脚本位于同目录；截图用 `CaptureCompactMountain.py`，需要已有本机 MCP 连接及 FastMCP 环境。
