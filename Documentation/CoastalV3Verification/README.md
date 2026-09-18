# 海面与白模关卡（2026-09-18）

本版保留最初的灰白 Terrain 和建筑，去掉导入的写实岩壁。山路改为贴坡步道；半山平台能在转弯后看到此前被山体遮挡的东侧海面。起点石砖外增加了沙滩过渡。

## 海水的组成

- **近岸**：独立 `WaterVolume`，72 × 86 个格子（6,192），4 米格距，15 Hz；负责局部水量、流速和游泳扰动。海床在初始化时读取已保存的 Terrain，运行期间不重新生成山体。
- **大浪**：GPU 顶点着色器叠加四组方向不同的正弦波。波长 42 / 27 / 18 / 13 米，默认振幅总上限 ±0.625 米。浅水处减弱；角色采样采用相同参数和同一网格三角形插值。
- **小波纹**：两层移动的 256 × 256 算法生成法线纹理，带 mipmap。远海只保留这层视觉细节和颜色/天空反射，不进行流体求解。
- **远海**：环形平面，仅 8 个三角形。近远海同材质、同水位；接缝前 12 米同时衰减几何浪和求解器表面位移，避免裂缝。
- **死亡边界**：黄色浮标标记近远海交界，接触四边或角落即返回出生点。按角色胶囊半径检查，越过边界的高速移动也能捕获；回点不重置水池机关。

技术参考：[GPU Gems 第 1 章](https://developer.nvidia.com/gpugems/gpugems/part-i-natural-effects/chapter-1-effective-water-simulation-physical-models)，该章介绍了用于 Uru: Ages Beyond Myst 的叠加波与 GPU 水面方法。本项目采用四组正弦高度波，未使用水平位移的完整 Gerstner 波或 FFT 海洋。

这些是高度场和解析海浪，不包含三维翻卷、破碎浪和体积液体飞溅。

## 调节与操作

选中 `07_Sea / Nearshore_SimulatedWater`：

- `OceanWaves.waveHeight`：波高倍率，默认 1。
- `waveSpeed`：传播速度倍率，默认 1。
- `wavelengthScale`：波长倍率，默认 1。
- `seamFadeDistance`：几何浪在接缝前的衰减距离，默认 12 米。
- `WaterVolume.simulationHz`：近岸物理更新频率，默认 15。格距与域尺寸在进入 Play 前调整；修改海床后重新进入 Play。
- 庭院水池仍使用独立的 `drainSpeedMultiplier = 4`，与海浪速度无关。

WASD 行走/游泳，Shift 快走，空格跳跃或上浮，Ctrl 下潜，Tab 观察，F1 全景、F2 半山海面、F3 神庙，Home 回起点；1 开始水池排水，R 重新蓄水。

场景、Terrain、预制件和材质均已保存，可直接在 Unity 编辑。海浪和流体表面是运行时动态水网格；地形建筑不是运行时生成。项目显式启用了 `com.unity.modules.terrainphysics`，保证 TerrainCollider 在 Play 和打包后保留。

## 验证

- 29 项纯数值检查通过，包括水量守恒、排水、近岸波传播、海浪边界和受扰动接缝。
- Unity Play：14 段上山路线及反向下山通过真实 CharacterController.Move 检查。
- 22 项运行检查通过：四边/四角/高速越界回点、复活朝向、泳者水体选择、独立水池、接缝、起点神庙可见、转弯后海面可见。
- 实际游泳状态和自动 LateUpdate 回点另见 JSON。
- 编辑器内近岸求解加网格更新平均 **3.29 ms/次**（90 次测量，15 Hz）。折算到 60 FPS 约 **0.82 ms/帧的平均 CPU 工作量**；这不是整帧耗时或 GPU 性能承诺，更新发生的帧仍承担完整单次开销。
- PNG 是 Unity Play 相机实际渲染，不是概念图。
- 视线射线首先命中山顶神庙的柱体，确认起点可见目标。

试玩包：`Builds/CoastalSea-Windows/CoastalTemple.exe`，分享包为 `Builds/CoastalSea-Windows.zip`（约 57 MB）。构建成功：0 错误、486 条包/着色器警告，详见 Build.json；并非无警告构建。Windows 程序已启动，玩家日志未出现脚本异常、着色器错误或地形碰撞模块被移除的问题。场景/交互/截图检查在 Unity Editor Play 中完成，未把启动检查当作完整的独立程序手动试玩。
