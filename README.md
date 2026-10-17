# Project Script Editor

为不同 Unity 工程记住不同的 External Script Editor，例如工程 A 使用 Rider，工程 B 使用 VS Code。

- UPM 包名：`com.avg.project-script-editor`
- 最低 Unity 版本：2022.3；仅在 Editor 中加载，不进入玩家构建。
- 默认关闭；首次安装不会改变编辑器选择。
- 配置按工程、按本机用户保存，不写入团队共享的 `ProjectSettings`。

## 安装

GitHub 仓库：<https://github.com/eriseven/unity-custom-external-editor>。`package.json` 位于仓库根目录。

在 Unity 的 **Window → Package Manager → + → Add package from git URL** 中输入仓库 URL，例如：

```text
https://github.com/eriseven/unity-custom-external-editor.git
```

推荐固定到 `v0.1.0` 版本：

```text
https://github.com/eriseven/unity-custom-external-editor.git#v0.1.0
```

Unity 所在机器需要能运行 Git。仓库为公有仓库，可直接通过上述 HTTPS URL 安装，无需 GitHub 认证。

也可以通过 **Add package from disk** 选择本目录的 `package.json` 进行本地试用。不要同时安装旧版单文件脚本；先删除旧的 `Assets/Editor/ProjectScriptEditorSettings.cs`，避免两套恢复逻辑互相干扰。

## 使用

1. 打开 **Edit → Project Settings → Project Script Editor**，也可用 **Tools → Project Script Editor → Settings...**。
2. 从 **Detected editors** 选择已安装编辑器，或用 **Browse...**、粘贴绝对路径。macOS 选择 `.app` bundle。**Use Current Editor** 可填入 Unity 当前选择。
3. 点击 **Save and Enable**。插件会在当前窗口有焦点时应用选择，并在启动、脚本重载和重新获得焦点后恢复。
4. 在其他工程中分别安装、保存各自的选择。

编辑器发现依赖工程中已有的 IDE 集成包。若下拉列表为空，仍可浏览选择路径；Rider、Visual Studio 等的项目生成、调试支持仍由各自的 Unity 集成包提供。

更换编辑器时，在插件设置页选择新路径，再点击 **Save and Enable**，无需先禁用。若要在 Unity 原生 **Preferences → External Tools** 中修改，则先点击 **Disable Override**，修改后再用 **Remember Current Editor** 保存，避免焦点恢复把旧值写回。

**Apply Saved Editor** 可手动重新应用。**Disable Override** 停止自动恢复并保留保存的路径；它不会还原某个历史全局选择。卸载插件也不会改变 Unity 当前的全局选择。

## 本地配置与限制

配置保存为：

```text
UserSettings/ProjectScriptEditorSettings.asset
```

请在使用本插件的每个 Unity 工程根目录的 `.gitignore` 中加入以下规则（如果已忽略整个 `UserSettings`，无需重复）：

```gitignore
/UserSettings/ProjectScriptEditorSettings.asset
```

包自身的 `.gitignore` 无法代替宿主工程的忽略规则。配置包含个人绝对路径，不适合跨机器共享。

Unity 的编辑器选择仍是用户级共享偏好，本插件通过官方 `CodeEditor.Editor.SetCodeEditor` API 自动恢复，不能提供底层完全隔离。多个工程同时打开时，以获得焦点且启用了本插件的工程为准；未配置本插件的工程仍可能受共享偏好影响。插件不会持续轮询写入，也不会在后台工程、批处理或 CI 中切换编辑器。

不支持将 **Open by file extension** 保存为覆盖值，因为它依赖系统文件关联。编辑器路径失效时会跳过恢复并提示一次；编辑器更新后请重新选择路径。只记住编辑器路径，不覆盖 External Tools 的启动参数、项目生成选项等其他设置。其他插件若同时修改编辑器选择，仍可能发生冲突。

## 验证

2026-10-17 在 Windows 上完成以下验证：

| Unity 版本 | 本地 Git URL 安装 | 冒烟测试 | 新进程持久化 |
| --- | --- | --- | --- |
| 2022.3.62f3 | 通过，UPM 来源为 Git | 28 项断言通过 | 3 项断言通过 |
| 6000.5.9f1 | 通过，UPM 来源为 Git | 28 项断言通过 | 3 项断言通过 |

两版均完成真实 Unity 导入与编译，设置页控件构建、USS 导入通过。未验证 macOS/Linux 桌面交互、真实 IDE 启动和两个工程窗口之间的焦点切换；远程仓库的网络与认证也不属于本地 Git 安装测试。

`Tests~/Editor` 包含可在临时工程中运行的独立冒烟测试，覆盖恢复条件、无效路径、IDE 集成异常、保存/禁用、UI 构建和跨进程持久化。Unity 忽略以 `~` 结尾的目录，因此测试不会自动导入用户工程，也没有 NUnit 依赖。

在一个全新的临时工程安装本包，再将 `Tests~/Editor` 复制到临时工程的 `Assets/ProjectScriptEditorTests`。使用 Unity CLI 顺序运行：

```powershell
unity run <temporary-project> --editor-version <installed-version> -- -executeMethod Avg.ProjectScriptEditor.Tests.SmokeTests.Run
unity run <temporary-project> --editor-version <installed-version> -- -executeMethod Avg.ProjectScriptEditor.Tests.SmokeTests.VerifyPersistence
```

测试通过注入回调检查选择行为，不启动 IDE、不修改真实全局编辑器选择。测试会写临时工程自己的 `UserSettings`，不要在日常工程中运行。

手动验收：同时打开两个工程，分别保存不同编辑器；来回切换窗口并双击脚本，确认使用各自编辑器。再验证禁用、重启、脚本重载、编辑器路径失效，以及 IDE 的行号跳转。自动化测试不替代这些桌面交互检查。

实现参考：[Unity CodeEditor 源码](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/CodeEditor/CodeEditor.cs)、[focusChanged](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorApplication-focusChanged.html)、[UPM Git dependencies](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-git.html)。
