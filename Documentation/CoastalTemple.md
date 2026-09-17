# 海岸神庙白模

场景：`Assets/CoastalTemple/Scenes/CoastalTemple.unity`。Unity 6000.5.9f1，ProBuilder 6.1.2。

先制作了 [概念图](Concepts/CoastalTemple-Concept.png)，再在 Unity 编辑器中通过原生 Terrain、ProBuilder 建模操作及预制件实例建立本场景。概念图是设计参考，实际白模截图见 [验证目录](CoastalTempleVerification/README.md)。

## 空间与编辑

- `00_ShoreStart`：岸边落脚平台；`Player_ShoreStart` 是 Play 后的起点。
- `01_CoastalTerrain`：300 × 330 米的原生 Terrain，513 高度图，山顶台地约海拔 48 米。选中后使用 Terrain 的 Raise/Lower、Smooth、Set Height 工具继续塑形；池底区域开了 Terrain Hole。
- `02_ShoreRuins_WaterCourt`：露天残柱、残墙、可进入的原有深水池与独立排水机关。原庭院封闭边界在此场景中停用。
- `03_MountainPath`：11 段道路/楼梯和圆形转折平台。台阶上升约 0.24 米；路宽 5.4 米。拐角和神庙入口已经用实际 CharacterController.Move 双向走通。
- `SummitTemple`：山顶神庙预制件，包含 20 根柱、台基、入口台阶、内殿、山墙屋顶。可直接编辑 ProBuilder 顶点/边/面，或把视觉部分替换成美术模型。
- `05_WalkthroughControls`：行走/相机/水机关输入，仅控制现有对象。三个 View 标记可在编辑器里移动，改变 F1/F2/F3 的观察角度。

`TempleColumn.prefab` 和 `SummitTemple.prefab` 位于 `Assets/CoastalTemple/Prefabs`。单独更换美术时保留碰撞尺寸、道路高度和机关引用。新的环境没有运行时建模脚本，也无需执行生成菜单即可打开、编辑和游玩。

海面 `Sea_Backdrop_VisualOnly` 是本轮用于判断岸线的背景色面；可交互流体仍是遗迹里的独立水池。当前没有海洋物理或水下地形玩法。

## 排水速度

`Assets/WaterSystem/Runtime/WaterVolume.cs` 的 `public float drainSpeedMultiplier = 4` 同时显示在 Inspector 中，范围 0.1–8。已对独立水体预制件与两张场景保存默认值 4。

- 1：水体按原来的物理时间推进。
- 4：当前庭院数值测试在 50 秒内排去超过 99.5% 水量。
- 更大的值：加速水流传播和排水过程，也增加 CPU 计算量。

这是可调的玩法时间倍率，开闸后只加速水体，不等同于扩大真实排水口，也不修改 `Time.timeScale`。水量仍由局部出流移除；稳定子步、质量守恒、流速与出流计量一起处理。`outletArea` 仍单独控制出口面积，`simulationHz` 控制更新频率。

## 团队协作与依赖

TerrainData 是 Unity 原生二进制资源，已单独加入 Git LFS，避免文本换行转换。修改同一座山体时由一名成员主改；建筑可以按预制件分工。提交资源时同步提交 `.meta`。

ProBuilder 通过 Unity Package Manager 还原；锁定 6.1.2，以适配当前编辑器 API。升级依据为 [Unity 官方变更记录](https://docs.unity3d.com/Packages/com.unity.probuilder@6.1/changelog/CHANGELOG.html)。

`Packages/com.coplaydev.unity-mcp` 是带 MIT 许可证的编辑器自动化工具，不是玩法或运行环境生成器。普通编辑和试玩不依赖本机 MCP 服务。`Assets/Editor/LocalMcpConnection.cs` 只有检测到本机忽略目录 `Logs/Mcp/connect` 时才自动连接 localhost；其他成员克隆后不会自动连接。Python 客户端示例在 `Tools/Mcp/UnityMcp.py`，需自行准备 FastMCP 环境。
