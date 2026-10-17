using System;

namespace Avg.ProjectScriptEditor
{
    [Serializable]
    internal struct EditorPreferenceSnapshot
    {
        public bool exists;
        public string path;
    }

    [Serializable]
    internal sealed class EditorPreferenceLeaseState
    {
        public bool active;
        public EditorPreferenceSnapshot previous;
        public string appliedPath;
    }

    internal interface IEditorPreferenceBackend
    {
        EditorPreferenceSnapshot ReadPreference();
        void SelectEditor(string path);
        void RestorePreference(EditorPreferenceSnapshot previous);
        EditorPreferenceLeaseState LoadLease();
        void SaveLease(EditorPreferenceLeaseState state);
    }

    // The backup belongs to a focused session, not to the project's permanent editor choice.
    internal sealed class EditorPreferenceLease
    {
        private readonly IEditorPreferenceBackend backend;

        internal EditorPreferenceLease(IEditorPreferenceBackend backend) => this.backend = backend;

        internal void Apply(string path)
        {
            var state = backend.LoadLease();
            var current = backend.ReadPreference();
            if (!OwnsPreference(state, current))
                state.previous = current;

            state.active = true;
            state.appliedPath = path;
            // Record before calling third-party IDE initialization, which can throw or request a reload.
            backend.SaveLease(state);
            try
            {
                backend.SelectEditor(path);
            }
            catch
            {
                Release();
                throw;
            }
        }

        internal void Release()
        {
            var state = backend.LoadLease();
            if (!state.active)
                return;

            // Do not undo an explicit change made by the user or another Unity process.
            if (OwnsPreference(state, backend.ReadPreference()))
                backend.RestorePreference(state.previous);

            // If restoration fails, retain the backup so the next lifecycle event can retry.
            backend.SaveLease(new EditorPreferenceLeaseState());
        }

        private static bool OwnsPreference(EditorPreferenceLeaseState state, EditorPreferenceSnapshot current)
        {
            return state.active && current.exists &&
                string.Equals(current.path, state.appliedPath, StringComparison.Ordinal);
        }
    }
}
