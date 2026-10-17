using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Avg.ProjectScriptEditor.Tests
{
    public static class SmokeTests
    {
        private static int assertions;
        private const string PreferenceKey = "kScriptsDefaultApp";
        private static string FixturePath => Path.GetFullPath("Library/ProjectScriptEditorTests/rider64.exe");

        public static void Run()
        {
            var hadPreference = EditorPrefs.HasKey(PreferenceKey);
            var preference = EditorPrefs.GetString(PreferenceKey, "");
            assertions = LauncherTests.Run(Path.GetFullPath("Library/ProjectScriptEditorTests/" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Path.GetDirectoryName(FixturePath));
            File.WriteAllText(FixturePath, "never executed");
            var settings = ProjectScriptEditorSettings.instance;
            settings.SetRiderIntegration(false);
            var launcher = ProjectScriptEditorController.Launch;
            int launches = 0;
            EditorLaunchPlan captured = null;
            ProjectScriptEditorController.Launch = plan => { launches++; captured = plan; };
            try
            {
                Check(ProjectScriptEditorController.Remember(" " + FixturePath + " ", EditorKind.Rider, "", out _), "save editor");
                Check(settings.EditorPath == FixturePath && settings.Kind == EditorKind.Rider && settings.OverrideEnabled, "save state");
                Check(launches == 0, "save never launches");
                Check(!ProjectScriptEditorController.Remember("missing.exe", EditorKind.Rider, "", out _) && settings.EditorPath == FixturePath, "invalid save preserves choice");
                var script = "Packages/com.avg.project-script-editor/Editor/ProjectScriptEditorSettings.cs";
                // Package paths are virtual and must be resolved before passing them to an external process.
                Check(ProjectScriptEditorController.TryOpen(script, 12, 3, out var error), "open package script: " + error);
                Check(captured.Arguments[captured.Arguments.Length - 1].EndsWith("ProjectScriptEditorSettings.cs"), "package script target");
                Check(File.Exists(captured.Arguments[captured.Arguments.Length - 1]), "package script path exists on disk");
                Check(ProjectScriptEditorController.HandleAssetPath(script, 9, 2, false), "interactive callback consumes script");
                Check(launches == 2, "callback launches exactly once");
                Check(ProjectScriptEditorController.HandleAssetPath(script, 9, 2, true) && launches == 2, "batch consumes script without launch");
                Check(!ProjectScriptEditorController.HandleAssetPath("Assets/Image.png", 0, 0, false), "non-code assets ignored");
                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(script);
#if UNITY_6000_3_OR_NEWER
                Check(monoScript != null && ProjectScriptEditorController.OnOpenAsset(monoScript.GetEntityId(), 8, 1), "real asset id resolves");
#else
                Check(monoScript != null && ProjectScriptEditorController.OnOpenAsset(monoScript.GetInstanceID(), 8, 1), "real asset id resolves");
#endif
                Check(launches == 2, "batch asset id callback does not launch");
                Check(AssetDatabase.OpenAsset(monoScript, 8, 1), "Unity asset opening dispatch handled");
                Check(launches == 2, "Unity dispatch honors batch guard");
                ProjectScriptEditorController.Launch = plan => throw new InvalidOperationException("test launch failure");
                Check(!ProjectScriptEditorController.TryOpen(null, -1, -1, out error) && error.Contains("test launch failure"), "launch failure reported");
                Check(ProjectScriptEditorController.HandleAssetPath(script, 1, 1, false), "failure consumed without global fallback");
                ProjectScriptEditorController.Launch = plan => launches++;
                settings.Remember(FixturePath + ".missing", EditorKind.Rider, "");
                Check(ProjectScriptEditorController.HandleAssetPath(script, 1, 1, false) && launches == 2, "missing editor cannot fall back");
                ProjectScriptEditorController.Disable();
                Check(!ProjectScriptEditorController.HandleAssetPath(script, 1, 1, false), "disabled callback uses normal Unity behavior");
                Check(!ProjectScriptEditorController.TryOpen(null, -1, -1, out _), "disabled explicit open blocked");
                Check(EditorPrefs.HasKey(PreferenceKey) == hadPreference && EditorPrefs.GetString(PreferenceKey, "") == preference, "global preference unchanged through all operations");

                var provider = ProjectScriptEditorSettingsProvider.CreateProvider();
                var root = new VisualElement();
                provider.OnActivate("", root);
                Check(root.Q<TextField>("editorPath") != null && root.Q<TextField>("solutionPath") != null, "UI fields constructed");
                Check(root.Query<Button>().ToList().Count == 5, "UI actions constructed");
                Check(root.Q<DropdownField>().choices.Count == 3, "editor kind selector");
                Check(root.Q<Toggle>("riderIntegration") != null, "experimental integration opt-in");
                Check(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.avg.project-script-editor/Editor/ProjectScriptEditorSettings.uss") != null, "USS imported");
                provider.OnDeactivate();
                settings.Remember(FixturePath, EditorKind.Rider, "");
                Check(File.Exists("UserSettings/ProjectScriptEditorSettings.asset"), "local settings saved");
                Check(EditorPrefs.HasKey(PreferenceKey) == hadPreference && EditorPrefs.GetString(PreferenceKey, "") == preference, "UI and persistence leave preference untouched");
                Debug.Log("ISOLATED_LAUNCHER_TESTS_PASSED assertions=" + assertions);
            }
            finally { ProjectScriptEditorController.Launch = launcher; }
        }

        public static void VerifyPersistence()
        {
            var settings = ProjectScriptEditorSettings.instance;
            Check(settings.OverrideEnabled && settings.EditorPath == FixturePath, "enabled path survives restart");
            Check(settings.Kind == EditorKind.Rider && settings.SolutionPath == "", "launch profile survives restart");
            settings.Disable();
            Debug.Log("ISOLATED_LAUNCHER_PERSISTENCE_PASSED assertions=" + assertions);
        }

        private static void Check(bool value, string description)
        {
            if (!value) throw new Exception("Smoke test failed: " + description);
            assertions++;
        }
    }
}
