# Project Script Editor

为每个 Unity 工程独立启动 Rider 或 VS Code。v0.2.0 不读写 Unity 的全局编辑器偏好，不需要在失焦、关闭或崩溃后还原设置。

- UPM 包名：`com.avg.project-script-editor`
- Unity 2022.3 及以上；仅在 Editor 中加载。
- 默认关闭。配置保存在工程自己的 `UserSettings`，不写入团队共享的 `ProjectSettings`。
- 通过脚本打开回调和插件自己的 Open Project 命令启动 IDE。

## 安装

在 **Window → Package Manager → + → Add package from git URL** 中输入：

```text
https://github.com/eriseven/unity-custom-external-editor.git#v0.2.0
```

[仓库](https://github.com/eriseven/unity-custom-external-editor)为公有仓库。Unity 所在机器需要安装 Git。也可通过 **Add package from disk** 选择仓库根目录的 `package.json`。

先删除旧版单文件脚本 `Assets/Editor/ProjectScriptEditorSettings.cs`，避免它继续修改全局偏好。

## 使用

1. 打开 **Edit → Project Settings → Project Script Editor**，或 **Tools → Project Script Editor → Settings...**。
2. 选择 **Rider** 或 **VS Code**，填写编辑器可执行文件的绝对路径，也可用 **Browse Editor...** 选择。
3. 点击 **Save and Enable**。双击脚本时会使用此工程的选择。
4. 要单独打开整个工程，使用 **Tools → Project Script Editor → Open Project**，或设置页的 **Open Project**。

Windows 选择 Rider 的 `rider64.exe` 或 VS Code 的 `Code.exe`，不支持 `.cmd` / `.bat`。macOS 可填写 `.app` 路径；Linux 填写可执行启动器路径。**Automatic** 按常见文件名识别 Rider / VS Code；自定义文件名请手动指定类型。

Rider 可在 **Rider solution (optional)** 指定已有 `.sln` / `.slnx`，支持绝对路径及相对工程根目录的路径。留空时优先选择与工程同名的解决方案，其次选择根目录唯一的解决方案；有多个候选时提示手动指定，没有时打开工程文件夹。VS Code 总是打开当前工程文件夹。

更换编辑器后重新 **Save and Enable** 即可。**Disable** 保留本地路径并停止接管脚本打开，此后遵循 Unity 原有行为。路径失效或启动失败时提示错误，不回退到全局编辑器。

## 隔离范围

例如 A 工程不安装插件，在 Unity Preferences 中选择 VS Code；B 工程安装插件，在插件设置页选择 Rider。B 的保存、打开脚本、禁用、重载、关闭均不修改 A 使用的全局选择。即使两个工程同时运行或 B 被强制结束，也没有插件写入的共享偏好需要恢复。

插件不调用 `CodeEditor.SetCodeEditor`、`EditorPrefs`、IDE 注册或初始化接口，不依赖窗口焦点和退出回调。旧版“临时切换再还原”的逻辑已完全移除。每次启动都携带当前工程的目录或解决方案，并在该工程目录下启动进程。

请注意使用入口：

| 入口 | 行为 |
| --- | --- |
| Project 中双击支持的代码资源 | 使用本工程配置 |
| 通过 Unity 资源打开回调跳转到脚本 | 使用本工程配置，并传递有效的行号、列号 |
| 插件自己的 Open Project | 使用本工程配置 |
| Unity 原生 Preferences → External Tools | 仍显示并控制 Unity 全局设置 |
| Unity 原生 Open C# Project、绕过资源回调的第三方命令 | 仍遵循原有实现 |

接管的扩展名：`.cs`、`.shader`、`.compute`、`.hlsl`、`.cginc`、`.glsl`、`.glslinc`、`.uxml`、`.uss`、`.asmdef`、`.asmref`、`.raytrace`。支持将 Git/registry 包中的虚拟资源路径解析为磁盘路径。批处理时不自动启动 IDE。

此包只负责启动，不生成 `.sln` / `.csproj`，不初始化 Rider/VS Code 的 Unity 集成或自动配置调试。需要完整项目上下文时，请另行准备解决方案及对应 IDE 支持。启动请求成功也不代表 IDE 已加载完毕；异步启动后的 IDE 自身错误请查看 IDE 日志。

隔离承诺针对本插件的行为。其他插件、旧版本仍在运行的 Unity 进程、用户在原生 Preferences 中的操作，仍可能修改 Unity 的共享设置。IDE 自身是否复用窗口由其行为和设置决定；本插件不传递 VS Code 的 `--reuse-window`。

## 从 v0.1.x 升级

v0.2.0 是启动方式的变更。升级前先禁用旧版覆盖并正常关闭旧版工程，避免旧进程继续运行全局切换逻辑。原本保存的启用状态和编辑器路径继续保留，新增类型默认为 Automatic；Rider / VS Code 的常见路径可自动识别，其他编辑器需重新选择受支持类型和路径。

如果旧版已经把 A 的全局设置改成 Rider，请在 Unity 原生 Preferences 中手动选回 VS Code 一次。新版不会猜测或写回历史值，也不会自动修正此前被污染的全局偏好。此后 B 只需在插件设置页配置 Rider。

## 本地配置

配置文件为 `UserSettings/ProjectScriptEditorSettings.asset`。请在宿主 Unity 工程的 `.gitignore` 中加入（已忽略整个 `UserSettings` 时无需重复）：

```gitignore
/UserSettings/ProjectScriptEditorSettings.asset
```

包自身的忽略规则不能替代宿主工程规则。文件包含本机路径，不适合跨机器共享。

## 验证与复现

测试在 Windows 上使用 Unity 2022.3.62f3 和 6000.5.9f1。测试只读取真实全局编辑器偏好，不写入或还原它。测试会修改临时工程自身配置，请勿在日常工程中运行。

2026-10-17 的验证结果：

| 验证 | Unity 2022.3.62f3 | Unity 6000.5.9f1 |
| --- | --- | --- |
| 本地 Git URL 安装、导入与编译 | 通过 | 通过 |
| 冒烟测试 | 59 项断言通过 | 59 项断言通过 |
| 新进程读取配置 | 2 项断言通过 | 2 项断言通过 |
| 真实子进程参数、工作目录与环境 | 3 组通过 | 3 组通过 |

另已同时运行两个配置不同编辑器的测试工程，通过第三个无插件 Unity 2022 工程确认全局偏好不变；强制结束 Unity 6 测试进程后再次检查，仍保持原值，剩余工程正常退出。生产源码检查未发现全局偏好、IDE 集成注册或生命周期还原接口，测试包与发布源码一致（忽略 Git 换行格式）。IDE 启动由记录器或测试回调代替，未进行真实 Rider / VS Code 的桌面交互验收。

`Tests~` 不会自动导入用户工程。在临时工程安装本包，将整个 `Tests~/Editor` 目录（包括 `.asmdef`）复制为 `Assets/ProjectScriptEditorTests`，依次执行：

```powershell
unity run <temporary-project> --editor-version <installed-version> -- -executeMethod Avg.ProjectScriptEditor.Tests.SmokeTests.Run
unity run <temporary-project> --editor-version <installed-version> -- -executeMethod Avg.ProjectScriptEditor.Tests.SmokeTests.VerifyPersistence
```

冒烟测试覆盖启动计划、路径与参数、Unity 资源打开回调、包资源解析、错误时不回退、保存/禁用、UI 构建和全局偏好保持不变。第二条命令验证新进程读取本地配置。

真实子进程参数测试使用 .NET 9 构建记录器，代替 IDE 捕获实际收到的参数、工作目录和环境变量：

```powershell
dotnet build Tests~/Recorder/Recorder.csproj -c Release -o .validation/Recorder
$env:PROJECT_SCRIPT_EDITOR_RECORDER = (Resolve-Path .validation/Recorder/Recorder.exe).Path
unity run <temporary-project> --editor-version <installed-version> -- -executeMethod Avg.ProjectScriptEditor.Tests.ProcessLaunchTests.Run
```

此测试覆盖两种 IDE 的启动参数及包含空格、引号、中文、反斜杠等内容的往返传递。进程通过 `UseShellExecute=false` 启动；子进程环境会移除从终端继承的 `ELECTRON_RUN_AS_NODE` 和 `VSCODE_IPC_HOOK_CLI`。

并发及异常退出测试：用两个不同版本的 Unity 二进制，以 `-batchmode -nographics -executeMethod Avg.ProjectScriptEditor.Tests.IsolationTests.Hold` 启动各自临时工程，**不加 `-quit`**。通过进程环境变量 `PROJECT_SCRIPT_EDITOR_TEST_KIND` 分别指定 `VSCode`、`Rider`，等待两个工程各自写出 `isolation-ready.txt`。测试持续检查真实全局偏好，并用注入的启动回调确认各工程选择自己的编辑器。

将 `Tests~/NoPluginProbe.cs` 单独复制到第三个无插件的临时工程 `Assets/Editor`；环境变量 `PROJECT_SCRIPT_EDITOR_BASELINE` 指向任一测试工程生成的 `isolation-baseline.json`，执行 `NoPluginProbe.Verify`。在两个测试进程仍运行时检查一次，再强制结束其中一个测试进程后检查一次。只终止明确识别的临时测试进程。最后在另一个测试工程根目录写入 `isolation-finish.txt` 使其正常退出；测试也会在超时后自行结束。

自动化测试不替代真实 Rider / VS Code 的桌面交互验收。macOS / Linux 的实际进程启动尚未验证。

实现参考：[Unity OnOpenAsset](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Callbacks.OnOpenAssetAttribute.html)、[Rider 命令行](https://www.jetbrains.com/help/rider/Opening_Files_from_Command_Line.html)、[VS Code 命令行](https://code.visualstudio.com/docs/configure/command-line)、[UPM Git 安装](https://docs.unity3d.com/2022.3/Documentation/Manual/upm-git.html)。
