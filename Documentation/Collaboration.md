# 团队协作说明

## 下载方式与成员权限

本项目使用个人账号 `Jolyne-kujo` 下的仓库，目前按组员查看需求临时公开：
<https://github.com/Jolyne-kujo/3D-Architect-Game-Demo>

公开期间可直接下载 [完整工程 ZIP](https://github.com/Jolyne-kujo/3D-Architect-Game-Demo/releases/latest/download/3D-Architect-UnityProject.zip)，无需 GitHub 账号。解压后在 Unity Hub 中添加同时包含 `Assets`、`Packages`、`ProjectSettings` 的目录，使用 Unity `6000.5.9f1` 打开；不要把它当作 `.unitypackage` 导入空项目。

只有仓库所有者及获邀并接受邀请的协作者可以推送修改。公开下载不代表有提交权限。若仓库恢复私有，网页和下载附件也需要获邀账号访问。

管理员从 **Settings → Collaborators → Add people** 输入团队成员的 GitHub 用户名发出邀请，管理员负责核对成员身份、邀请和移除。不要共享账号或个人访问令牌。Unity Cloud 成员邀请不会授予 GitHub 仓库权限。

完整工程 ZIP 已包含真实模型和贴图，不依赖下载者运行 Git LFS。ZIP 不包含 `.git` 和版本历史：查看、试改可用 ZIP，持续提交请用 Git 克隆。通过 ZIP 交接美术改动时，请把改动资源和 `.meta` 一起交给负责人，不要覆盖其他人的整个工程。

`main` + 功能分支 + Pull Request 是本项目的协作约定，不能把它当成已启用的服务器端强制分支保护。若后续需要组织 Team、细分角色或强制评审，可迁移至团队的 GitHub Organization，再按该账号套餐配置相应规则。

## 本机初始化

先安装 `ProjectSettings/ProjectVersion.txt` 指定的编辑器版本，以及 Git 和 Git LFS。每位成员使用自己的 Git 提交身份：

```sh
git config --local user.name "你的名字"
git config --local user.email "你的 GitHub 提交邮箱"
```

Windows 推荐运行 `Tools/Setup-Git.ps1`，用 `-UnityEditor` 指定当前版本的 `Unity.exe`。省略参数时脚本尝试 Unity Hub 默认目录和本项目原开发机的安装目录。该脚本需要每个新克隆执行一次，因为 `.git/config` 不随源码共享。

macOS/Linux 可在项目根目录手动配置：

```sh
git lfs install --local
git config --local core.autocrlf false
git config --local pull.ff only
git config --local merge.unityyamlmerge.name "Unity Smart Merge"
git config --local merge.unityyamlmerge.recursive binary
```

然后按本机安装位置设置驱动，例如 macOS：

```sh
git config --local merge.unityyamlmerge.driver '"/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/Tools/UnityYAMLMerge" merge -p %O %B %A %A'
```

Linux 使用该编辑器安装目录下的 `Editor/Data/Tools/UnityYAMLMerge`。路径含空格时保留双引号。驱动仅用于 `.unity` 和 `.prefab`；其他 YAML 和 `.meta` 使用普通文本合并。

## 一项任务的完整流程

切换分支或合并前先保存 Unity 场景，并关闭项目，避免编辑器自动写回旧状态。

```sh
git switch main
git pull --ff-only
git lfs pull
git switch -c feature/room-layout
```

修改后先在 Unity 中确认无编译错误、场景能运行，再保存并检查提交：

```sh
git status
git diff
git add Assets Packages ProjectSettings
git diff --cached --stat
git commit -m "feat: add room layout"
git push -u origin feature/room-layout
```

如同时修改文档或工具，另行 `git add` 对应路径。在 GitHub 发起目标分支为 `main` 的 Pull Request，说明改动、测试情况和场景影响。评审并合并后，本机切回 `main` 拉取最新版本。

## Unity 资源规则

- `Assets/`、`Packages/`、`ProjectSettings/` 必须一起版本控制；保留包锁文件。
- 每个资源及子文件夹的 `.meta` 都要提交。移动或重命名资源优先在 Unity 的 Project 窗口操作，保持 GUID 不变。
- **Version Control → Visible Meta Files** 和 **Editor → Asset Serialization → Force Text** 已在项目设置中启用。
- 避免多人同时编辑同一个大场景；按工作范围拆分预制件或附加场景，编辑前沟通负责人。
- LFS 管理图片、模型、音频和其他已在 `.gitattributes` 列出的二进制文件；不要手工编辑 LFS 指针。新增格式时补充属性规则后再提交。
- 不上传缓存、个人编辑器状态、密码、令牌、签名证书或构建目录。
- 不要擅自升级 Unity 或包版本。需升级时单独开分支并通知团队。
- 包来源须可被队友访问；不要提交指向个人磁盘的 `file:` 绝对路径依赖。

## 冲突与资源缺失

遇到文本冲突，解决后在 Unity 里重新打开场景检查引用。Unity Smart Merge 可协助合并场景和预制件，但仍需验证结果。二进制资源冲突由负责人选定正确版本，不能通过拼接解决。

如果贴图显示为短文本或资源缺失，先确认已接受仓库邀请并正确登录，再执行：

```sh
git lfs pull
git lfs fsck
git lfs ls-files
```

这些命令失败时先解决认证、网络或 LFS 配额问题，再打开 Unity。不要通过删除 `.meta` 修复资源引用。

## 参考

- [GitHub 私有仓库协作者权限](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/repository-access-and-collaboration/permission-levels-for-a-personal-account-repository)
- [Unity 6 版本控制设置](https://docs.unity3d.com/cn/6000.0/Manual/Versioncontrolintegration.html)
- [Unity Smart Merge](https://docs.unity3d.com/cn/6000.0/Manual/SmartMerge.html)
