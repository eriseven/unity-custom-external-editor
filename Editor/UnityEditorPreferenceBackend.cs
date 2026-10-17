using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace Avg.ProjectScriptEditor
{
    internal sealed class UnityEditorPreferenceBackend : IEditorPreferenceBackend
    {
        internal const string PreferenceKey = "kScriptsDefaultApp";
        private const string SessionKey = "Avg.ProjectScriptEditor.PreferenceLease.v1";

        public EditorPreferenceSnapshot ReadPreference() => new EditorPreferenceSnapshot
        {
            exists = EditorPrefs.HasKey(PreferenceKey),
            path = EditorPrefs.GetString(PreferenceKey, "")
        };

        public void SelectEditor(string path) => CodeEditor.Editor.SetCodeEditor(path);

        public void RestorePreference(EditorPreferenceSnapshot previous)
        {
            try
            {
                // Restore the process-local integration as well as the persisted preference.
                CodeEditor.Editor.SetCodeEditor(previous.exists ? previous.path : "");
            }
            finally
            {
                // Even if an IDE's Initialize throws, do not leave our path in shared preferences.
                // Preserve the difference between an absent key and explicit OS file association.
                if (previous.exists)
                    EditorPrefs.SetString(PreferenceKey, previous.path);
                else
                    EditorPrefs.DeleteKey(PreferenceKey);
            }
        }

        public EditorPreferenceLeaseState LoadLease()
        {
            var json = SessionState.GetString(SessionKey, "");
            return string.IsNullOrEmpty(json)
                ? new EditorPreferenceLeaseState()
                : JsonUtility.FromJson<EditorPreferenceLeaseState>(json);
        }

        public void SaveLease(EditorPreferenceLeaseState state)
        {
            if (state.active)
                SessionState.SetString(SessionKey, JsonUtility.ToJson(state));
            else
                SessionState.EraseString(SessionKey);
        }
    }
}
