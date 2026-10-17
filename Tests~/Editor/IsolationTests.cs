using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Avg.ProjectScriptEditor.Tests
{
    public static class IsolationTests
    {
        [Serializable]
        private sealed class Snapshot { public bool exists; public string path; }

        public static void Hold()
        {
            var baseline = new Snapshot { exists = EditorPrefs.HasKey("kScriptsDefaultApp"), path = EditorPrefs.GetString("kScriptsDefaultApp", "") };
            var kind = Environment.GetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_TEST_KIND") == "VSCode" ? EditorKind.VSCode : EditorKind.Rider;
            var fixture = Path.GetFullPath("Library/Isolation/" + (kind == EditorKind.Rider ? "rider64.exe" : "Code.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(fixture));
            File.WriteAllText(fixture, "never executed");
            if (!ProjectScriptEditorController.Remember(fixture, kind, "", out var error)) throw new Exception(error);
            ProjectScriptEditorController.Launch = plan =>
            {
                if (plan.Executable != fixture) throw new Exception("Project selected another editor.");
            };
            if (!ProjectScriptEditorController.TryOpen(null, -1, -1, out error)) throw new Exception(error);
            File.WriteAllText("isolation-baseline.json", JsonUtility.ToJson(baseline));
            File.WriteAllText("isolation-ready.txt", kind.ToString());
            var deadline = EditorApplication.timeSinceStartup + 180;
            EditorApplication.update += () =>
            {
                if (EditorPrefs.HasKey("kScriptsDefaultApp") != baseline.exists || EditorPrefs.GetString("kScriptsDefaultApp", "") != baseline.path)
                {
                    Debug.LogError("ISOLATION_PREFERENCE_CHANGED");
                    EditorApplication.Exit(1);
                }
                else if (File.Exists("isolation-finish.txt"))
                {
                    Debug.Log("CONCURRENT_ISOLATION_PASSED kind=" + kind);
                    EditorApplication.Exit(0);
                }
                else if (EditorApplication.timeSinceStartup > deadline)
                {
                    Debug.LogError("ISOLATION_TEST_TIMEOUT");
                    EditorApplication.Exit(2);
                }
            };
        }
    }
}
