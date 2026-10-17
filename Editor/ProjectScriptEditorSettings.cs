using UnityEditor;
using UnityEngine;

namespace Avg.ProjectScriptEditor
{
    [FilePath("UserSettings/ProjectScriptEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ProjectScriptEditorSettings : ScriptableSingleton<ProjectScriptEditorSettings>
    {
        [SerializeField] private bool overrideEnabled;
        [SerializeField] private string editorPath = "";
        [SerializeField] private EditorKind editorKind;
        [SerializeField] private string solutionPath = "";
        [SerializeField] private bool riderIntegrationEnabled;

        internal bool OverrideEnabled => overrideEnabled;
        internal string EditorPath => editorPath;
        internal EditorKind Kind => editorKind;
        internal string SolutionPath => solutionPath;
        internal bool RiderIntegrationEnabled => riderIntegrationEnabled;

        internal void SetRiderIntegration(bool enabled) { riderIntegrationEnabled = enabled; Save(true); }

        internal void Remember(string path, EditorKind kind, string solution)
        {
            editorPath = path;
            editorKind = kind;
            solutionPath = solution;
            overrideEnabled = true;
            Save(true);
        }

        internal void Disable()
        {
            overrideEnabled = false;
            Save(true);
        }
    }
}
