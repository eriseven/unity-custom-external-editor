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

推荐固定到 `v0.1.1` 版本：

```text
https://github.com/eriseven/unity-custom-external-editor.git#v0.1.1
```

Unity 所在机器需要能运行 Git。仓库为公有仓库，可直接通过上述 HTTPS URL 安装，无需 GitHub 认证。

也可以通过 **Add package from disk** 选择本目录的 `package.json` 进行本地试用。不要同时安装旧版单文件脚本；先删除旧的 `Assets/Editor/ProjectScriptEditorSettings.cs`，避免两套恢复逻辑互相干扰。

## 使用

1. 打开 **Edit → Project Settings → Project Script Editor**，也可用 **Tools → Project Script Editor → Settings...**。
2. 从 **Detected editors** 选择已安装编辑器，或用 **Browse...**、粘贴绝对路径。macOS 选择 `.app` bundle。**Use Current Editor** 可填入 Unity 当前选择。
3. 点击 **Save and Enable**。插件会在当前窗口有焦点时暂用该选择；启动、脚本重载和重新获得焦点后也会应用。
4. 窗口失焦、正常关闭、禁用覆盖或脚本重载前，插件会还原进入本次覆盖前的全局选择。需要单独指定编辑器的其他工程也可分别安装、配置。

编辑器发现依赖工程中已有的 IDE 集成包。若下拉列表为空，仍可浏览选择路径；Rider、Visual Studio 等的项目生成、调试支持仍由各自的 Unity 集成包提供。

更换编辑器时，在插件设置页选择新路径，再点击 **Save and Enable**，无需先禁用。若要在 Unity 原生 **Preferences → External Tools** 中修改，则先点击 **Disable Override**，修改后再用 **Remember Current Editor** 保存，避免焦点恢复把旧值写回。

**Apply Saved Editor** 可手动重新应用。**Disable Override** 停止自动应用、保留工程保存的路径，并归还本次覆盖前的全局选择。如果用户或其他工程期间已经改成另一个值，插件不会覆盖这个新值。卸载导致脚本重载时也会先归还当前覆盖。

## 从 v0.1.0 升级

v0.1.0 只切换全局偏好，没有归还旧值。因此，使用 Rider 的 B 工程退出后，没有安装插件的 A 工程可能仍读到 Rider。v0.1.1 修复了正常退出、失焦、禁用及脚本重载的还原流程。

更新包 URL 的版本后，先关闭 B，在 A 的 **Preferences → External Tools** 中手动选回 VS Code 一次，然后关闭 A，再打开 B。旧版从未保存 A 原来的选择，升级无法凭空恢复那个历史路径。B 保存在 `UserSettings` 中的 Rider 路径仍然可用，无需重新配置。

预期顺序：A（无插件）使用 VS Code → 关闭 A → B（有插件）获得焦点后使用 Rider → 正常关闭 B → 再开 A，仍是 VS Code。

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

Unity 的编辑器选择仍是用户级共享偏好，本插件通过官方 `CodeEditor.Editor.SetCodeEditor` API 临时切换。备份使用进程内 `SessionState`，重复应用不会把工程自己的覆盖值误当成原值；脚本重载可以读取同一备份。插件不会持续轮询写入，也不会在批处理或 CI 中自动切换编辑器。

这仍不是底层完全隔离：B 获得焦点期间全局值暂时是 Rider；若 A 在此时启动、第三方插件并发写入，仍可能观察到临时值。Unity 崩溃、被强制结束或断电时，退出回调无法执行，可能留下覆盖值，需要手动恢复。不同进程碰巧写入相同的编辑器路径时，也无法仅凭这个共享值区分写入者。正常的失焦、关闭流程会还原，但不能承诺所有并发和异常退出场景下互不影响。

不支持将 **Open by file extension** 保存为覆盖值，因为它依赖系统文件关联。编辑器路径失效时会跳过恢复并提示一次；编辑器更新后请重新选择路径。只记住编辑器路径，不覆盖 External Tools 的启动参数、项目生成选项等其他设置。其他插件若同时修改编辑器选择，仍可能发生冲突。

## 验证

2026-10-17 在 Windows 上完成以下验证：

| Unity 版本 | 本地 Git URL 安装 | 冒烟测试 | 新进程持久化 |
| --- | --- | --- | --- |
| 2022.3.62f3 | 通过，UPM 来源为 Git | 40 项断言通过 | 3 项断言通过 |
| 6000.5.9f1 | 通过，UPM 来源为 Git | 40 项断言通过 | 3 项断言通过 |

两版均完成真实 Unity 导入与编译，设置页控件构建、USS 导入通过。未验证 macOS/Linux 桌面交互、真实 IDE 启动和两个工程窗口之间的焦点切换；远程仓库的网络与认证也不属于本地 Git 安装测试。

上表的常规测试不包含下面需要单独启用的真实全局偏好退出测试。

另已通过 Unity 6 的真实 `EditorPrefs` 回归：使用生产控制器验证应用、失焦还原、禁用还原、SessionState 备份恢复和实际进程退出，共 5 项断言。退出后，再启动未安装本包的 Unity 2022.3 工程，确认读取到的共享偏好与测试前一致。测试使用不会执行的占位编辑器路径，不启动真实 Rider 或 VS Code；测试前的原始偏好已还原。这项验证不等同于真实 IDE 启动或桌面双窗口焦点切换验收。

`Tests~/Editor` 包含可在临时工程中运行的独立冒烟测试，覆盖恢复条件、无效路径、IDE 集成异常、保存/禁用、UI 构建和跨进程持久化。Unity 忽略以 `~` 结尾的目录，因此测试不会自动导入用户工程，也没有 NUnit 依赖。

在一个全新的临时工程安装本包，再将 `Tests~/Editor` 复制到临时工程的 `Assets/ProjectScriptEditorTests`。使用 Unity CLI 顺序运行：

```powershell
unity run <temporary-project> --editor-version <installed-version> -- -executeMethod Avg.ProjectScriptEditor.Tests.SmokeTests.Run
unity run <temporary-project> --editor-version <installed-version> -- -executeMethod Avg.ProjectScriptEditor.Tests.SmokeTests.VerifyPersistence
```

常规测试通过注入回调和内存后端检查选择行为，不启动 IDE、不修改真实全局编辑器选择。测试会写临时工程自己的 `UserSettings`，不要在日常工程中运行。另有 12 项覆盖生命周期回归断言：A 的 VS Code 在 B 释放覆盖后保留、重复应用、更换路径、外部修改、缺省键、初始化异常和重载备份恢复。

`NativePreferenceTests.PrepareQuitRegression` 是单独的真实偏好退出测试，需先关闭其他 Unity 窗口，并显式设置进程环境变量 `PROJECT_SCRIPT_EDITOR_NATIVE_TEST=1`。它会短暂修改真实偏好、通过生产控制器的生命周期回调归还，在正常退出时写入 `native-preference-quit-passed.txt`。测试前的值备份在临时工程的 `native-preference-baseline.json`；若测试进程被强制终止，应按备份恢复原选择。常规测试不会调用这个入口。

跨版本的最后一步：把 `Tests~/NoPluginProbe.cs` 单独放入未安装本包的 Unity 2022.3 临时工程 `Assets/Editor`，设置环境变量 `PROJECT_SCRIPT_EDITOR_BASELINE` 为上述备份文件的绝对路径，执行 `NoPluginProbe.Verify`。它只读取全局偏好，确认 Unity 6 的 B 工程退出后，无插件的 A 工程仍读到原值。

手动验收：同时打开两个工程，分别保存不同编辑器；来回切换窗口并双击脚本，确认使用各自编辑器。再验证禁用、重启、脚本重载、编辑器路径失效，以及 IDE 的行号跳转。自动化测试不替代这些桌面交互检查。

实现参考：[Unity CodeEditor 源码](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/CodeEditor/CodeEditor.cs)、[focusChanged](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/EditorApplication-focusChanged.html)、[UPM Git dependencies](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-git.html)。
