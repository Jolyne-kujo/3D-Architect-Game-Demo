# PureNature 环境模型子集

来源：用户提供的 `NavMeshProject/Assets/PureNature`。原工程保留未移动，仅复制了环境几何、四张基础颜色纹理、对应 `.meta` 和原始 `Readme.pdf`。

- `Models/Cliffs`、`RocksModular`、`Rocks`、`Boulders`：43 个原始 FBX，方便后续继续拼装。
- `Textures`：Cliff02、Cliff03、Cliff04 与 Grass01 的 albedo。原图保留，Unity 导入尺寸限制为 1024，并使用压缩和 mipmap。
- 未导入原项目的玩法代码、角色、材质着色器或工程设置。

当前山体使用 Cliff02/03/04 的几何进行缩放、拼装及局部形变。草顶面与岩壁 UV 分开，派生网格保存在 `Assets/Objects/Environment/Terrain/Headland`，配套标准 URP 材质位于 `Assets/Materials/Environment/Headland_*.mat`。这是本项目的地形拼装，不是原神摘星崖原始模型。

当前场景按要求改用暖白色和淡土黄色纯色材质，不再绑定这些原始颜色贴图；源贴图保留在此处，供后续美术使用。

原资源作者、使用说明与权利信息以附带的 `Readme.pdf` 和原始资源许可为准。本文件记录导入来源，不替代原资源许可。
