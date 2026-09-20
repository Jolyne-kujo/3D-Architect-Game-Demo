# 第三人称试玩视角

主场景仍为 `Assets/Scenes/CoastalTemple.unity`，默认第三人称。鼠标环绕观察，WASD 相对镜头水平朝向移动，角色朝实际移动方向转身。Shift 快跑，空格跳跃 / 上浮，Ctrl 下潜；E、H、L、Home 和原教学操作保持不变。

完整 Quaternius 人偶显示在世界中，继续播放用户提供的 Slow Run / Fast Run。原地观察不移动或旋转身体；移动、动画和灯光照常影响真实投影。第一人称手臂及叠加摄像机停用，提灯移到完整人物的左手。交互距离仍从角色眼部计算，不从后方镜头计算。

镜头使用稳定的角色支点，不跟随动画头骨。默认距离 4.2 米、支点高度 1.48 米、肩偏移 0.35 米、俯角 10 度；游泳时提高支点。实体障碍立即收近镜头，障碍移开后缓慢恢复；极近时只隐藏身体表面并保留阴影，避免看到模型内部。常规检查使用无分配物理查询，只有命中缓冲填满时才补查。

选择场景中的 `Player_ShoreStart`，在 `CoastalPlayerCamera` 调整距离和构图。菜单 `Coastal Temple > Player > Use third-person preview` 可重新应用当前配置。第一人称代码保留，可通过 `SetThirdPerson(false)` 或原来的第一人称配置菜单返回；日常游戏没有新增视角切换快捷键。

相关代码集中在 `Assets/Scripts/Gameplay/Player`；移动控制仍在 `Assets/Scripts/WaterDemo/Runtime/CourtyardWalker.cs`，第三人称只新增输入方向和转身配置，不改变水体求解或惯性参数。

## 验证

最终摄像机组件检查 12/12、场景运行检查 26/26、惯性检查 19/19 通过，水体数值回归 29/29 通过。测试结束时控制台没有错误；之后重新进入试玩时，Unity 编辑器的 `ProcessInitializeOnLoadMethodAttributes` 记录一条 `Deleting invalid font reference.`，无游戏脚本堆栈，实际游戏画面中的中文字体正常显示。

- [摄像机组件检查](CameraChecks.txt)：实际 Unity 球形检测、身体 / 手臂显隐、原地环绕和视角恢复。
- [场景运行检查](PlayChecks.txt)：完整人物跑步、转向、跳跃、镜头避障、游泳 / 下潜、提灯和交互位置。
- [惯性检查](MomentumChecks.txt)：30 / 60 / 144 Hz 加速、空中松键、落地减速。
- [游戏画面](05-ShorePreview.png)、[慢跑](01-SlowRun.png)、[快跑](02-FastRun.png)、[游泳](04-Swimming.png)。灰色地面是临时测试场地，不保存进关卡。
- [已打开的试玩画面](06-ReadyToPlay.png)：镜头环绕后的实际场景，点击游戏画面或按 Esc 接管角色。

测试中发现第一人称模型默认隐藏时，动画图尚未启动就读取动画层会报错；已改为首次显示并完成动画初始化后再读取。自动起跳还发现测试计时器把末尾一帧切成微秒级片段，导致 PhysX 零位移更新丢失落地标记；[复现记录](JumpProbe.txt) 确认原因后，测试改为使用与实际输入相同的完整帧步长。
