using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using UnityEditor;
using UnityEngine;

namespace Avg.ProjectScriptEditor.Tests
{
    public static class RiderIntegrationTests
    {
        [Serializable] private sealed class Snapshot { public bool exists; public string path; }
        private static readonly string[] Keys = { "kScriptsDefaultApp", "com.jetbrains.rider.modifiedsourcefile", "Rider_SelectedLoggingLevel", "Rider_LogEventsCollectorEnabled", "ScriptCompilationDuringPlay" };
        private static Dictionary<string, string> baseline;
        private static int assertions;
        private static Dictionary<string, string> SnapshotPreferences() => Keys.ToDictionary(k => k,
            k => EditorPrefs.HasKey(k) + ":" + EditorPrefs.GetString(k, "<missing>") + ":" + EditorPrefs.GetInt(k, -123456) + ":" + EditorPrefs.GetBool(k, false));
        private static void Check(bool condition, string description) { if (!condition) throw new Exception(description); assertions++; }
        private static void Unchanged() => Check(baseline.SequenceEqual(SnapshotPreferences()), "Global preferences changed");

        public static void Run()
        {
            Prepare();
            RiderIntegration.Stop();
            Check(!RiderIntegration.Running, "Stop cancels the connection");
            Check(!File.Exists("Library/ProtocolInstance.json"), "Stop removes only this project's discovery file");
            Check(!RiderIntegration.TryStart(out var error) && error.Contains("Reload"), "Re-enable requires fresh domain");
            ProjectScriptEditorSettings.instance.SetRiderIntegration(false);
            Unchanged();
            Debug.Log("ISOLATED_RIDER_INTEGRATION_PASSED assertions=" + assertions);
        }

        public static void RunSuite()
        {
            Run();
            SmokeTests.Run();
            ProcessLaunchTests.Run();
        }

        private static void Prepare()
        {
            baseline = SnapshotPreferences();
            var rider = Environment.GetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_RIDER");
            if (string.IsNullOrEmpty(rider)) throw new Exception("Set PROJECT_SCRIPT_EDITOR_RIDER to the audited Rider executable.");
            var solution = Path.Combine(ProjectScriptEditorController.ProjectDirectory, new DirectoryInfo(ProjectScriptEditorController.ProjectDirectory).Name + ".sln");
            if (!File.Exists(solution)) File.WriteAllText(solution, "Microsoft Visual Studio Solution File, Format Version 12.00\nGlobal\nEndGlobal\n");
            ProjectScriptEditorSettings.instance.SetRiderIntegration(false);
            Check(ProjectScriptEditorController.Remember(rider, EditorKind.Rider, "", out var error), error);
            var dll = Path.Combine(Directory.GetParent(Path.GetDirectoryName(rider)).FullName, "plugins/rider-unity/EditorPlugin", RiderAssemblyAdapter.PluginName);
            var original = File.ReadAllBytes(dll);
            var adapted = RiderAssemblyAdapter.Adapt(original, out var count);
            Check(count > 10, "All preference calls adapted");
            using (var assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(adapted)))
            {
                bool ReferencesPrefs(TypeDefinition t) => t.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
                    .Any(i => i.Operand is MethodReference m && m.DeclaringType.FullName == "UnityEditor.EditorPrefs") || t.NestedTypes.Any(ReferencesPrefs);
                Check(!assembly.MainModule.Types.Any(ReferencesPrefs), "Adapted DLL has no native EditorPrefs call sites");
            }
            var changed = (byte[])original.Clone(); changed[changed.Length - 1] ^= 1;
            try { RiderAssemblyAdapter.Adapt(changed, out _); throw new Exception("Unknown DLL accepted"); }
            catch (NotSupportedException) { assertions++; }
            ProjectScriptEditorSettings.instance.SetRiderIntegration(true);
            Check(RiderIntegration.TryStart(out error), error);
            Check(RiderIntegration.Running && File.Exists("Library/ProtocolInstance.json"), "Project-local protocol started");
            var plugin = RiderIntegration.Plugin;
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var preferences = plugin.GetType("JetBrains.Rider.Unity.Editor.EditorPrefsWrapper");
            Check((string)preferences.GetProperty("ExternalScriptEditor", flags).GetValue(null) == rider, "DLL sees project Rider while Unity retains its global choice");
            preferences.GetProperty("ExternalScriptEditor", flags).SetValue(null, "attempted global write");
            Check((string)preferences.GetProperty("ExternalScriptEditor", flags).GetValue(null) == rider, "DLL cannot change editor choice");
            plugin.GetType("JetBrains.Rider.Unity.Editor.ModificationPostProcessor").GetMethod("OnWillCreateAsset", flags).Invoke(null, new object[] { "Assets/Fixture.cs.meta" });
            Check(ProjectRiderPreferences.GetBool("com.jetbrains.rider.modifiedsourcefile"), "Rider asset callback writes only project preferences");
            Check(File.Exists("UserSettings/ProjectRiderIntegration.json"), "Project preferences persist");
            Check(original.SequenceEqual(File.ReadAllBytes(dll)), "Rider installation DLL unchanged");
            Unchanged();
        }

        public static void Hold()
        {
            Prepare();
            File.WriteAllText("rider-baseline.json", JsonUtility.ToJson(new Snapshot { exists = EditorPrefs.HasKey("kScriptsDefaultApp"), path = EditorPrefs.GetString("kScriptsDefaultApp", "") }));
            File.WriteAllText("rider-ready.txt", File.ReadAllText("Library/ProtocolInstance.json"));
            var deadline = EditorApplication.timeSinceStartup + 480;
            bool connectedReported = false;
            EditorApplication.update += () =>
            {
                try
                {
                    Unchanged();
                    if (!connectedReported && RiderIntegration.Status.StartsWith("Connected"))
                    {
                        connectedReported = true;
                        File.WriteAllText("rider-connected.txt", RiderIntegration.Status);
                        Debug.Log("REAL_RIDER_CONNECTED " + Application.dataPath);
                    }
                    if (File.Exists("rider-finish.txt"))
                    {
                        RiderIntegration.Stop();
                        Unchanged();
                        ProjectScriptEditorSettings.instance.SetRiderIntegration(false);
                        Debug.Log("RIDER_CONCURRENT_ISOLATION_PASSED connected=" + connectedReported);
                        EditorApplication.Exit(0);
                    }
                    else if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Rider hold timeout");
                }
                catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
            };
        }
    }
}
