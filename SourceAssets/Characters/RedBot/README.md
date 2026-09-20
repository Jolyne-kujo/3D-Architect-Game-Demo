# 红色 X Bot 原始模型备份

`Slow Run.dae` 从用户提供的 `Assets/Animations/Character/Slow Run.zip` 解压，已通过 ZIP CRC 和 SHA-256 校验。保存在 Assets 之外，避免 Unity 重复导入同一个角色。Unity 使用用户同时提供的七个 FBX 文件。

该 ZIP 实际只有 `Slow Run.dae`，没有 texture 文件夹或图片；FBX 与 DAE 也未引用外部贴图。角色使用纯色材质：`Beta_Surface` 对应红色 `Beta_HighLimbsGeoSG3`，`Beta_Joints` 对应深红色 `Beta_Joints_MAT1`。不需要贴图即可还原红色 X Bot。

慢跑 FBX 包含完整蒙皮模型；快跑、待机、跳跃、Lifting、Swimming 和 Treading Water 文件只有动画与同名骨架，这是正常的 without-skin 导出。原始文件哈希、压缩包成员信息和材质数值见 `Documentation/RedBotReplacement/SourceInventory.json`。

这些文件由用户提供；本次备份不改变原资源的授权条件。
