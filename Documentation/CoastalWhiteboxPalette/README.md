# 白色与淡土黄色配色

最新试玩：`Builds/CoastalWhitebox-Windows/CoastalTemple.exe`，压缩包为同名 `.zip`。

- 岩壁、神庙与庭院：暖白色。
- 原草地顶面、地表、山路及沙滩：不同明度的淡土黄色。
- 去掉模型彩色纹理绑定；Terrain 改为四张 2×2 的纯色纹理，保持地形与模型配色一致。
- 海水和机关发光反馈保留。模型形状、路线、碰撞与水模拟未修改，独立水实验场景的材质也未修改。

![整体](01-Overview.png)
![尖岬](02-PointedSummit.png)
![俯视地图](03-TopMap.png)

以上为 Unity Play 实际渲染。修改配方：`Tools/Terrain/ApplyHeadlandWhiteboxPalette.cs.txt`。原岩石贴图保留于 ThirdParty 资源目录。原 V6 验证截图保留为改色前历史，当前画面以本目录为准。
