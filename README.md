# 3D Architect

Unity 6 团队开发项目。源码和资源保存在私有 GitHub 仓库，供获得访问权限的团队成员协作。

- 仓库：<https://github.com/Jolyne-kujo/3D-Architect-Game-Demo>
- Unity 编辑器：**6000.5.9f1**，以 `ProjectSettings/ProjectVersion.txt` 为准。
- 渲染管线：URP；依赖版本由 `Packages/manifest.json` 和 `Packages/packages-lock.json` 共同记录。
- 起始场景：`Assets/Scenes/SampleScene.unity`。

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
