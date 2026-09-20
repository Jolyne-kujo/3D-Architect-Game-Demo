# 用户提供的动作源文件

`Slow Run.dae` 与 `Fast Run.dae` 分别来自用户放入 Character 文件夹的两个 ZIP。文件已经完整解压并经过 ZIP CRC、字节长度和 SHA-256 校验，原 ZIP 已按要求删除。

项目使用用户同时提供的两个 FBX，位于 `Assets/Animations/Player/Locomotion`；DAE 原件放在 Assets 外，供后续编辑或转换，避免 Unity 重复导入同一动作。

动作文件的骨骼命名为 `mixamorig`。保留用户提供的源文件，不对其额外赋予其他许可。源文件与压缩包的对应关系及校验值见 `Documentation/AssetOrganization/MigrationPlan.json`。
