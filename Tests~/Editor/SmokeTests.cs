using System;
using System.IO;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Avg.ProjectScriptEditor.Tests
{
    // Copy Tests~/Editor into Assets of a disposable project with the package installed.
    // No test-framework dependency and no real IDE is launched or selected.
    public static class SmokeTests
    {
        private static int assertions;
        private static string FixturePath => Path.GetFullPath("Temp/ProjectScriptEditorTests/fake-editor.exe");

        public static void Run()
        {
            var originalEditor = CodeEditor.CurrentEditorPath;
            Directory.CreateDirectory(Path.GetDirectoryName(FixturePath));
            File.WriteAllText(FixturePath, "test fixture; never executed");
            assertions = PolicyTests.Run(Path.GetDirectoryName(FixturePath));

            var settings = ProjectScriptEditorSettings.instance;
            Check(!settings.OverrideEnabled, "new project starts disabled");
            Check(ProjectScriptEditorController.Remember("  " + FixturePath + "  ", out _), "remember valid selection");
            Check(settings.OverrideEnabled && settings.EditorPath == FixturePath, "remember trims and enables");
            Check(!ProjectScriptEditorController.Remember("", out _)
                && settings.OverrideEnabled && settings.EditorPath == FixturePath, "invalid save preserves prior selection");
            ProjectScriptEditorController.Disable();
            Check(!settings.OverrideEnabled && settings.EditorPath == FixturePath, "disable preserves saved path");
            ProjectScriptEditorController.ApplySaved();
            Check(CodeEditor.CurrentEditorPath == originalEditor, "batch operations never change real preference");

            var provider = ProjectScriptEditorSettingsProvider.CreateProvider();
            var root = new VisualElement();
            provider.OnActivate("", root);
            Check(root.Q<TextField>() != null, "settings path field constructed");
            Check(root.Query<Button>().ToList().Count == 5, "settings actions constructed");
            Check(root.Q<DropdownField>() != null, "detected editors dropdown constructed");
            Check(AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.avg.project-script-editor/Editor/ProjectScriptEditorSettings.uss") != null, "stylesheet imported");
            provider.OnDeactivate();

            // Leave a saved enabled selection for VerifyPersistence, run in a NEW Editor process.
            settings.Remember(FixturePath);
            Check(File.Exists("UserSettings/ProjectScriptEditorSettings.asset"), "settings persisted in UserSettings");
            Check(CodeEditor.CurrentEditorPath == originalEditor, "UI creation leaves preference unchanged");
            Debug.Log("PROJECT_SCRIPT_EDITOR_TESTS_PASSED assertions=" + assertions);
        }

        public static void VerifyPersistence()
        {
            var originalEditor = CodeEditor.CurrentEditorPath;
            var settings = ProjectScriptEditorSettings.instance;
            Check(settings.OverrideEnabled, "enabled state survives process restart");
            Check(settings.EditorPath == FixturePath, "path survives process restart");
            ProjectScriptEditorController.ApplySaved();
            Check(CodeEditor.CurrentEditorPath == originalEditor, "enabled override remains inert in batch mode");
            settings.Disable();
            Debug.Log("PROJECT_SCRIPT_EDITOR_PERSISTENCE_PASSED assertions=" + assertions);
        }

        private static void Check(bool condition, string description)
        {
            if (!condition)
                throw new Exception("Project Script Editor test failed: " + description);
            assertions++;
        }
    }
}
