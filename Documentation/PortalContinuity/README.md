# 双面实时传送门修复

入口仍为 `Assets/Prefabs/Mechanisms/PortalPair.prefab`。两端默认开启 `LightPortal.TwoSided`；从正面和背面都能看见目标空间、穿越人物与激光。独立 `Portal1` / `Portal2` 使用时仍需互相指定场景实例的 `Paired`。主地图和实验场的现有门、V8 / V9 教学 prefab 已保存双面配置，没有重建地图布局。

## 原因和修改

| 现象 | 复现到的原因 | 修改 |
| --- | --- | --- |
| 远处门是黑色，换角度后画面冻结 | 旧代码拒绝背面渲染，却保留上一次正面纹理；正面 4 / 40 / 200 米本身可以渲染 | 两面参与视锥检测；未绘制窗口清除旧视图，只有成功提交的新图才可显示 |
| 只能从一面进入 | 人物扫掠、激光穿越、出口偏移、身体切片和第三人称摄像机都固定按正面处理 | 共用入口侧符号，映射到对应出口侧；保留跨面速度与剩余位移，避免反弹或镜头跳跃 |
| 贴近时出现空白缝 | 薄门面在扩展厚度前做视锥测试，落在近裁剪面以内；厚度固定朝单侧扩展 | 每次可见性判断前按观察侧保护近裁剪面；出口斜裁剪也随观察侧翻转 |
| 多门里有窗口不更新 | 先画的递归分支耗尽固定预算，后续窗口沿用旧图 | 给每个直接可见窗口预留一次当帧绘制；仅嵌套视图共享剩余预算 |
| 递归状态相互影响 | 旧分支仅恢复纹理，没有恢复门面位置、厚度和隐藏状态 | 每一层完整保存并恢复渲染上下文；缓存复用状态数组和 RenderTexture |

当前沿用 **URP 的独立摄像机渲染请求 + RenderTexture**，内部视图先渲染，再合成外层门。并非把 Minecraft 的 Java / OpenGL 渲染器直接移植到 Unity。条件递归、双面斜裁剪、近裁剪保护和渲染上下文管理在本项目里独立实现。

## 性能与边界

- `PortalSurface.RecursionDepth` 默认 2，最多 4；`MaxTextureSize` 默认 1024；仅视锥内窗口提交渲染，内部层降低分辨率。
- 常规目标每帧最多 12 次门户绘制。若第一层可见窗口数超过 12，则仍逐个更新第一层，并暂停额外嵌套绘制。深层到达限制时显示封底，不保存人物静止画面冒充实时视图。
- 可以制作多组门；实际验证三组不同门的嵌套视图。窗口仍是有限深度渲染，没有无限递归。
- 使用一个观察摄像机，运行验证针对 Game 摄像机；未实现同时供 Scene / Game / 分屏 / XR 多观察者使用的独立纹理集合。
- 传送门不会挖掉已有墙体碰撞，继续保留世界 Y 重力与原有角色尺寸规则；本次没有引入跨空间音频、射线之外的隔门交互或异步加载其他场景。

## 验证

Unity 6000.5.9f1 原生 Play 模式，读回实际 URP 像素，没有用概念图代替渲染验证。

- [Baseline.json](Baseline.json)：修改前 14 项中失败 10 项，覆盖背面黑屏 / 停帧、近裁剪、反向穿越和预算不足。
- [Checks.json](Checks.json)：修改后 14 / 14，通过双面 4、40、200 米画面、目标变化、双面贴脸、激光、人物扫掠、身体切片、三组门嵌套及 16 个直接可见窗口全部刷新。
- [ShowroomCrossings.txt](ShowroomCrossings.txt)：实验场实际玩家通过两扇门的正反面，4 / 4 成功；跨面摄像机位置差最大约 0.0000012 米。使用真实 CharacterController 移动 API 自动推进，不是完整人工游玩。
- [LiveFrames.txt](LiveFrames.txt)：实际 RedBot 动画持续运行，两次采样间 11348 帧对应 11348 次窗口刷新；门内红色身体有 814 个像素变化，左手骨骼位置变化约 0.50 米。[动画帧 A](Animation-A.png) / [动画帧 B](Animation-B.png)。
- 原有门户 Runtime 11 / 11、身体 Visual 4 / 4、URP Render Probe 通过；机关 prefab 回归 36 / 36。报告位于 `Documentation/Mechanisms` 与 `Documentation/CoastalTemple`。
- [SavedConfiguration.txt](SavedConfiguration.txt)：保存的场景和 prefab 范围。未重新构建 Windows 发布包。

实际场景截图：[正面窗口](Showroom-Front.png)、[另一动画帧](Showroom-Front-Animated.png)、[背面窗口](Showroom-Back.png)。

## 参考

- [Unity 6 URP Create a render request](https://docs.unity.cn/6000.0/Documentation/Manual/urp/User-Render-Requests.html)：当前 URP 可同步提交单摄像机到渲染纹理；不需要改写底层引擎。
- [Immersive Portals 实现说明](https://qouteall.fun/immptl/wiki/Implementation-Details)：比较 stencil 与 framebuffer 路线，参考条件递归、上下文恢复及跨面显示思路。
- [Immersive Portals RendererUsingStencil.java](https://github.com/iPortalTeam/ImmersivePortalsMod/blob/1.21/src/main/java/qouteall/imm_ptl/core/render/renderer/RendererUsingStencil.java)：阅读其递归层入栈 / 出栈与深度恢复；本项目没有复制它的 Minecraft 渲染接口。
- [Sebastian Lague Portal.cs](https://github.com/SebLague/Portals/blob/master/Assets/Scripts/Core/Portal.cs)：核对观察侧、近裁剪保护与身体切片处理，采用本项目现有空间映射约定。
