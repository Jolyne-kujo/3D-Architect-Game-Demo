# V13 放松手型与低位摆臂

摄像机与世界身体/阴影的位置关系已由 [V14 修复](CoastalV14CameraShadow.md)更新；本页继续说明手部表现。

当前场景：`Assets/Scenes/CoastalTemple.unity`。空手时手指自然微曲，待机双臂低垂，行走时沿原下载动画的节奏交替摆动；双手的位置与手腕朝向已调低，不再维持双拳举在胸前的姿势。快走会增加摆动幅度。

保留 Quaternius 原模型、骨骼、蒙皮和行走动作，只增加了一个指部姿势层 `Relaxed Hands`。`RelaxedHands.anim` 用原人偶的握拳和张手姿势混合得到放松手型，不覆盖肩肘、步伐或位移。后面的 `Carried Lantern` 层可覆盖左手握持；进入游泳时淡出放松层，恢复原游泳手型。

后续调整在 `FirstPersonHands` 组件中进行：`walkingAim` 控制走路时手部取景，`restingAim` 控制待机位置，`movingAim` 控制快走取景，`importedSwing` 控制原动画摆臂的可见幅度，`relaxedWristRoll` 控制空手时的手腕朝向。进入腾空、游泳或持灯状态时平滑切换。移动惯性仍使用 V12 的设置。

已查看实际游戏帧：[待机](CoastalV13Verification/01-Idle.png)、[行走 A](CoastalV13Verification/02-Walk-A.png)、[行走 B](CoastalV13Verification/03-Walk-B.png)、[快走](CoastalV13Verification/04-Run-A.png)。图中灰色地面是检查期间临时创建的平面，不会留在主场景。

[动作与移动检查](CoastalV13Verification/MotionChecks.txt)与[第一人称回归检查](CoastalV13Verification/FirstPersonRegression.txt)保留独立记录。修改前的场景保存于 `Assets/Scenes/Archive/CoastalTemple_V12_BeforeRelaxedArms.unity`。
