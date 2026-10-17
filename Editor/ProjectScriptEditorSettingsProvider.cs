using System;
using System.Collections.Generic;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Avg.ProjectScriptEditor
{
    internal sealed class ProjectScriptEditorSettingsProvider : SettingsProvider
    {
        internal const string SettingsPath = "Project/Project Script Editor";
        private const string StylePath = "Packages/com.avg.project-script-editor/Editor/ProjectScriptEditorSettings.uss";

        private ProjectScriptEditorSettingsProvider() : base(SettingsPath, SettingsScope.Project)
        {
            label = "Project Script Editor";
            keywords = new HashSet<string> { "External", "Script", "Editor", "Rider", "Visual Studio", "VS Code" };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider() => new ProjectScriptEditorSettingsProvider();

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            var panel = new ScrollView();
            panel.AddToClassList("project-script-editor");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (sheet != null)
                panel.styleSheets.Add(sheet);
            rootElement.Add(panel);

            var title = new Label("Project Script Editor");
            title.AddToClassList("title");
            panel.Add(title);
            panel.Add(new HelpBox("Use this editor while this Unity window has focus. The previous global editor is restored on focus loss, shutdown, disable and script reload. Unity's underlying preference is still shared.", HelpBoxMessageType.Info));

            var status = new Label();
            status.AddToClassList("wrapped");
            panel.Add(status);
            var current = new Label();
            current.AddToClassList("wrapped");
            panel.Add(current);

            var settings = ProjectScriptEditorSettings.instance;
            var pathField = new TextField("Editor path")
            {
                value = string.IsNullOrEmpty(settings.EditorPath) ? CodeEditor.CurrentEditorPath : settings.EditorPath,
                tooltip = "Absolute executable path, or a macOS .app bundle. Changes are saved only when Save and Enable is clicked."
            };
            panel.Add(pathField);

            var message = new HelpBox("", HelpBoxMessageType.Info);
            message.AddToClassList("hidden");

            void ShowMessage(string text, HelpBoxMessageType type)
            {
                message.text = text;
                message.messageType = type;
                message.RemoveFromClassList("hidden");
            }

            var selectionButtons = new VisualElement();
            selectionButtons.AddToClassList("button-row");
            selectionButtons.Add(new Button(() => pathField.value = CodeEditor.CurrentEditorPath) { text = "Use Current Editor" });
            selectionButtons.Add(new Button(() =>
            {
                var path = Application.platform == RuntimePlatform.OSXEditor
                    ? EditorUtility.OpenFolderPanel("Select editor .app bundle", "/Applications", "")
                    : EditorUtility.OpenFilePanel("Select script editor", "", Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
                if (!string.IsNullOrEmpty(path))
                    pathField.value = path;
            }) { text = "Browse..." });
            panel.Add(selectionButtons);

            var installed = new DropdownField("Detected editors", new List<string> { "Choose an installed editor..." }, 0);
            var detectedPaths = new List<string> { "" };
            try
            {
                foreach (var entry in CodeEditor.Editor.GetFoundScriptEditorPaths())
                {
                    installed.choices.Add(entry.Value + " — " + entry.Key);
                    detectedPaths.Add(entry.Key);
                }
            }
            catch (Exception exception)
            {
                ShowMessage("Editor discovery failed. You can still browse or paste a path. " + exception.Message, HelpBoxMessageType.Warning);
            }
            installed.SetEnabled(detectedPaths.Count > 1);
            installed.RegisterValueChangedCallback(evt =>
            {
                var index = installed.choices.IndexOf(evt.newValue);
                if (index > 0)
                    pathField.value = detectedPaths[index];
            });
            panel.Add(installed);

            var actions = new VisualElement();
            actions.AddToClassList("button-row");
            actions.Add(new Button(() =>
            {
                if (ProjectScriptEditorController.Remember(pathField.value, out var error))
                    ShowMessage("Saved for this project. See the Console if the editor integration reports an activation error.", HelpBoxMessageType.Info);
                else
                    ShowMessage(error, HelpBoxMessageType.Error);
            }) { text = "Save and Enable" });
            var apply = new Button(ProjectScriptEditorController.ApplySaved) { text = "Apply Saved Editor" };
            var disable = new Button(() =>
            {
                ProjectScriptEditorController.Disable();
                ShowMessage("Override disabled. The previous global editor has been restored unless the selection was changed elsewhere.", HelpBoxMessageType.Info);
            }) { text = "Disable Override" };
            actions.Add(apply);
            actions.Add(disable);
            panel.Add(actions);
            panel.Add(message);
            panel.Add(new HelpBox("Saved locally in UserSettings/ProjectScriptEditorSettings.asset. To change editors, choose a new path here and click Save and Enable. If using Preferences > External Tools instead, disable the override first.", HelpBoxMessageType.Info));

            void RefreshStatus()
            {
                status.text = settings.OverrideEnabled ? "Override enabled: " + settings.EditorPath : "Override disabled";
                var currentPath = CodeEditor.CurrentEditorPath;
                current.text = "Unity's current editor: " + (string.IsNullOrEmpty(currentPath) ? "Open by file extension" : currentPath);
                apply.SetEnabled(settings.OverrideEnabled);
                disable.SetEnabled(settings.OverrideEnabled);
            }

            RefreshStatus();
            panel.schedule.Execute(RefreshStatus).Every(500);
        }
    }
}
