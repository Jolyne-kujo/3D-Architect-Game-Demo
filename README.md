# 3D Architect

Unity 6 海岸遗迹解谜项目。源码、模型、场景和可复用机关在 GitHub 统一维护；仓库目前按组员查看需求临时公开，提交修改仍需协作者权限。

- 仓库：<https://github.com/Jolyne-kujo/3D-Architect-Game-Demo>
- Unity 编辑器：**6000.5.9f1**，以 `ProjectSettings/ProjectVersion.txt` 为准。
- 渲染管线：URP；依赖版本由 `Packages/manifest.json` 和 `Packages/packages-lock.json` 共同记录。
- 默认关卡：`Assets/Scenes/CoastalTemple.unity`。
- 机关实验场：`Assets/Scenes/MechanismPlayground.unity`。
- 独立水系统实验：`Assets/Scenes/WaterCourtyard.unity`（原 SampleScene 保留）。

## 组员直接下载完整工程 ZIP

**[下载完整 Unity 工程 ZIP](https://github.com/Jolyne-kujo/3D-Architect-Game-Demo/releases/latest/download/3D-Architect-UnityProject.zip)** · [发布页与更新说明](https://github.com/Jolyne-kujo/3D-Architect-Game-Demo/releases/latest)

1. 下载发布附件 `3D-Architect-UnityProject.zip`，完整解压到一个新文件夹，不要覆盖自己正在修改的工程。
2. 安装 Unity Hub 和 **Unity 6000.5.9f1**。
3. 在 Hub 中选择 **Add / 添加 → Add project from disk / 从磁盘添加项目**，选中解压后的 `3D-Architect` 文件夹。这一层应同时包含 `Assets`、`Packages` 和 `ProjectSettings`。
4. 等待包下载和资源导入完成，再打开 `Assets/Scenes/CoastalTemple.unity`，点击 Play。切换测试场景时先停止 Play，再打开 `MechanismPlayground.unity`。

这是完整工程，不是 `.unitypackage`，不需要先新建工程再导入。ZIP 已包含模型、贴图等真实 Git LFS 资源；下载者无需安装 Git 或 Git LFS，也无需连接 Unity Cloud。首次导入仍需网络及自己的 Unity 许可证。

**请优先下载上面的发布附件。** GitHub 自动生成的 `Source code (zip)` / `Code → Download ZIP` 是否包含真实 LFS 文件取决于仓库设置，不能用几百字节的模型指针代替资源。

ZIP 适合查看、试改和提交美术文件。它不带 Git 历史，不能直接 Push；需要持续提交工程的成员请使用下方的 Git 克隆流程。临时通过文件交接时，资源与对应 `.meta` 必须一起交给负责人。

## 2026-10-02 更新

- 主地图起点换为等比放大 40 倍的“广场”，保留原建筑、倒柱、台阶、地面和材质。
- 蓝色小柱替换为激光发生器，红色机关替换为红光受光器；蓝板为固定镜、绿板为可旋转镜。
- 当前为第一人称，正常视野隐藏身体和双手，拾取提灯后只显示灯；操作机关时升高为俯视观察，继续按 E 操作，WASD / 空格 / Esc 返回第一人称。
- 加入组员神庙模块、固定与可旋转镜预制体、光路预览检查、可复用水体及近期海岛和长廊调整。
- 整套广场：`Assets/Prefabs/地形建筑/关卡片段/广场_新手关.prefab`；第一人称玩家：`Assets/Prefabs/Player/ExplorerFirstPerson.prefab`。

## 海岸神庙白模

打开 `Assets/Scenes/CoastalTemple.unity` 并 Play。当前默认第一人称；红色 X Bot 的完整身体用于动画和投影，不遮挡玩家视野。项目包含游泳、激光石幕、光桥、镜面反射与空间连接机关；完整机关集中在实验场测试。`Builds` 中的历史试玩包不代表当前场景版本。
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

实验水体预制体：`Assets/Prefabs/Water/WaterVolume.prefab`。支持编辑器预览、独立排水、游泳与浮力自动采样，水密度及折射率可调；见 [拖入使用说明](Documentation/WaterPrefab/README.md)。

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

持续协作推荐 Git 克隆；只想下载查看或试改时可直接使用本页顶部的完整工程发布 ZIP。首次导入需要联网下载 Unity 包，并使用团队成员自己的有效 Unity 许可证。

## 日常协作

在 `main` 上拉取最新内容，为每项任务创建 `feature/任务名` 或 `fix/任务名` 分支，完成后推送并发起 Pull Request。通过评审后合并；主分支保持可打开、可运行。

资源必须与对应 `.meta` 一起提交。场景和预制件保持文本序列化；图片、模型、音频等二进制资源由 Git LFS 管理。`Library`、`Temp`、`Logs`、`UserSettings` 和构建输出不上传。

完整操作、合并冲突处理、macOS/Linux 配置和权限管理见 [团队协作说明](Documentation/Collaboration.md)。
