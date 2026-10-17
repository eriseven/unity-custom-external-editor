using System.Collections.Generic;
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
            keywords = new HashSet<string> { "External", "Script", "Editor", "Rider", "VS Code", "Isolated" };
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider() => new ProjectScriptEditorSettingsProvider();

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            var panel = new ScrollView();
            panel.AddToClassList("project-script-editor");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StylePath);
            if (sheet != null) panel.styleSheets.Add(sheet);
            rootElement.Add(panel);
            var title = new Label("Project Script Editor");
            title.AddToClassList("title");
            panel.Add(title);
            panel.Add(new HelpBox("Open scripts using this project's editor without changing Unity's global preferences. Unity Preferences and the built-in Open C# Project command keep their global behavior.", HelpBoxMessageType.Info));

            var settings = ProjectScriptEditorSettings.instance;
            var integration = new Toggle("Experimental isolated Rider integration") { value = settings.RiderIntegrationEnabled, name = "riderIntegration" };
            integration.RegisterValueChangedCallback(evt =>
            {
                settings.SetRiderIntegration(evt.newValue);
                RiderIntegration.Reconcile();
            });
            panel.Add(integration);
            var integrationStatus = new Label();
            integrationStatus.AddToClassList("wrapped");
            panel.Add(integrationStatus);
            panel.Add(new HelpBox("Experimental: Windows Rider 2026.2.3.1 only. Uses an in-memory adapted copy of Rider's plugin, with project-local preferences. Existing .sln/.csproj files are required. Disabling disconnects immediately; changing or re-enabling requires a script reload or restart. Unknown builds are refused.", HelpBoxMessageType.Warning));
            var status = new Label();
            status.AddToClassList("wrapped");
            panel.Add(status);
            var kind = new DropdownField("Editor", new List<string> { "Automatic", "Rider", "VS Code" }, (int)settings.Kind);
            panel.Add(kind);
            var path = new TextField("Editor executable") { value = settings.EditorPath, name = "editorPath" };
            panel.Add(path);
            panel.Add(new Button(() =>
            {
                var selected = EditorUtility.OpenFilePanel("Select Rider or VS Code executable (.app on macOS)", "",
                    Application.platform == RuntimePlatform.WindowsEditor ? "exe" : "");
                if (!string.IsNullOrEmpty(selected)) path.value = selected;
            }) { text = "Browse Editor..." });

            var solution = new TextField("Rider solution (optional)")
            {
                value = settings.SolutionPath, name = "solutionPath",
                tooltip = "Existing .sln/.slnx, absolute or relative to the project. Empty: detect an existing solution, otherwise open the folder."
            };
            panel.Add(solution);
            panel.Add(new Button(() =>
            {
                var selected = EditorUtility.OpenFilePanel("Select existing Rider solution", ProjectScriptEditorController.ProjectDirectory, "");
                if (!string.IsNullOrEmpty(selected)) solution.value = selected;
            }) { text = "Browse Solution..." });

            var message = new HelpBox("", HelpBoxMessageType.Info);
            message.AddToClassList("hidden");
            void ShowMessage(string text, HelpBoxMessageType type)
            {
                message.text = text;
                message.messageType = type;
                message.RemoveFromClassList("hidden");
            }
            var actions = new VisualElement();
            actions.AddToClassList("button-row");
            actions.Add(new Button(() =>
            {
                if (ProjectScriptEditorController.Remember(path.value, (EditorKind)kind.index, solution.value, out var error))
                    ShowMessage("Saved for this project. No global preference was changed.", HelpBoxMessageType.Info);
                else ShowMessage(error, HelpBoxMessageType.Error);
            }) { text = "Save and Enable" });
            var open = new Button(() =>
            {
                if (!ProjectScriptEditorController.TryOpen(null, -1, -1, out var error))
                    ShowMessage(error, HelpBoxMessageType.Error);
            }) { text = "Open Project" };
            var disable = new Button(() =>
            {
                ProjectScriptEditorController.Disable();
                ShowMessage("Disabled. Script opening now follows Unity's normal behavior.", HelpBoxMessageType.Info);
            }) { text = "Disable" };
            actions.Add(open);
            actions.Add(disable);
            panel.Add(actions);
            panel.Add(message);
            panel.Add(new HelpBox("Saved in UserSettings. The launcher does not generate solutions. The optional Rider adapter establishes a project-local connection; project generation and debugging setup remain separate steps.", HelpBoxMessageType.Info));

            void RefreshStatus()
            {
                status.text = settings.OverrideEnabled ? "Enabled: " + settings.EditorPath : "Disabled";
                integrationStatus.text = "Rider integration: " + RiderIntegration.Status;
                open.SetEnabled(settings.OverrideEnabled);
                disable.SetEnabled(settings.OverrideEnabled);
            }
            RefreshStatus();
            panel.schedule.Execute(RefreshStatus).Every(500);
        }
    }
}
