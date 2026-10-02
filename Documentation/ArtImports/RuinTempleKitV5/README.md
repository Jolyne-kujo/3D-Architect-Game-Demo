# Ruin Temple Kit V5 实验场展示

来源：组员提供的 `F:\Unity Project\RuinTempleKit_V5_FacetedFractures`。2026-09-26 导入本项目，原目录未修改。

打开 `Assets/Scenes/MechanismPlayground.unity`，Hierarchy 的 `RuinTempleKit_V5_Showcase` 下有四区：

| 节点 | 内容 |
| --- | --- |
| `01_Temple_Courtyard_庭院` | 48 米庭院，214 个模块实例。入口朝向原实验区；中心约为世界坐标 `(70, 0, 8)`。 |
| `02_Terraced_Sanctuary_台地神殿` | 64 × 48 米台地建筑，266 个模块实例。 |
| `03_Processional_Avenue_柱廊` | 80 × 24 米柱廊，240 个模块实例。 |
| `04_Module_Library_全部120款` | 120 款单件按类别排列。展示名称、编号及区域文字均已移除。 |

原实验区右侧有连接道和金色入口标记。Play 后可走到新展示区，再穿过朝西的庭院门查看。选中各分区按 F 可在 Scene 视图聚焦；也可关闭整个展示区或某个分区。展示区共 840 个模块实例，保留 LOD 和共享材质；已提前切换远处 LOD 并启用 PC 管线的 GPU 实例化，仍然是可编辑的资产阅览布置。性能实测见 [优化记录](../../Performance/TempleKit/README.md)。

## 资源在哪里

- 单件预制体：`Assets/Prefabs/地形建筑/单件模块/`，按墙体、柱子、地板、楼梯等分为 16 类，共 120 款。
- 三套组合建筑预制体：`Assets/Prefabs/地形建筑/组合建筑/`
- FBX 和 LOD：`Assets/Objects/Environment/RuinTempleKit/Models/`、`LODModels/`
- 模块尺寸、拼接点和组合布局清单：`Assets/Objects/Environment/RuinTempleKit/Data/`
- 材质：`Assets/Materials/Environment/RuinTempleKit/`
- 贴图：`Assets/Textures/Environment/RuinTempleKit/`

沿用原 `.meta` GUID，模型与建筑继续保持预制体连接。需要新拼建筑时，可直接拖入单件；大型建筑可以整体移动，再逐个调整内部模块。所有资产保持 1 单位 = 1 米。可使用模块下的 `SnapPoints` 对齐位置。

2026-09-26 整理：删除实验场展示区的 125 个 TextMesh 标签，预制体本身没有这些文字。123 个预制体通过 Unity AssetDatabase 移动并保留 GUID，场景实例、组合建筑内部引用、LOD、材质和碰撞不变。直接拖入新目录里的预制体即可搭建，不需要从展示场景复制名称标签。

## 本项目适配

- 导入 120 款 × 3 级 LOD，共 360 个 FBX；导入 120 个模块及 3 个组合建筑预制体。
- 5 个石材从 Standard 转为 URP Lit，保留原色、漫反射贴图和法线贴图；启用材质实例化。粗糙度贴图随包保留，光滑度采用原工具的 0.15，没有错误地直接把粗糙度当作光滑度使用。
- 120 个模块的 LOD0 共加上 330 个静态、非凸 MeshCollider，展示实例共 1961 个网格碰撞体。碰撞不随渲染 LOD 切换。若以后做动态刚体，需要另做凸碰撞或组合碰撞。
- LOD 阈值现在为 `0.70 / 0.32 / 0.005`。导入时曾使用 `0.35 / 0.10 / 0.005`，但 PC 的 LOD Bias 为 2，导致高模保持距离偏长。现在更早使用已有 LOD1、LOD2；末档剔除阈值继续保持很小，避免远看时建筑地板整片消失。LOD 按屏幕占比切换，不是固定米数。
- 未引入重复 OBJ、Rhino 工程、原示例场景、原包 Editor 脚本；避免重复网格和依赖固定目录的重建工具。原包完整内容仍在来源目录。
- FBX 和贴图的 SHA-256 与组员文件一致；只适配了材质、预制体碰撞和 LOD 设置，没有改动模型几何。

## 本次检查

- 导入后缺失网格、材质、脚本均为 0；840 个模型实例均保留三档 LOD。
- Play Mode 实际 CharacterController 检查：从原实验区跑过连接道、穿过庭院入口登上导入地板，均通过；原水池仍可采样到模拟水体。
- 原实验场的 165 个对象/设置序列化块原样保留，只增加展示区根节点。用户原先已有的 `教学关.fbx` 保持不变。主地图未修改。
- 本次运行 Console 错误 0；没有重新构建 Windows 试玩包。

记录：[Unity 导入检查](UnityImportChecks.json)、[运行检查](PlayModeChecks.txt)、[来源与文件校验](SourceImportReport.json)。

![庭院实渲](Courtyard.png)

![全部展示区](Overview.png)
