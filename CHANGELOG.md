# Changelog

## 0.3.0-preview.4 — 2026-11-06 (experimental)

- Discover installed Rider and VS Code/Insiders through read-only Windows installation records, known installation/Toolbox directories, and PATH. List multiple versions with version metadata and paths; deduplicate Rider launchers within one Windows installation.
- Add an Installed editors dropdown and Refresh Editors action. Selecting fills the executable and editor kind; only Save and Enable persists the project choice. Refreshing or missing installations never silently replaces saved or manually entered paths.
- Keep manual browsing and all global-preference isolation behavior. Discovery does not initialize IDE integrations or execute detected programs; Rider's advanced integration still requires an audited build.
- Add multi-version, stale/duplicate path, metadata, platform-layout and actual EditorWindow interaction tests, plus Windows machine discovery and GUI selection/save validation.

## 0.3.0-preview.3 — 2026-11-06 (experimental)

- Audit and support Windows Rider 2026.2.4.1 (262.11879.119), retaining the previous hash-pinned version. Require exactly 17 preference redirects and two replaced solution-sync methods.
- Validate the actual Rider desktop with Unity 6000.5.9f1: Console, Play/Pause/Resume/Stop, reconnect after script compilation and Play domain reload, debugger attachment, a bound breakpoint and single stepping with an inspected local value.
- Revalidate both Unity versions with Harmony 2.2.0 present, independent launching, protocol tests, and concurrent/crash isolation. No native Unity preferences were written or restored.
- Add an opt-in passive desktop acceptance observer, a runtime debugging fixture, and a reproducible validation report. Automatic solution generation and untested advanced features remain outside this preview's acceptance scope.

## 0.3.0-preview.2 — 2026-11-06 (experimental)

- Fix CS0433 when Harmony embeds its own Mono.Cecil types. Explicitly reference only `Mono.Cecil.dll` from the plugin and test assemblies, leaving other plugins' import settings unchanged.
- Add a Unity compilation/runtime regression with the official Harmony 2.2.0 DLL alongside Unity's Mono.Cecil package. No editor preference or Rider adapter behavior changes.

## 0.3.0-preview.1 — 2026-10-17 (experimental)

- Add an opt-in, hash-pinned Windows Rider 2026.2.3.1 adapter for Unity 2022.3 / 6000.5.
- Redirect the installed EditorPlugin's preference calls to per-project storage in an in-memory assembly copy; leave the original installation and Unity preference APIs untouched.
- Initialize independent project-local communication with cancellation on disable, reload and exit. Reject unknown binaries and pre-existing integrations.
- Redirect solution synchronization to asset refresh only. Existing root-level `.sln` and project files are required; automatic solution generation is intentionally outside this preview.
- Add initialization, preference isolation, protocol, concurrent service and forced termination tests. Full Rider desktop feature acceptance remains pending; this is not a stable advanced-integration release.

## 0.2.0 — 2026-10-17

- Replace global preference switching with an independent per-project Rider / VS Code launcher. No global preference reads or writes, focus handlers, session leases or shutdown restoration remain in production code.
- Open supported code assets through `OnOpenAsset`, with line/column navigation and physical package path resolution. Support Unity 2022.3 and Unity 6's newer EntityId callback.
- Add a dedicated Open Project command, explicit editor type and optional existing Rider solution. Keep legacy per-project paths and enabled state.
- Launch without a shell, quote arguments, use the current project's working directory and remove inherited VS Code terminal routing variables from the child environment.
- Consume supported asset requests on failure so they cannot silently fall back to the global editor; skip automatic launches in batch mode.
- Breaking change: only Rider / VS Code are supported. Native Unity editor selection and Open C# Project retain their global behavior. Solution generation and IDE integration initialization are outside this launcher's scope.
- Replace preference mutation tests with read-only preference checks, real child process argument tests and concurrent/crash isolation tests.

## 0.1.1 — 2026-10-17

- Restore the previous global script editor on focus loss, normal shutdown, disable and before assembly reload.
- Keep the original preference (including a missing key) across repeated applications and editor changes.
- Preserve the session backup across domain reloads and roll back failed IDE initialization.
- Do not overwrite a different editor choice made while the override was active.
- Add regression coverage for a project without the package retaining its editor after another project exits.

## 0.1.0 — 2026-10-17

- Initial UPM package for Unity 2022.3 and newer.
- Local, per-project external script editor selection.
- Project Settings UI with editor discovery, path selection, save and disable controls.
- Restore on startup, script reload and focus gain, only while the Editor is focused.
- Skip batch mode and invalid paths; preserve the user's selection when disabled.
