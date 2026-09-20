# V9 空间解谜与模块整理

本版把起点到半山腰重做为三段真实断路：借光后归还支撑、在中层停靠施工、改变光门高度绕过实体遮挡。设计依据为用户指定的[共享设计对话](https://chatgpt.com/share/6aad66a7-5094-83e9-94fd-f4c45b5e88ec)，重点是观察、试验、理解后复用规则。完成标记只发提示和奖励，不替玩家打开机关。

## 已验证

| 检查 | 结果与范围 |
|---|---|
| 连续主路线 | 89 条记录通过；从原出生点走到半山观景落点，19 次正常近距交互，主线传送 0 次 |
| 跌落恢复 | 加入 14 条恢复记录后合计 103 条通过；明确区分测试摆位 4 次与实际自动回点 3 次；机关状态保留 |
| Edit 组件 | 交互 40、原升降组件 11、排水与迁移 20、地形切口 20 项通过 |
| Play 组件 | 独立物理场景内机关 31、跌落恢复 15 项通过 |
| 场景绑定 | 6 项通过；无丢失脚本、3 个教学配置、4 个移动机关、2 段实体光桥 |
| 网格更新 | 19 项通过；连续修改同一个资源后，原生显卡顶点和索引缓冲与当前网格一致，保留资源标识与全部 UV 通道 |
| 真实跑跳 | 第一段 7 米断口无法直接跑跳抵达对岸，跌落后自动回到该段入口；未声称逐一跑跳测试其他断口 |
| 游泳摄像机 | 5 项通过，包括真实水面漂浮、浮箱旁视距、实体墙避让与恢复 |

主路线使用实际 CharacterController 移动意图、正常帧与 Rigidbody 运动；交互经同一个 PlayerInteractor 距离和遮挡检查，且目标必须是当前最近可用操作点。没有直接设置机关成功状态，也没有给角色在主线中传送。它是自动场景验证，**没有注入实体键盘鼠标事件，不等同于完整人工试玩**。

报告：[连续路线](PlayChecks.md)、[机器可读路线](PlayChecks.json)、[组件](ComponentChecks.txt)、[物理组件](PhysicsComponents.txt)、[场景](SceneChecks.json)、[网格缓冲](MeshBufferChecks.txt)、[跑跳](JumpGapCheck.json)、[游泳](SwimmingCameraChecks.json)、[实际地形切割](Excavation.json)。

## 可编辑结构

场景仍是 `Assets/Scenes/CoastalTemple.unity`。展开 `40_Tutorial` 编辑三段机关；背景地形在 `10_Geography_EDIT_TERRAIN_HERE`。V8 场景另存于 `Assets/Scenes/Archive/CoastalTemple_V8.unity`。

每段都是普通场景对象并提供 Prefab。运行时不生成关卡地形。三段配置、交互、光路、移动、光桥实体状态、排水、奖励和界面均可分别调整。更多见[玩法与编辑说明](../CoastalV9Routes.md)及[模块结构与 API](../CoastalV9Architecture.md)。

切口处理保留原 Mesh/TerrainData 备份和恢复清单；移动桥的位置不会自动移动旧切口，需显式恢复后再切。网格显示复核还发现 Unity 原地序列化复制 Mesh 后可能保留旧显示缓冲，因此编辑工具已改为显式重写网格缓冲，并完成显卡数据读回检查及最终场景画面复核，保证显示与碰撞一致。

本版仍是白模。光门传递光线，提灯仅负责普通照明；机关状态保留到本次游玩结束，没有跨次游玩的存档。

## Windows 试玩交付

`Builds/CoastalRoutes-Windows.zip`（约 61.3 MB）包含可执行程序、必要数据和中文 `开始试玩.txt`。解压整个目录后运行 `CoastalTemple.exe`。

- [构建结果](BuildChecks.json)：Windows 64 位构建成功，0 错误、486 条警告。
- [独立程序启动检查](WindowsSmoke.json)：单独记录进程持续运行、响应、显卡初始化及日志检查；范围是启动检查，未完成 Windows 全流程人工试玩。
- [打包检查](PackageChecks.json)：216 个文件，压缩包内容、CRC 与中文说明核对通过，并记录 SHA-256。

Unity 编辑器另外出现过 Package Manager 窗口的 `path` 参数异常，与本次玩法调用栈无关；不能把构建成功表述成整个编辑器从未报错。
