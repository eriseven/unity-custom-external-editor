# Rider 2026.2.4.1 隔离集成验收

日期：2026-11-06。发布版本：`0.3.0-preview.3`。平台：Windows。

## 结论和范围

真实 Rider 桌面客户端已在 Unity 6000.5.9f1 验证核心高级集成；不是仅运行模拟协议客户端。整个测试没有写入或恢复 Unity 原生偏好。两个工程同时运行、其中一个被强制终止时，另一个工程的连接与原生全局编辑器值保持独立。

该实现依赖经过审查的私有 EditorPlugin 二进制，仍保持实验状态并默认关闭。自动生成/更新解决方案、Profiler、测试运行器、其他操作系统及未列出的 Rider 功能未在本轮验收。

## 二进制审查

| 项目 | 值 |
| --- | --- |
| Rider 产品版本 | 2026.2.4.1 |
| Build | 262.11879.119 |
| DLL | JetBrains.Rider.Unity.Editor.Plugin.2022.3.Net46.Repacked.dll |
| SHA-256 | `F76CD56788833A2395C3D8FB2E0B343D4BC77BF75862112A0C67C5187092AA5F` |
| MVID | `b7ba61a5-5b76-4fad-9446-0c39c239526e` |
| 原生 EditorPrefs 调用 | 17 处，全部改写为本工程偏好封装 |
| SyncSolution 入口 | 2 处，均替换为本插件资源刷新 |

通过已安装 DLL 的元数据、方法 IL 和全程序集调用扫描审查。注册表相关调用在路径定位代码中仅为读取；反射调用官方 Rider 包的解决方案同步路径，其调用入口已替换。适配器同时校验原始哈希、17 处偏好重定向及两个同步入口数量；未知二进制拒绝加载，不使用模糊版本匹配。

原安装 DLL 在适配、调试和退出后仍为同一哈希。适配字节只载入当前 Unity 进程内存，不保存或随包分发。旧版 2026.2.3.1 的已审查哈希保留，本轮没有重新安装或运行旧版客户端。

## 验证矩阵

| 验证 | Unity 2022.3.62f3 | Unity 6000.5.9f1 |
| --- | --- | --- |
| Harmony 2.2.0 在场时编译、加载 | 通过 | 通过 |
| DLL 适配、偏好隔离、拒绝未知 DLL、停止服务 | 16 项断言通过 | 16 项断言通过 |
| 独立启动回归 | 60 项断言通过 | 60 项断言通过 |
| 真实子进程参数及环境 | 3 组通过 | 3 组通过 |
| RD TCP 握手、状态 RPC、Console、Pause | 通过 | 通过 |
| Rider 桌面连接及 Unity Console | 未重复桌面验收 | 通过 |
| Rider 工具栏 Play / Pause / Resume / Stop | 未重复桌面验收 | 通过 |
| Play 域重载、脚本编译后自动重连 | 未重复桌面验收 | 通过 |
| 附加调试、命中断点、单步及变量查看 | 未重复桌面验收 | 通过 |

实际调试：在 `IntegrationFixture.Start` 的 `var answer = 42;` 行命中绑定断点；F10 后执行位置移到下一行 `Debug.Log`，Rider 显示 `answer: 42`，随后恢复执行并分离调试器。Unity 的调试提示选择了 **Enable debugging for this session**，没有选择影响全部工程的选项。

并发/崩溃验证：Unity 2022 服务端口为 4144，Unity 6 为 13496（端口为当次运行值）。第三个不安装本包的 Unity 2022 工程读取全局编辑器偏好，检查通过；核对测试进程命令行后强制终止 Unity 2022，再运行无插件探针仍通过。Unity 6 的真实 Rider 客户端随后再次成功进入 Play，并完成重连和 Stop。最后由桌面观察器正常退出 Unity 6，记录 `finished global_preference_unchanged=true`。

全局编辑器基线是机器原有的 Rider 路径，**本轮没有将其改为 VS Code**。因此，真实“全局 VS Code + 本工程 Rider”组合未在本轮重新构造；隔离证据来自适配后的完整 IL 检查、工程偏好写入测试、持续原生偏好比对，以及无插件独立进程在并发/崩溃前后的读取。

观察器检查七项原生偏好：`kScriptsDefaultApp`、`com.jetbrains.rider.modifiedsourcefile`、`Rider_SelectedLoggingLevel`、`Rider_LogEventsCollectorEnabled`、`ScriptCompilationDuringPlay`、`kAutoRefresh`、`kAutoRefreshMode`。它是被动观察器，不构造 RD 对端、不代替 Rider 发送控制命令。断点和单步结果来自桌面界面观察，不能由观察器的布尔字段单独证明。

## 复现桌面验收

只使用可丢弃的临时 Unity 工程。`Start` 会创建空场景并保存为 `Assets/RiderValidation.unity`，修改该临时工程的插件配置。

1. 安装本包，复制 `Tests~/Editor` 为 `Assets/ProjectScriptEditorTests`，复制 `Tests~/IntegrationFixture.cs` 到 `Assets`，准备正确引用 Unity 程序集的根目录 `.sln/.csproj`。
2. 设置 `PROJECT_SCRIPT_EDITOR_RIDER` 为经过审查的 Rider 可执行文件路径。重复测试前删除该临时工程的 `Library/rider-desktop-state.json`、`Library/rider-desktop-events.txt`、`Library/rider-desktop-finish`、`Library/rider-desktop-recompile`（若存在）。
3. 用 Unity 可执行文件打开工程，附加以下参数；不要添加 `-batchmode`、`-quit` 或 `-riderIntegrationTests`。这样验证的是正常 GUI 域重载启动路径。

```text
-projectPath "<temporary-project>" -isolatedRiderDesktopTests
-executeMethod Avg.ProjectScriptEditor.Tests.RiderDesktopTests.Start
-logfile "<absolute-log-path>"
```

4. 用 Rider 打开临时解决方案；若出现信任提示，由使用者核对工程后手动确认。在 Rider 的 Unity 工具窗口确认 `ISOLATED_RIDER_DESKTOP_CONSOLE` 消息，并操作 Play、Pause、Resume、Stop。
5. 在工程 `Library` 中创建空文件 `rider-desktop-recompile`，触发脚本编译，确认自动重连和新的 Console 标记。观察器的状态及事件分别保存在 `rider-desktop-state.json`、`rider-desktop-events.txt`。
6. 在测试脚本第 9 行设断点，附加 Unity Editor 调试器，进入 Play，命中后 F10，检查局部变量为 42。恢复、停止 Play 并分离调试器。
7. 在 `Library` 创建 `rider-desktop-finish`。观察器要求四种控制动作和重连已发生，检查原生偏好后停止工程服务并退出。另需人工确认 Console、断点、变量和预期工程，不能把正常退出单独当作全部桌面功能通过。

其余自动化协议、并发及无插件探针入口见根目录 README。原始运行日志保留在本地 `.validation`，不提交带有机器路径和环境信息的整份 IDE/Unity 日志。
