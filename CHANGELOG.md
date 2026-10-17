# Changelog

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
