using System;

namespace Avg.ProjectScriptEditor.Tests
{
    internal static class PreferenceLeaseTests
    {
        internal static int Run()
        {
            var assertions = 0;
            void Check(bool value, string description)
            {
                if (!value)
                    throw new Exception("Preference lease regression: " + description);
                assertions++;
            }

            var backend = new MemoryBackend();
            var lease = new EditorPreferenceLease(backend);
            backend.Current = new EditorPreferenceSnapshot { exists = true, path = "VS Code" };
            lease.Apply("Rider");
            Check(backend.Current.path == "Rider", "B uses Rider while focused");
            lease.Apply("Rider");
            lease.Apply("Rider Updated");
            lease.Release();
            Check(backend.Current.path == "VS Code", "A retains VS Code after B releases, even after repeated apply/change");
            lease.Release();
            Check(backend.Current.path == "VS Code", "duplicate shutdown/focus-loss release is inert");

            backend.Current = new EditorPreferenceSnapshot { exists = false, path = "" };
            lease.Apply("Rider");
            lease.Release();
            Check(!backend.Current.exists, "missing preference remains absent");
            backend.Current = new EditorPreferenceSnapshot { exists = true, path = "" };
            lease.Apply("Rider");
            lease.Release();
            Check(backend.Current.exists && backend.Current.path == "", "explicit OS association remains explicit");

            backend.Current = new EditorPreferenceSnapshot { exists = true, path = "VS Code" };
            lease.Apply("Rider");
            backend.Current.path = "Visual Studio";
            lease.Release();
            Check(backend.Current.path == "Visual Studio", "do not overwrite an external editor change");
            lease.Apply("Rider");
            backend.Current = new EditorPreferenceSnapshot { exists = false, path = "" };
            lease.Release();
            Check(!backend.Current.exists, "do not overwrite an externally removed preference");

            backend.Current = new EditorPreferenceSnapshot { exists = true, path = "VS Code" };
            lease.Apply("Rider");
            backend.Current.path = "Visual Studio";
            lease.Apply("Rider");
            lease.Release();
            Check(backend.Current.path == "Visual Studio", "reacquisition captures the latest external baseline");

            lease.Apply("Rider");
            new EditorPreferenceLease(backend).Release();
            Check(backend.Current.path == "Visual Studio", "session backup survives controller reconstruction");

            backend.FailSelect = true;
            try { lease.Apply("Broken Rider"); } catch (InvalidOperationException) { }
            Check(backend.Current.path == "Visual Studio" && !backend.State.active, "failed initialization rolls back immediately");
            backend.FailSelect = false;
            lease.Apply("Rider");
            backend.FailRestore = true;
            try { lease.Release(); } catch (InvalidOperationException) { }
            Check(backend.State.active, "failed restore keeps backup");
            backend.FailRestore = false;
            lease.Release();
            Check(backend.Current.path == "Visual Studio" && !backend.State.active, "subsequent lifecycle event retries restoration");
            return assertions;
        }

        private sealed class MemoryBackend : IEditorPreferenceBackend
        {
            internal EditorPreferenceSnapshot Current;
            internal EditorPreferenceLeaseState State = new EditorPreferenceLeaseState();
            internal bool FailSelect;
            internal bool FailRestore;

            public EditorPreferenceSnapshot ReadPreference() => Current;
            public void SelectEditor(string path)
            {
                Current = new EditorPreferenceSnapshot { exists = true, path = path };
                if (FailSelect) throw new InvalidOperationException("IDE initialization failed");
            }
            public void RestorePreference(EditorPreferenceSnapshot previous)
            {
                if (FailRestore) throw new InvalidOperationException("Restore failed");
                Current = previous;
            }
            public EditorPreferenceLeaseState LoadLease() => new EditorPreferenceLeaseState
            {
                active = State.active, previous = State.previous, appliedPath = State.appliedPath
            };
            public void SaveLease(EditorPreferenceLeaseState state) => State = state;
        }
    }
}
