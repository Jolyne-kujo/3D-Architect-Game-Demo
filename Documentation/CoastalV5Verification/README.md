# 悬挑断崖与折返登山路线

本版保留原生 Unity Terrain 山体和简约浅色白模，突出半山方台与山顶神庙。场景为 `Assets/CoastalTemple/Scenes/CoastalTemple.unity`。

## 本次形态

- 山顶台地由约 48 米抬高到 78 米；神庙向岸边前移 22 米，形成清晰的高处地标，出生点仍能看见。
- 半山平台高约 28 米，外轮廓约 53 × 38 米，采用小倒角方块形体。主体相对山坡独立，右侧接入山体，崖面向下内收。
- 起点路线先向右绕山，再向左折回半山方台。平台右侧另一个路口连接上段登山路。
- 从俯视图看，上段沿东侧 → 后山北侧 → 西侧 → 山顶正面逆时针上升，最后进入神庙。
- 平台前右方保留一处低山口，形成上山途中被山肩挡住、到平台边缘后看见东侧海面的视线变化。

## 编辑与性能

原生 Terrain 是高度场，不能单独表示同一水平坐标下的悬空崖底。因此主体继续使用 Terrain，负角度部分用两个保存好的 ProBuilder 实体补充；这也是 [Unity 地形开洞文档](https://docs.unity.cn/6000.5/Documentation/Manual/terrain-PaintHoles.html) 提到的悬挑处理方式。

在 Hierarchy 中选择 `08_CliffLandforms/Halfway_CliffBlock` 或 `Summit_OverhangingCliff`，可直接编辑 ProBuilder 顶点、边和面。两个实体分别 44 / 50 三角形，配静态非凸 MeshCollider，没有新增逐帧建模或地形运算。主体高度图仍为 513²，四个 TerrainLayer；海水网格和更新频率不变。

`Tools/Terrain/SculptCliffCoast.cs.txt` 是编辑器塑形记录，保存在 Assets 外，正常打开和游玩无需执行。重新执行会覆盖本轮对应的地形、岩台与位置，后续手工编辑前应保留版本。

## 实际验证

- `RouteChecks.json`：CharacterController.Move 实际完成 25 段上山和 25 段下山，段内无跳跃或传送；起点神庙视线通过。
- `GeometryChecks.json`：两个悬挑体闭合、无退化三角形、碰撞体匹配；从崖下向上射线能碰到向下倾斜的崖面，其下有真实净空。半山平台的东海视线由遮挡转为可见。
- `RuntimeChecks.json`：19 项原有海面及水体回归检查通过，包括九处接缝/越界回点、正确选择近海与独立水池、波形变化和模拟边界水位。编辑器中单次海水模拟及网格更新测得约 3.53 ms（15 Hz）；该数值不是整帧性能或目标硬件保证。
- `SceneValidation.json`：重新加载场景后，0 丢失脚本、0 丢失预制件；两处 ProBuilder 网格及对应碰撞体引用仍在，TerrainCollider 与水池 621 个开洞单元保留。
- 自动路线检查在 Unity Editor Play 中执行，不代表完整键鼠人工试玩。PNG 均为实际 Unity 相机渲染，未使用概念图替代结果。

## 看图与试玩

`01-Overview.png` 为整体轮廓，`02-Spawn.png` 为起点视线，`03-HalfwayCliff.png` / `04-SummitCliff.png` 为两处负角度断崖，`05-RightFork.png` 为半山右侧路口，`06-TopRoute.png` 为整条路线俯视，`07-RearAscent.png` 为后山上升段，`09-HalfwaySea.png` / `10-BeforeReveal.png` 为同一东侧海面目标的前后视线。

本机试玩：`Builds/CoastalCliff-Windows/CoastalTemple.exe`，分享时使用整个 `Builds/CoastalCliff-Windows.zip`（52.85 MB，215 项，CRC 通过，不含 DoNotShip 文件）。构建成功，0 错误，486 条已有包/着色器警告；不属于无警告构建。Windows 版以 D3D12 初始化显卡并持续启动运行约 34 秒，日志未出现脚本异常或场景加载失败。启动检查使用 batchmode，不等同于人工键鼠试玩。详细数据见 `BuildChecks.json`。

操作：WASD 行走/游泳，Shift 快走，空格跳跃/上浮，Ctrl 下潜；F1 全景、F2 半山海面、F3 神庙，Home 回起点，Tab 切换观察；1 排水、R 蓄水。
