# 海岸神庙白模验证

2026-09-17，Unity 6000.5.9f1，Windows。验证对应已保存的 `CoastalTemple.unity`，水体默认排水倍率 4。

- `Numerical.txt`：22/22 独立数值检查通过，包括 4 倍速 50 秒露底、守恒、非负水深、闭阀不加速、8 倍速出流单位和非法参数。
- `Route-Ascent.json`、`Route-Descent.json`：使用场景中的真实 CharacterController 和碰撞体，沿路径连续 Move，上山 12/12、下山 12/12 路段通过。验证了台阶接续与神庙入口；这是自动行走检查，不是完整手动游玩覆盖。
- `Runtime-DrainCompletion.json`：Unity Play 中开光后约 45.36 游戏秒（Time.timeScale = 1）排去 99.5% 水量，余水约 0.4985%。初始水量 689.936 m³，剩余水量与累计出流之和相等至浮点误差。
- 该次排水更新观察的平滑核心耗时均值约 4.37 ms、最大约 9.62 ms；统计口径为 WaterVolume 中数值求解计时的平滑值，编辑器会受到其他工作负载影响，不包含网格更新或 GPU，也不是完整帧性能基准。
- 山脚眼高 1.8 米到神庙山墙的物理射线没有被地形挡住；实际山脚截图同时确认可见轮廓。
- Unity 场景验证：0 个缺失脚本、0 个损坏预制件。代码审查提出的构建入口问题已修复，CoastalTemple 为第 0 场景，独立 WaterCourtyard 为第 1 场景。

四张 PNG 来自 Unity Play 中实际 Camera 渲染，1600 × 900，无后期图片修改：

1. `01-CoastalOverview.png`：海岸、遗迹、水池、上山路和神庙整体关系。
2. `02-MountainFoot.png`：山脚可见神庙。
3. `03-SummitTemple.png`：神庙台阶、柱廊与屋顶。
4. `04-ShoreStart.png`：岸边起点观察方向。

`Runtime-Water.json` 为前一次排水运行中的独立守恒抽查；精确本轮阈值时间以 `Runtime-DrainCompletion.json` 为准。Windows 构建结果另见本目录构建报告。

Windows 构建成功：124.8 MB，0 错误，487 条依赖/构建警告。程序已实际启动，D3D12 / RTX 4060 Laptop，启动日志未出现运行异常。桌面截图的应用授权超时，未完成独立程序的图像与键盘验收；上述截图与双向行走验证来自 Unity 编辑器 Play。分享压缩包约 56.7 MB，不含 Burst 调试目录；构建产物保留本机，不纳入源码仓库。
