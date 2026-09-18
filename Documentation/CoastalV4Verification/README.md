# 原生 Terrain 山体迭代

本版按海岸神庙概念图的空间关系重塑原有 Unity Terrain：岸边遗迹、渐进上升的山体、半山观海平台、山顶神庙。保持简约几何和浅色岩面，加入少量低饱和草色；未导入写实岩壁模型。

## 山体造型依据

亚平宁并非单一形状的山：它包含褶皱—逆冲构造、不同岩性和后期侵蚀形成的多种地貌。本关卡提炼三类适合玩法的形态，而非模拟地质年代或复刻某座真实山峰：

| 参考形态 | 场景中的表达 |
| --- | --- |
| 褶皱—逆冲带的成组山脊 | 几条方向接近、错落叠置的长山脊，缓倾坡与较陡坡面不对称 |
| 抗蚀岩体的陡壁与台地 | 承托神庙的山顶台地、山腰的观海岩台、折线状山肩 |
| 侵蚀形成的沟谷和鞍部 | 大尺度切面与少数沟槽；路线沿低处穿行，避免悬在外侧 |

依据：[意大利地质调查部门的亚平宁构造说明](https://www.isprambiente.gov.it/contentfiles/00006600/6603-p15.pdf)、[亚平宁国家公园的山脊与岩台地貌介绍](https://www.parcoappennino.it/page.php?id=469)。这些是造型参考，不代表所有亚平宁山体都有相同坡形，也不是地质模拟程序。

## 编辑与性能

- 场景仍为 `Assets/CoastalTemple/Scenes/CoastalTemple.unity`。
- 在 Hierarchy 选 `01_CoastalTerrain`，可直接用 Terrain 的抬升、压低、平滑、设定高度和绘制纹理工具编辑。
- 同一块原生 TerrainData，尺寸 300 × 330 米，高度图保持 513 × 513；TerrainCollider 与水池开洞保留。
- 约 5 米宽的步道在 Terrain 内切出，向两侧过渡到高处山肩。平台及神庙入口有意保持开放。
- 半山岩台约海拔 24 米，山顶神庙基底约 48 米；从出生点可见神庙，从半山转弯前看不到选定东侧海面，到平台后可见。
- 地形使用四个纯色 TerrainLayer，保持单组地形图层；没有新增运行时造山脚本、岩壁网格、岩石碰撞体或逐帧地形运算。
- 近岸海床在进入 Play 时重新读取 Terrain；海浪、近海物理、远海及死亡接缝继续使用上一版系统。
- `Tools/Terrain/SculptFoldedCoast.cs.txt` 是本次编辑时的塑形记录，位于 Assets 外；正常编辑、打开场景与游玩不需要运行它。重新执行会重塑这块地形，后续手工修改应先备份。

## 验证范围

- `RouteAscent.json`：角色实际 CharacterController.Move 通过岸边到神庙的 14 段路径；无需跳跃。
- `RuntimeChecks.json`：22 项检查通过，包括反向下山、出生点神庙视线、半山海景揭示、泳者选择正确水体、原独立水池、四边四角及高速越界回出生点。
- `RoadSections.json`：六处山路横断面，距中心 3.5～8 米范围的两侧山肩均高于路面，测得约 1.02～8.16 米。回头弯处跨过山肩后可落到较低一层步道，平台与神庙入口不要求封闭。
- `SceneValidation.json`：重新打开编辑器后确认 0 丢失脚本、0 丢失预制件、TerrainCollider 存在，水池 621 个地形开洞单元保留。近岸网格和 15 Hz 频率不变。
- 路线测试发现并修复了庭院平整区与下山路交界的一处高度接缝；修复后上下山检查重新通过。
- PNG 均来自 Unity 相机实际渲染。它们展示简约地形版本，不是写实概念图的成品美术复刻。
- 主要检查在 Unity Editor Play 中执行；`Tools/Terrain/Verify*.json` 保存了可通过 Unity MCP 重跑的检查。未将自动角色路线测试描述为键鼠完整试玩。

实机全景见 `12-ConceptAngle.png`，山路第一人称见 `14-EmbeddedPath.png`，平台海湾见 `15-TerraceBay.png`。

Windows 试玩：`Builds/CoastalTerrain-Windows/CoastalTemple.exe`；完整分享包为 `Builds/CoastalTerrain-Windows.zip`（56.6 MB，215 项，包含主程序且无 DoNotShip 目录）。构建成功，0 错误，486 条已有包/着色器警告；不能表述为无警告构建。旧 `CoastalSea-Windows` 是上一版，未替换正在运行的旧程序。

试玩操作保持不变：WASD 行走/游泳，Shift 快走，空格跳跃或上浮，Ctrl 下潜；F1 全景、F2 半山海面、F3 神庙，Home 回起点，Tab 切换观察；1 排水、R 蓄水。
