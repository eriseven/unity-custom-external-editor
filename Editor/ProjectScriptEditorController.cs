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

        static ProjectScriptEditorController()
        {
            if (Application.isBatchMode)
                return;

            EditorApplication.focusChanged += OnFocusChanged;
            EditorApplication.delayCall += QueueRestore;
        }

        private static void OnFocusChanged(bool focused)
        {
            if (focused)
                QueueRestore();
        }

        private static void QueueRestore()
        {
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
            var settings = ProjectScriptEditorSettings.instance;
            EditorOverride.TryRestore(settings.OverrideEnabled, settings.EditorPath,
                Application.isBatchMode, EditorApplication.isFocused,
                path => CodeEditor.Editor.SetCodeEditor(path), out var error);

            if (error != null && error != lastWarning)
                Debug.LogWarning("[Project Script Editor] " + error);
            lastWarning = error;
        }

        [MenuItem(MenuRoot + "Disable Override", priority = 22)]
        internal static void Disable()
        {
            ProjectScriptEditorSettings.instance.Disable();
            lastWarning = null;
        }

        [MenuItem(MenuRoot + "Apply Saved Editor", validate = true)]
        [MenuItem(MenuRoot + "Disable Override", validate = true)]
        private static bool CanApplyOrDisable() => ProjectScriptEditorSettings.instance.OverrideEnabled;
    }
}
