# 3D Architect

Unity 6 团队开发项目。源码和资源保存在私有 GitHub 仓库，供获得访问权限的团队成员协作。

- 仓库：<https://github.com/Jolyne-kujo/3D-Architect-Game-Demo>
- Unity 编辑器：**6000.5.9f1**，以 `ProjectSettings/ProjectVersion.txt` 为准。
- 渲染管线：URP；依赖版本由 `Packages/manifest.json` 和 `Packages/packages-lock.json` 共同记录。
- 默认关卡：`Assets/Scenes/CoastalTemple.unity`。
- 独立水系统实验：`Assets/Scenes/WaterCourtyard.unity`（原 SampleScene 保留）。

## 海岸神庙白模

打开 `Assets/Scenes/CoastalTemple.unity` 并 Play。当前角色为红色 X Bot，使用新待机、慢跑、快跑、跳跃动画，并叠加右手举灯姿态。默认第三人称跟随视角，鼠标环绕观察，角色朝移动方向转身；包含游泳、激光石幕、光桥及空间连接教学。`Builds` 中的历史试玩包不代表当前场景版本。
当前采用暖白色岩壁与建筑、淡土黄色地表和沙滩，已去除山体彩色贴图。[当前地形与截图](Documentation/CoastalV7Verification/README.md)。
从沙滩岸边进入露天遗迹，经半山观海平台到达大型神庙。**WASD** 慢跑/游泳，**Shift** 快跑，**空格** 跳跃/上浮，**Ctrl** 下潜，**E** 交互，**H** 查看线索，**L** 切换已获得的提灯，**Esc** 释放鼠标，**Home** 回到起点。普通玩法关闭了全景切换快捷键。触碰黄色浮标所在的近远海交界线会回到出生点。

## 资源与代码位置

`Assets` 按用途分为 `Animations`、`Materials`、`Objects`、`Prefabs`、`Scenes`、`Scripts`、`Settings`、`Shaders`、`Textures`。所有代码统一在 `Assets/Scripts`；人物相关代码在 `Gameplay/Player`，移动与游泳控制在 `WaterDemo/Runtime/CourtyardWalker.cs`。

完整目录表和常用文件入口见 [Assets 导航](Assets/README.md)。慢跑、快跑文件在 `Assets/Animations/Player/Locomotion`，原始 DAE 在 `SourceAssets/Characters/Mixamo`。资源迁移记录与验证见 [本次整理说明](Documentation/AssetOrganization/README.md)。

地形模块、Terrain 与建筑的编辑方法见 [海岸关卡说明](Documentation/CoastalTemple.md)。海面采用 GPU 叠加波，近岸另有流体模拟，参数见 [海面说明](Documentation/CoastalV3Verification/README.md)。各历史版本的验证报告保留在 Documentation 下。

## 水系统白模实验

打开实验场景并 Play；或运行本机 `Builds/WaterCourt-Windows/WaterCourt.exe`。
按 **1** 开启光源，水下接收器持续受光 1.5 秒后锁定排水；按 **Tab** 进入庭院，**WASD** 行走，沿西侧楼梯下池。**R** 重置，**V** 查看流速，**2** 扰动水面。

这是有水量、流量和可变形网格的高度场水体原型。它不包含三维液体翻卷、飞溅和任意洞穴流动。复用方法、边界与测试见 [水系统说明](Documentation/WaterSystem.md)。

选中水体的 `WaterVolume` 组件，调节 **Drain Speed Multiplier**（代码变量 `drainSpeedMultiplier`）。默认 **4**，当前实验池约 **50 秒**露出池底；设为 **1** 恢复原始速度。只加速开闸后的水模拟，不改变角色或全局时间。

## 团队成员首次接入

1. 由仓库所有者在 **Settings → Collaborators → Add people** 邀请你的 GitHub 账号，接受邀请后再克隆。
2. 安装 Git、Git LFS、Unity Hub，以及项目指定的 Unity 编辑器版本；使用自己的 GitHub 账号登录 Git。
3. 执行：

```sh
git lfs install
git clone https://github.com/Jolyne-kujo/3D-Architect-Game-Demo.git
cd 3D-Architect-Game-Demo
git lfs pull
```

4. Windows 下在项目根目录运行协作初始化脚本。Unity 安装位置不同，请传入实际的 `Unity.exe` 路径：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\Setup-Git.ps1 -UnityEditor "E:\Unity\6000.5.9f1\Editor\Unity.exe"
```

脚本仅设置当前仓库的 Git LFS、换行符、拉取策略和 Unity 场景/预制件合并驱动，不修改全局 Git 配置。

5. 在 Unity Hub 中选择 **Add → Add project from disk**，选中克隆目录，等待依赖还原和首次导入。

请通过 Git 克隆获取完整资源，不要把 GitHub 的 ZIP 下载作为默认接入方式。首次导入需要联网下载 Unity 包，并使用团队成员自己的有效 Unity 许可证。

## 日常协作

在 `main` 上拉取最新内容，为每项任务创建 `feature/任务名` 或 `fix/任务名` 分支，完成后推送并发起 Pull Request。通过评审后合并；主分支保持可打开、可运行。

资源必须与对应 `.meta` 一起提交。场景和预制件保持文本序列化；图片、模型、音频等二进制资源由 Git LFS 管理。`Library`、`Temp`、`Logs`、`UserSettings` 和构建输出不上传。

完整操作、合并冲突处理、macOS/Linux 配置和权限管理见 [团队协作说明](Documentation/Collaboration.md)。
