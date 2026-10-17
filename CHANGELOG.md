# Changelog

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
