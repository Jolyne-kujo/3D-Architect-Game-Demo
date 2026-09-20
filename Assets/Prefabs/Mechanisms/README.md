# 可直接摆放的机关

推荐先打开 `Assets/Scenes/MechanismPlayground.unity` 试玩。拖动本目录的 prefab 即可独立摆放，内部引用已配置；整套移动、旋转、缩放请选最外层根节点。

- 光源 `LaserDevice`：初始关闭，E 切黄 / 蓝 / 红 / 关闭；背面 NOW 显示当前光色，NEXT 显示下次点击的光色。可在 `colorCycle` 中调整顺序。
- 红墙 `RedStoneCurtain`：红光永久消失，黄光临时消失。
- 浮板 `VerticalBuoyantPlatform` / `HorizontalBuoyantPlatform`：放进已有水域，自动找水；横向浮板 E 前进 / 停止 / 返回。
- 传送门推荐 `PortalPair`：整对拖入、再分别摆放两个子门；复制整对不会串线。
- `LightReceiver`、`LightBridge`、`LightDrivenLift`：独立接收器、实体光桥、双向升降台。
- `DrainGate`、`WaterMirage`：排水闸与水中投影机关，放在已有水域内。

完整说明与调节参数：项目根目录 `Documentation/Mechanisms/README.md`。
