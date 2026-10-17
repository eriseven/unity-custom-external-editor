using UnityEditor;
using UnityEngine;

namespace Avg.ProjectScriptEditor
{
    [FilePath("UserSettings/ProjectScriptEditorSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ProjectScriptEditorSettings : ScriptableSingleton<ProjectScriptEditorSettings>
    {
        [SerializeField] private bool overrideEnabled;
        [SerializeField] private string editorPath = "";

        internal bool OverrideEnabled => overrideEnabled;
        internal string EditorPath => editorPath;

        internal void Remember(string path)
        {
            editorPath = path;
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
