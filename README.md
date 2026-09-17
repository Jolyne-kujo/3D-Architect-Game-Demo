# 3D Architect

Unity 6 团队开发项目。源码和资源保存在私有 GitHub 仓库，供获得访问权限的团队成员协作。

- 仓库：<https://github.com/Jolyne-kujo/3D-Architect-Game-Demo>
- Unity 编辑器：**6000.5.9f1**，以 `ProjectSettings/ProjectVersion.txt` 为准。
- 渲染管线：URP；依赖版本由 `Packages/manifest.json` 和 `Packages/packages-lock.json` 共同记录。
- 默认关卡：`Assets/CoastalTemple/Scenes/CoastalTemple.unity`。
- 独立水系统实验：`Assets/WaterCourtyard/Scenes/WaterCourtyard.unity`（原 SampleScene 保留）。

## 海岸神庙白模

打开 `CoastalTemple` 场景并 Play；本机试玩包位于 `Builds/CoastalTemple-Windows/CoastalTemple.exe`。
从岸边进入露天遗迹，沿山路到达大型神庙。**WASD** 行走，**Shift** 快走，**空格** 跳跃，**Tab** 在行走和观察间切换，**F1/F2/F3** 查看全景、山脚和神庙，**Home** 回到起点。水池按 **1** 排水、**R** 重新蓄水。

山体采用原生 Terrain，建筑和山路采用 ProBuilder 网格并保存于场景/预制件中，Play 时不生成环境。编辑方法、模块与验证见 [海岸关卡说明](Documentation/CoastalTemple.md)。

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
