using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_6000_3_OR_NEWER
using AssetId = UnityEngine.EntityId;
#else
using AssetId = System.Int32;
#endif

namespace Avg.ProjectScriptEditor
{
    internal static class ProjectScriptEditorController
    {
        private const string MenuRoot = "Tools/Project Script Editor/";
        private static readonly HashSet<string> Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".shader", ".compute", ".hlsl", ".cginc", ".glsl", ".glslinc",
            ".uxml", ".uss", ".asmdef", ".asmref", ".raytrace"
        };
        // A test seam for capturing launch requests without starting the user's IDE.
        internal static Action<EditorLaunchPlan> Launch = EditorLauncher.Start;
        internal static string ProjectDirectory => Directory.GetParent(Application.dataPath).FullName;
        internal static LaunchPlatform Platform => Application.platform == RuntimePlatform.WindowsEditor
            ? LaunchPlatform.Windows : Application.platform == RuntimePlatform.OSXEditor ? LaunchPlatform.MacOS : LaunchPlatform.Linux;

        internal static bool Remember(string path, EditorKind kind, string solution, out string error)
        {
            path = path?.Trim();
            solution = solution?.Trim() ?? "";
            try
            {
                EditorLauncher.Build(path, kind, solution, ProjectDirectory, null, -1, -1, Platform);
                ProjectScriptEditorSettings.instance.Remember(path, kind, solution);
                error = null;
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        [MenuItem(MenuRoot + "Settings...", priority = 0)]
        internal static void OpenSettings() => SettingsService.OpenProjectSettings(ProjectScriptEditorSettingsProvider.SettingsPath);

        [OnOpenAsset(-10000)]
        internal static bool OnOpenAsset(AssetId instanceId, int line, int column)
        {
            return HandleAssetPath(AssetDatabase.GetAssetPath(instanceId), line, column, Application.isBatchMode);
        }

        internal static bool HandleAssetPath(string path, int line, int column, bool batchMode)
        {
            if (!ProjectScriptEditorSettings.instance.OverrideEnabled || !Extensions.Contains(Path.GetExtension(path)))
                return false;
            // Claim supported assets even in batch mode or on failure, preventing a fallback to the global editor.
            if (!batchMode && !TryOpen(path, line, column, out var error))
                Debug.LogWarning("[Project Script Editor] " + error + " Open Project Script Editor settings to correct the configuration.");
            return true;
        }

        internal static bool TryOpen(string file, int line, int column, out string error)
        {
            var settings = ProjectScriptEditorSettings.instance;
            if (!settings.OverrideEnabled) { error = "Project Script Editor is disabled."; return false; }
            try
            {
                var plan = EditorLauncher.Build(settings.EditorPath, settings.Kind, settings.SolutionPath,
                    ProjectDirectory, ResolveAssetFile(file), line, column, Platform);
                Launch(plan);
                error = null;
                return true;
            }
            catch (Exception exception) { error = "Could not open the project editor: " + exception.Message; return false; }
        }

        private static string ResolveAssetFile(string file)
        {
            if (string.IsNullOrEmpty(file) || !file.StartsWith("Packages/", StringComparison.Ordinal)) return file;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(file);
            if (package == null) throw new FileNotFoundException("Cannot locate this package's files.", file);
            var prefix = "Packages/" + package.name + "/";
            if (!file.StartsWith(prefix, StringComparison.Ordinal)) throw new ArgumentException("Invalid package asset path.");
            return Path.Combine(package.resolvedPath, file.Substring(prefix.Length));
        }

        [MenuItem(MenuRoot + "Open Project", priority = 20)]
        internal static void OpenProject()
        {
            if (!Application.isBatchMode && !TryOpen(null, -1, -1, out var error))
                Debug.LogWarning("[Project Script Editor] " + error);
        }

        [MenuItem(MenuRoot + "Disable", priority = 21)]
        internal static void Disable() => ProjectScriptEditorSettings.instance.Disable();

        [MenuItem(MenuRoot + "Open Project", validate = true)]
        [MenuItem(MenuRoot + "Disable", validate = true)]
        private static bool IsEnabled() => ProjectScriptEditorSettings.instance.OverrideEnabled;
    }
}
