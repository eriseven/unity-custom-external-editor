using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Avg.ProjectScriptEditor.Tests
{
    public static class ProcessLaunchTests
    {
        [Serializable]
        private sealed class Capture
        {
            public string[] arguments;
            public string workingDirectory;
            public string nodeMode;
            public string ipcHook;
        }

        public static void Run()
        {
            var executable = Environment.GetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_RECORDER");
            if (string.IsNullOrEmpty(executable)) throw new Exception("Set PROJECT_SCRIPT_EDITOR_RECORDER to Recorder.exe.");
            var directory = Path.GetFullPath("Library/Process launch & 中文 " + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var file = Path.Combine(directory, "File with spaces.cs");
            File.WriteAllText(file, "fixture");
            var originalPath = EditorPrefs.GetString("kScriptsDefaultApp", "");
            var originalExists = EditorPrefs.HasKey("kScriptsDefaultApp");
            var oldCapture = Environment.GetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_CAPTURE");
            var oldNode = Environment.GetEnvironmentVariable("ELECTRON_RUN_AS_NODE");
            var oldIpc = Environment.GetEnvironmentVariable("VSCODE_IPC_HOOK_CLI");
            try
            {
                Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", "1");
                Environment.SetEnvironmentVariable("VSCODE_IPC_HOOK_CLI", "wrong-project");
                foreach (var kind in new[] { EditorKind.Rider, EditorKind.VSCode })
                {
                    var plan = EditorLauncher.Build(executable, kind, "", directory, file, 24, 8, ProjectScriptEditorController.Platform);
                    VerifyRoundTrip(plan, Path.Combine(directory, kind + ".json"));
                }
                VerifyRoundTrip(new EditorLaunchPlan
                {
                    Executable = executable, WorkingDirectory = directory, Platform = ProjectScriptEditorController.Platform,
                    Arguments = new[] { "", "two words", "a\"b", "C:\\trailing slash\\", "中文 & ; $(not-a-command) `literal`", "slash\\\"quote" }
                }, Path.Combine(directory, "escaping.json"));
                if (EditorPrefs.HasKey("kScriptsDefaultApp") != originalExists || EditorPrefs.GetString("kScriptsDefaultApp", "") != originalPath)
                    throw new Exception("Process launch changed the shared editor preference.");
                Debug.Log("REAL_PROCESS_LAUNCH_PASSED cases=3 global_preference_unchanged=true");
            }
            finally
            {
                Environment.SetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_CAPTURE", oldCapture);
                Environment.SetEnvironmentVariable("ELECTRON_RUN_AS_NODE", oldNode);
                Environment.SetEnvironmentVariable("VSCODE_IPC_HOOK_CLI", oldIpc);
            }
        }

        private static void VerifyRoundTrip(EditorLaunchPlan plan, string output)
        {
            Environment.SetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_CAPTURE", output);
            EditorLauncher.Start(plan);
            var deadline = DateTime.UtcNow.AddSeconds(10);
            Capture capture = null;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    if (File.Exists(output)) capture = JsonUtility.FromJson<Capture>(File.ReadAllText(output));
                }
                catch (IOException) { }
                catch (ArgumentException) { }
                if (capture?.arguments != null) break;
                Thread.Sleep(20);
            }
            if (capture == null || !plan.Arguments.SequenceEqual(capture.arguments) || capture.workingDirectory != plan.WorkingDirectory)
                throw new Exception("Child process received incorrect arguments or working directory: " + output);
            if (!string.IsNullOrEmpty(capture.nodeMode) || !string.IsNullOrEmpty(capture.ipcHook))
                throw new Exception("Inherited IDE terminal variables leaked into child process.");
        }
    }
}
