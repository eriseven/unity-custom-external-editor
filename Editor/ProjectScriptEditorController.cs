using System;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace Avg.ProjectScriptEditor
{
    [InitializeOnLoad]
    internal static class ProjectScriptEditorController
    {
        private const string MenuRoot = "Tools/Project Script Editor/";
        private static string lastWarning;
        private static bool quitting;
        private static readonly EditorPreferenceLease PreferenceLease =
            new EditorPreferenceLease(new UnityEditorPreferenceBackend());

        static ProjectScriptEditorController()
        {
            if (Application.isBatchMode)
                return;

            RegisterLifecycleCallbacks();
            // SessionState survives domain reloads; recover any outstanding lease before reapplying.
            ReleaseOverride();
            EditorApplication.delayCall += QueueRestore;
        }

        internal static void RegisterLifecycleCallbacks()
        {
            EditorApplication.focusChanged -= OnFocusChanged;
            EditorApplication.focusChanged += OnFocusChanged;
            EditorApplication.quitting -= OnQuitting;
            EditorApplication.quitting += OnQuitting;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseOverride;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseOverride;
        }

        internal static void OnFocusChanged(bool focused)
        {
            if (focused)
                QueueRestore();
            else
                ReleaseOverride();
        }

        private static void QueueRestore()
        {
            if (quitting)
                return;
            EditorApplication.update -= RestoreWhenReady;
            EditorApplication.update += RestoreWhenReady;
        }

        private static void RestoreWhenReady()
        {
            if (!EditorApplication.isFocused)
            {
                EditorApplication.update -= RestoreWhenReady;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            EditorApplication.update -= RestoreWhenReady;
            ApplySaved();
        }

        internal static bool Remember(string path, out string error)
        {
            path = path?.Trim();
            if (!EditorOverride.ValidatePath(path, out error))
                return false;

            ProjectScriptEditorSettings.instance.Remember(path);
            lastWarning = null;
            ApplySaved();
            return true;
        }

        [MenuItem(MenuRoot + "Settings...", priority = 0)]
        internal static void OpenSettings()
        {
            SettingsService.OpenProjectSettings(ProjectScriptEditorSettingsProvider.SettingsPath);
        }

        [MenuItem(MenuRoot + "Remember Current Editor", priority = 20)]
        private static void RememberCurrent()
        {
            if (!Remember(CodeEditor.CurrentEditorPath, out var error))
                Debug.LogWarning("[Project Script Editor] " + error);
        }

        [MenuItem(MenuRoot + "Apply Saved Editor", priority = 21)]
        private static void ApplyFromMenu() => ApplySaved();

        internal static void ApplySaved()
        {
            if (quitting)
                return;
            var settings = ProjectScriptEditorSettings.instance;
            EditorOverride.TryRestore(settings.OverrideEnabled, settings.EditorPath,
                Application.isBatchMode, EditorApplication.isFocused,
                PreferenceLease.Apply, out var error);

            if (error != null && error != lastWarning)
                Debug.LogWarning("[Project Script Editor] " + error);
            lastWarning = error;
        }

        [MenuItem(MenuRoot + "Disable Override", priority = 22)]
        internal static void Disable()
        {
            ProjectScriptEditorSettings.instance.Disable();
            ReleaseOverride();
            lastWarning = null;
        }

        private static void OnQuitting()
        {
            quitting = true;
            ReleaseOverride();
        }

        private static void ReleaseOverride()
        {
            EditorApplication.delayCall -= QueueRestore;
            EditorApplication.update -= RestoreWhenReady;
            try
            {
                PreferenceLease.Release();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Project Script Editor] Could not restore the previous editor: " + exception.Message);
            }
        }

        [MenuItem(MenuRoot + "Apply Saved Editor", validate = true)]
        [MenuItem(MenuRoot + "Disable Override", validate = true)]
        private static bool CanApplyOrDisable() => ProjectScriptEditorSettings.instance.OverrideEnabled;
    }
}
