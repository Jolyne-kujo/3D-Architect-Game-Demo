# 海岸遗迹与前段双折长廊 · 2026-09-29

主场景：`Assets/Scenes/CoastalTemple.unity`。所有建筑、铺地和岩石已经保存为正常场景对象；运行时不生成关卡。现有机关继续使用原预制体与脚本。

## 本轮布局

- 海滩第一关从原平面布局等比扩大 1.5 倍，庭院主体约 36 × 36 米。保留 12 根单层倒柱引导路线，删除叠柱及旧连续高碰撞围挡。外围使用破损砌体墙、门洞和侧室残基。
- 沙滩改成不规则弧形岸线，地面平缓落入海里；破碎铺地、缺失边角与少量崩石完成建筑到沙地的过渡。
- 按最后一次指示将山上长廊收窄至约 2.8 米可用通道，三段水平总长约 44 米，整体移到前段。保留两个约 148° / 134° 的钝角转弯。
- 三段长廊两侧是紧凑的房屋废墟，墙身、台基、残楼板分开；72 个建筑模块和 23 个铺地模块，共用已有 LOD 预制体。
- 长廊之后到半山腰约 51 米的上坡保留为空地/道路，供后续关卡设计。
- 修整山体与坡道衔接，细分横穿道路的大三角面，清除 5 个仍使用旧山形的重复碰撞体。角色控制器代码没有改变。

## 去哪里修改

| Hierarchy / 资产 | 用途 |
|---|---|
| `20_Architecture/SU_ShoreTutorial` | 沙滩第一关；铺地、残墙、倒柱、机关分别归组 |
| `20_Architecture/Mountain_RuinCorridor` | 三段山腰长廊；按分段、左右、房屋归组；含铺地与路线标记 |
| `10_Geography_EDIT_TERRAIN_HERE/01_CoastalTerrain` | Unity Terrain 沙滩与坡道 |
| `10_Geography_EDIT_TERRAIN_HERE/10_CompactMountain` | 大型山体网格 |
| `12_ShoreTalus_EDIT_ROCKS` / `13_WeatheredCliffApron_EDIT_ROCKS` | 独立可拖动的岩脚与崩石 |
| `Assets/Prefabs/地形建筑/关卡片段/SU_岸边教学关_平地.prefab` | 更新后的海滩关卡 |
| `Assets/Prefabs/地形建筑/关卡片段/SU_山腰双折长廊.prefab` | 可复用建筑长廊，含铺地；放到新地形上时仍需适配地面坡度 |

长廊的四个中心节点为 `(-65,1.2,174)`、`(-65,8.2,160)`、`(-57.39,15.3,147.84)`、`(-42.05,21.2,144.70)`。

原来与长廊重叠的 `02_StoneWorkshop`、`03_FoldedBridge` 已移入 `91_Archive_PreCorridor_INACTIVE`，保留完整对象并停止对应教学提示。新后半坡没有重新放置这些机关。

## 验证

- `Audit.txt`：沿路线每 0.45 米采样中心及左右 1 米；无缺地面、过陡坡面或身体阻挡；机关数量、材质、事件引用检查通过，0 个失败、0 个警告。
- `PlayValidation.json`：实际 Play 模式下使用现有 `CourtyardWalker.SimulateMovement` 输入接口，角色跑过全部 6 段，上行和下行均通过，未按跳跃、未进入攀爬。它是自动输入验证，不是人工手柄游玩记录。
- 同次 Play 测试，直射和四次反射两条光路激活两个红色受光器，石幕按事件消失。退出 Play 后保留初始关闭状态。
- Unity Console 无错误。截图来自 Unity 相机原生渲染，并启用与游戏相机一致的后处理；不是概念图。
- 未进行独立的帧率基准测试。

## 备份与重建工具

本轮前的本地备份：`Logs/RuinCoast29/CoastalTemple.before.unity`、`ShoreTerrain.before.asset`、`ShoreTutorial.before.prefab`。原 TerrainData、原山体网格仍保留；本轮新地形放在 `Assets/Objects/LevelGeometry/RuinCoast29`。

编辑器搭建工具在 `Assets/Scripts/Editor/LevelDesign`：`RuinCoastAuthoring`、`RuinCorridorBuilder`、`RuinRouteMeshCut`、`RuinCoastChecks`。以后直接在 Scene 中调整已保存对象即可。搭建方法会替换它们负责的整组对象，手工改完后不要随意重新调用 `Shore` / `CorridorAndTalus`。

## 参考如何落实

- [English Heritage：Tintagel Castle 遗址结构](https://www.english-heritage.org.uk/visit/places/tintagel-castle/history-and-legend/description/)：借鉴院落、台地、残墙与自然海崖共同形成空间层次，外围由建筑遗存而非柱栏解释。
- [NPS：Mesa Verde Cliff Palace](https://www.nps.gov/meve/learn/historyculture/cliff_palace.htm)：借鉴成组砌体房屋依靠岩壁布置、屋顶与墙冠不完整的轮廓。没有下载或复制该遗址模型。
- 两个钝角的方向和次序来自项目内 SU 教学参考模型；长度与宽度按本轮最新指示缩紧。
