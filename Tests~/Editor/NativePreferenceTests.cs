using System;
using System.IO;
using System.Reflection;
using Unity.CodeEditor;
using UnityEditor;
using UnityEngine;

namespace Avg.ProjectScriptEditor.Tests
{
    // Explicit opt-in: these tests briefly change the real preference and always restore it.
    // Run only in a disposable project with no other Unity Editor running.
    public static class NativePreferenceTests
    {
        public static void PrepareQuitRegression()
        {
            if (Environment.GetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_NATIVE_TEST") != "1")
                throw new InvalidOperationException("Native preference tests require explicit opt-in.");

            var backend = new UnityEditorPreferenceBackend();
            var original = backend.ReadPreference();
            var fixture = Path.GetFullPath("Temp/ProjectScriptEditorTests/fake-rider.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(fixture));
            File.WriteAllText(fixture, "never executed");
            File.WriteAllText("native-preference-baseline.json", JsonUtility.ToJson(original));

            // Use the exact lease used by the controller, not a fake selection callback.
            var lease = (EditorPreferenceLease)typeof(ProjectScriptEditorController)
                .GetField("PreferenceLease", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            try
            {
                lease.Apply(fixture);
                Check(CodeEditor.CurrentEditorPath == fixture, "native override applied");
                ProjectScriptEditorController.OnFocusChanged(false);
                Check(Same(backend.ReadPreference(), original), "focus loss restores native preference");

                lease.Apply(fixture);
                ProjectScriptEditorController.Disable();
                Check(Same(backend.ReadPreference(), original), "disable restores native preference");

                lease.Apply(fixture);
                new EditorPreferenceLease(new UnityEditorPreferenceBackend()).Release();
                Check(Same(backend.ReadPreference(), original), "SessionState backup survives lease reconstruction");

                // Match production event registration, then let the Editor actually exit.
                ProjectScriptEditorController.RegisterLifecycleCallbacks();
                lease.Apply(fixture);
                EditorApplication.quitting += () =>
                {
                    try
                    {
                        Check(Same(backend.ReadPreference(), original), "normal quit restores native preference");
                        File.WriteAllText("native-preference-quit-passed.txt", "passed");
                        Debug.Log("NATIVE_PREFERENCE_QUIT_PASSED assertions=5");
                    }
                    finally
                    {
                        backend.RestorePreference(original);
                        backend.SaveLease(new EditorPreferenceLeaseState());
                    }
                };
                Debug.Log("NATIVE_PREFERENCE_QUIT_ARMED");
            }
            catch
            {
                backend.RestorePreference(original);
                backend.SaveLease(new EditorPreferenceLeaseState());
                throw;
            }
        }

        private static bool Same(EditorPreferenceSnapshot a, EditorPreferenceSnapshot b) =>
            a.exists == b.exists && (!a.exists || a.path == b.path);

        private static void Check(bool value, string message)
        {
            if (!value) throw new Exception("Native preference regression: " + message);
        }
    }
}
