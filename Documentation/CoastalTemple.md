# 海岸神庙白模

场景：`Assets/CoastalTemple/Scenes/CoastalTemple.unity`。Unity 6000.5.9f1，ProBuilder 6.1.2。

先制作了 [概念图](Concepts/CoastalTemple-Concept.png)，再在 Unity 编辑器中通过原生 Terrain、ProBuilder 建模操作及预制件实例建立本场景。概念图是设计参考，实际白模截图见 [验证目录](CoastalTempleVerification/README.md)。

## 空间与编辑

- `00_ShoreStart`：岸边落脚平台；`Player_ShoreStart` 是 Play 后的起点。
- `01_CoastalTerrain`：300 × 330 米的原生 Terrain，513 高度图，高度范围 −10～60 米。V6 仅用它辅助沙滩、道路、接缝和基础，山体主要轮廓由岩石模型承担。可使用 Terrain 的 Raise/Lower、Smooth、Set Height 工具继续塑形；新水池位置已重新开 Terrain Hole。
- `02_ShoreRuins_WaterCourt`：露天残柱、残墙、可进入的原有深水池与独立排水机关。原庭院封闭边界在此场景中停用。
- `03_MountainPath`：原长楼梯已停用保留。当前道路从北岸 (-65,0,204) 绕西侧向南上山，到 (-24,23,34) 的半山，再朝东北进入三角形山顶左侧缓坡。道路宽约 6 米，上下山和尖岬往返共 42 段已通过 CharacterController.Move 验证。最新画面与地图见 [V6 模型山体](CoastalV6Verification/README.md)。
- `04_SummitTemple`：山顶神庙预制件，包含 20 根柱、台基、入口台阶、内殿、山墙屋顶。V6 根节点位置 (29.6,−0.2,−24.45)，缩放 0.65，建筑中心约 (40,31,88)，内殿地面约 32.3 米。神庙位于尖岬较宽的根部，东北最尖端保持开放。可直接编辑 ProBuilder 顶点/边/面，或替换视觉模型。
- `05_WalkthroughControls`：行走/相机/水机关输入，仅控制现有对象。三个 View 标记可在编辑器里移动，改变 F1/F2/F3 的观察角度。
- `08_CliffLandforms`：V5 的两个方台已停用，仅留作历史对象。
- `09_ModelHeadland`：V6 的 13 个岩石模块，来自用户提供的 NavMeshProject/PureNature。`Summit_TriangularCliff04` 是向东北收尖、左侧低而尖端高的三角山体；`West_GradedFlank_Cliff02` 承托南行道路；其他模块拼接半山、山肩和崖脚。网格保存于 `Terrain/Headland`，原 FBX 与纹理保存于 `ThirdParty/PureNatureSubset`。模型均配套静态 MeshCollider，移动或缩放时碰撞随对象变化。若编辑网格顶点，也须同步更新碰撞网格，并检查与 Terrain 道路的衔接。

`TempleColumn.prefab` 和 `SummitTemple.prefab` 位于 `Assets/CoastalTemple/Prefabs`。单独更换美术时保留碰撞尺寸、道路高度和机关引用。新的环境没有运行时建模脚本，也无需执行生成菜单即可打开、编辑和游玩。

原 `Sea_Backdrop_VisualOnly` 已停用。当前 `07_Sea` 包含可游泳、可扰动的近岸水、GPU 叠加海浪、轻量远海及越界回点组件。V6 重新适配海床预览和北侧出生点；水池整体平移到 (-50,0,182)，保留独立模拟和机关连接。详见 [海面实现](CoastalV3Verification/README.md) 与 [搬迁后验证](CoastalV6Verification/PoolChecks.json)。

## 排水速度

`Assets/WaterSystem/Runtime/WaterVolume.cs` 的 `public float drainSpeedMultiplier = 4` 同时显示在 Inspector 中，范围 0.1–8。已对独立水体预制件与两张场景保存默认值 4。

- 1：水体按原来的物理时间推进。
- 4：当前庭院数值测试在 50 秒内排去超过 99.5% 水量。
- 更大的值：加速水流传播和排水过程，也增加 CPU 计算量。

这是可调的玩法时间倍率，开闸后只加速水体，不等同于扩大真实排水口，也不修改 `Time.timeScale`。水量仍由局部出流移除；稳定子步、质量守恒、流速与出流计量一起处理。`outletArea` 仍单独控制出口面积，`simulationHz` 控制更新频率。

## 团队协作与依赖

TerrainData 是 Unity 原生二进制资源，已单独加入 Git LFS，避免文本换行转换。V6 的派生网格、源 FBX 和贴图同样使用 LFS。修改同一份 TerrainData 或网格时由一名成员主改；建筑和不同山体模块可分工。提交资源时同步提交 `.meta`。

ProBuilder 通过 Unity Package Manager 还原；锁定 6.1.2，以适配当前编辑器 API。升级依据为 [Unity 官方变更记录](https://docs.unity3d.com/Packages/com.unity.probuilder@6.1/changelog/CHANGELOG.html)。

`Packages/com.coplaydev.unity-mcp` 是带 MIT 许可证的编辑器自动化工具，不是玩法或运行环境生成器。普通编辑和试玩不依赖本机 MCP 服务。`Assets/Editor/LocalMcpConnection.cs` 只有检测到本机忽略目录 `Logs/Mcp/connect` 时才自动连接 localhost；其他成员克隆后不会自动连接。Python 客户端示例在 `Tools/Mcp/UnityMcp.py`，需自行准备 FastMCP 环境。
