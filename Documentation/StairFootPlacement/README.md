# 斜坡楼梯与独立脚部落点

2026-09-20，Unity 6000.5.9f1。

后续修复了高帧率投影台阶卡住、深水脚底接触压过游泳、水面编辑预览与池岸缝隙；当前行为和新增验证见 [水中行走与预览说明](../WaterTraversal/README.md)。下文原始 50 FPS 检查不能单独作为高帧率通过的证据。

## 备份和试玩版本

- 修改前完整项目：私有 GitHub `Jolyne-kujo/3D-Architect-Game-Demo`，`main` 提交 `6f59ea4971e2a689972e93ce49486b3a1e7f8d12`。
- 固定回退标签：`backup/pre-stair-foot-ik-2026-09-20`。源代码、场景、模型、贴图、动画、meta 和文档均在备份内；Unity 自动生成的 Library/Temp 等缓存不上传。
- 本次修改分支：`codex/stair-ramp-foot-ik`。主地图和实验场同时更新。
- 回退时先停止 Play、保存自己新做的改动并关闭 Unity，再切到上述标签或备份提交后重新打开项目。不要直接覆盖尚未保存的场景。

## 当前行为

楼梯外观仍是逐级台阶，但身体碰撞使用一条从地面起始的连续斜坡。原入口 0.30m 竖直碰撞面已去掉；主地图原先低于池岸 0.30m 的楼梯顶端也已与岸面对齐；不再使用楼梯专用速度和上下楼动画，WASD/Shift/Space 与普通地面一致。两张当前地图中的楼梯已经解除预制件连接，可以直接编辑外观和斜坡。旧 Staircase prefab 的 GUID 留给归档地图与旧编辑工具兼容，不再要求用它搭建关卡。

角色主体继续使用 CharacterController；左右脚分别检测鞋底和脚尖，通过 Humanoid IK 单独调整脚、膝盖和髋部。没有增加会互相挤压身体或推动机关的腿部刚体。脚底 Ray/Sphere 查询和原生胶囊下方接触共同决定落地，修复薄板边缘明明接触却被判悬空、前进速度被削掉的问题。低台阶继续由原生 Step Offset 自动跨越；水面游泳时，如果脚前方确有可站立、头部空间足够的低台阶，也允许自动跨步，随后切回站立，不触发攀爬动画。

现有 RedBot Humanoid 和慢跑/快跑动画已足够，不需要新增动画文件。跳跃、游泳及高台攀爬时停用脚部 IK；传送/重生清掉旧脚部偏移。该方案是低成本的程序化脚部修正，不是整条腿每个部位的精确物理碰撞，也不是完整动作匹配系统。

## 可调参数

- 玩家 `CharacterController > Step Offset`：自动跨步高度上限，当前 0.35m。0.60m 障碍仍需跳跃，且不会错误触发低处攀爬。
- 红色模型 Animator 所在物体的 `GroundFootIK`：`Maximum Lift` 0.38m、`Maximum Drop` 0.22m、`Sole Clearance` 0.018m、`Toe Reach` 0.12m、`Settle Speed` 3。
- 楼梯的 `Smooth walk collision - keep with stairs` 是身体碰撞，`Editable visual steps` 是可编辑的台阶外观。外观台阶参与脚部落点计算。

## 验证

使用 Unity 原生物理和实际模型/场景测试，报告和截图保存在本目录：

- `MovementBefore.txt` / `MovementChecks.txt`：7 项斜坡、旋转缩放、薄板、较高障碍、低顶棚和下坡测试；旧版本复现入口竖直面和 Shift 无效。
- `MechanismPlaygroundChecks.txt`：实际楼梯步行/跑步、实际水池四块投影台阶、左右脚相差 20cm 的落点，以及跳跃、游泳、重生时 IK 的切换。
- `CoastalTempleChecks.txt`：主地图真实楼梯双向通行和跑步。
- `../WaterExit/RegressionChecks.txt`：水下着地、低台阶、抓边、消失/移动平台、低顶棚；水面抓边边界增加 2mm 浮点容差，避免精确处于允许高度的台面因射线端点舍入漏检。

自动测试的输入直接调用与键盘共用的运动入口；截图来自 Unity 实际骨骼求解后的网格。未把自动测试表述为完整人工手感验收。旧的 Package Manager `path ... undefined` 编辑器报错与本次改动无关。

实现依据：[Unity CharacterController](https://docs.unity3d.com/6000.0/Documentation/Manual/class-CharacterController.html)、[Humanoid OnAnimatorIK](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/monobehaviour/onanimatorik)、[脚底高度](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Animator-leftFeetBottomHeight.html)。
