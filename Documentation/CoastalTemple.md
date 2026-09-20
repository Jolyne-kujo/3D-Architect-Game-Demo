# 海岸神庙白模

场景：`Assets/Scenes/CoastalTemple.unity`。Unity 6000.5.9f1，ProBuilder 6.1.2。

当前配色为暖白色与淡土黄色，模型不使用彩色表面贴图。Terrain 使用同色纯色层，庭院采用本场景的中性材质副本；海水与机关的动态发光反馈保留。最新画面见 [V7 紧凑山体](CoastalV7Verification/README.md)，历史配色见 [白模配色](CoastalWhiteboxPalette/README.md)。

先制作了 [概念图](Concepts/CoastalTemple-Concept.png)，再在 Unity 编辑器中通过原生 Terrain、ProBuilder 建模操作及预制件实例建立本场景。概念图是设计参考，实际白模截图见 [验证目录](CoastalTempleVerification/README.md)。

## 空间与编辑

- `00_ShoreStart`：岸边落脚平台；`Player_ShoreStart` 是 Play 后的起点。
- `01_CoastalTerrain`：300 × 330 米的原生 Terrain，513 高度图，高度范围 −10～100 米。仅用于沙滩、凿路的连续地面、接缝和基础，山体主要轮廓由岩石模型承担。可使用 Terrain 的 Raise/Lower、Smooth、Set Height 工具继续塑形；水池保留 Terrain Hole，岩体也已清出庭院和楼梯空间。
- `02_ShoreRuins_WaterCourt`：露天残柱、残墙、可进入的原有深水池与独立排水机关。原庭院封闭边界在此场景中停用。
- `03_MountainPath`：原长楼梯已停用保留。当前道路从北岸 (-65,0,204) 向南上山，到 (-3,41,121) 的半山。主路折返接入神庙，另一支路经过 (33,37,146) 附近的观海岩肩后下降到新沙滩。路面约 6～7 米宽，山体和 Terrain 同时下切。庭院入口、观海边缘和沙滩坡脚保留开放侧边。主路、海滩支路、尖岬往返 56 段通过 CharacterController.Move 验证，路线数据见 [RoutePoints.json](CoastalV7Verification/RoutePoints.json)。
- `04_SummitTemple`：山顶神庙预制件，包含 20 根柱、台基、入口台阶、内殿、山墙屋顶。根节点位置 (22.6,32.8,−1.45)，缩放 0.65，建筑中心约 (33,64,111)，内殿地面约 65.3 米。神庙位于尖岬较宽处，东北最尖端保持开放。可直接编辑 ProBuilder 顶点/边/面，或替换视觉模型。
- `05_WalkthroughControls`：行走/相机/水机关输入，仅控制现有对象。三个 View 标记可在编辑器里移动，改变 F1/F2/F3 的观察角度。
- `08_CliffLandforms`：V5 的两个方台已停用，仅留作历史对象。
- `09_ModelHeadland`：V6 的 13 个岩石模块已停用保留。
- `10_CompactMountain`：当前 11 个拼接模型，来自项目内 PureNature 源 FBX 的派生网格。半山和山顶紧贴；`Summit_CompactPoint` 为三角尖顶，`Halfway_JoinedFront` 为半山主体，`Beach_BroadRockSlope` 承托通往隐藏沙滩的支路。派生网格保存在 `Terrain/CompactMountain`，原 FBX 位于 `ThirdParty/PureNatureSubset`，未修改源工程。每块模型使用相同网格进行渲染和 MeshCollider 碰撞，可独立选中、移动、缩放。编辑顶点时须同步碰撞，并复查 Terrain 路面衔接。
- `11_CoastalLandmarks`：半山、隐藏沙滩和尖顶的空对象标记，便于定位和后续布置关卡。

`TempleColumn.prefab` 和 `SummitTemple.prefab` 位于 `Assets/Prefabs/Architecture`。单独更换美术时保留碰撞尺寸、道路高度和机关引用。新的环境没有运行时建模脚本，也无需执行生成菜单即可打开、编辑和游玩。

原 `Sea_Backdrop_VisualOnly` 已停用。当前 `07_Sea` 包含可游泳、可扰动的近岸水、GPU 叠加海浪、轻量远海及越界回点组件。V7 已更新海床预览。水池保持 (-50,0,182)，独立模拟和机关连接均保留。详见 [海面实现](CoastalV3Verification/README.md) 与 [当前水池验证](CoastalV7Verification/PoolChecks.json)。

## 排水速度

`Assets/Scripts/WaterSystem/WaterVolume.cs` 的 `public float drainSpeedMultiplier = 4` 同时显示在 Inspector 中，范围 0.1–8。已对独立水体预制件与两张场景保存默认值 4。

- 1：水体按原来的物理时间推进。
- 4：当前庭院数值测试在 50 秒内排去超过 99.5% 水量。
- 更大的值：加速水流传播和排水过程，也增加 CPU 计算量。

这是可调的玩法时间倍率，开闸后只加速水体，不等同于扩大真实排水口，也不修改 `Time.timeScale`。水量仍由局部出流移除；稳定子步、质量守恒、流速与出流计量一起处理。`outletArea` 仍单独控制出口面积，`simulationHz` 控制更新频率。

## 团队协作与依赖

TerrainData 是 Unity 原生二进制资源，已单独加入 Git LFS，避免文本换行转换。所有派生网格、源 FBX 和贴图同样使用 LFS。修改同一份 TerrainData 或网格时由一名成员主改；建筑和不同山体模块可分工。提交资源时同步提交 `.meta`。

ProBuilder 通过 Unity Package Manager 还原；锁定 6.1.2，以适配当前编辑器 API。升级依据为 [Unity 官方变更记录](https://docs.unity3d.com/Packages/com.unity.probuilder@6.1/changelog/CHANGELOG.html)。

`Packages/com.coplaydev.unity-mcp` 是带 MIT 许可证的编辑器自动化工具，不是玩法或运行环境生成器。普通编辑和试玩不依赖本机 MCP 服务。`Assets/Scripts/Editor/Tools/LocalMcpConnection.cs` 只有检测到本机忽略目录 `Logs/Mcp/connect` 时才自动连接 localhost；其他成员克隆后不会自动连接。Python 客户端示例在 `Tools/Mcp/UnityMcp.py`，需自行准备 FastMCP 环境。
