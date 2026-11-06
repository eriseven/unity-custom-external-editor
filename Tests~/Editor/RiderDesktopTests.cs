using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Avg.ProjectScriptEditor.Tests
{
    // Passive observer for a real Rider desktop. It never constructs an RD test peer.
    [InitializeOnLoad]
    public static class RiderDesktopTests
    {
        [Serializable] private sealed class State
        {
            public string baseline;
            public string status;
            public string failure;
            public bool playing;
            public bool paused;
            public int domains;
            public int connections;
            public bool sawPlay;
            public bool sawPause;
            public bool sawResume;
            public bool sawStop;
            public string deadline;
        }
        private const string StateFile = "Library/rider-desktop-state.json";
        private const string EventsFile = "Library/rider-desktop-events.txt";
        private static readonly string[] Keys = { "kScriptsDefaultApp", "com.jetbrains.rider.modifiedsourcefile", "Rider_SelectedLoggingLevel", "Rider_LogEventsCollectorEnabled", "ScriptCompilationDuringPlay", "kAutoRefresh", "kAutoRefreshMode" };
        private static State state;
        private static bool connected;
        private static bool observing;
        private static double nextPoll;
        private static string Snapshot() => string.Join("\n", Keys.Select(k => k + ":" + EditorPrefs.HasKey(k) + ":" + EditorPrefs.GetString(k, "<missing>") + ":" + EditorPrefs.GetInt(k, -123456) + ":" + EditorPrefs.GetBool(k, false)));
        private static bool Enabled => Environment.GetCommandLineArgs().Contains("-isolatedRiderDesktopTests");

        static RiderDesktopTests()
        {
            if (!Enabled) return;
            EditorApplication.delayCall += Resume;
        }

        public static void Start()
        {
            if (!Enabled || Application.isBatchMode) throw new Exception("Use a GUI Editor with -isolatedRiderDesktopTests.");
            state = new State { baseline = Snapshot(), deadline = DateTime.UtcNow.AddHours(1).ToString("O") };
            Save();
            ProjectScriptEditorSettings.instance.SetRiderIntegration(false);
            var rider = Environment.GetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_RIDER");
            if (!ProjectScriptEditorController.Remember(rider, EditorKind.Rider, "", out var error)) throw new Exception(error);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Rider integration fixture").AddComponent(Type.GetType("IntegrationFixture, Assembly-CSharp", true));
            EditorSceneManager.SaveScene(scene, "Assets/RiderValidation.unity");
            ProjectScriptEditorSettings.instance.SetRiderIntegration(true);
            if (!RiderIntegration.TryStart(out error)) throw new Exception(error);
            Resume();
        }

        private static void Resume()
        {
            if (observing || !File.Exists(StateFile)) return;
            observing = true;
            state = JsonUtility.FromJson<State>(File.ReadAllText(StateFile));
            state.domains++;
            Event("domain=" + state.domains);
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
            Save();
        }

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            try
            {
                if (Snapshot() != state.baseline) throw new Exception("Global preferences changed during real-client validation.");
                if (DateTime.UtcNow > DateTime.Parse(state.deadline).ToUniversalTime()) throw new Exception("Desktop validation timed out.");
                state.status = RiderIntegration.Status;
                var nowConnected = state.status.StartsWith("Connected", StringComparison.Ordinal);
                if (nowConnected && !connected)
                {
                    state.connections++;
                    Event("connected=" + state.connections);
                    Debug.Log("ISOLATED_RIDER_DESKTOP_CONSOLE domain=" + state.domains);
                }
                connected = nowConnected;
                var playing = EditorApplication.isPlaying;
                var paused = EditorApplication.isPaused;
                if (playing && !state.playing) { state.sawPlay = true; Event("play"); }
                if (paused && !state.paused) { state.sawPause = true; Event("pause"); }
                if (!paused && state.paused) { state.sawResume = true; Event("resume"); }
                if (!playing && state.playing) { state.sawStop = true; Event("stop"); }
                state.playing = playing;
                state.paused = paused;
                Save();
                if (File.Exists("Library/rider-desktop-recompile"))
                {
                    File.Delete("Library/rider-desktop-recompile");
                    Event("request-recompile");
                    CompilationPipeline.RequestScriptCompilation();
                }
                if (File.Exists("Library/rider-desktop-finish"))
                {
                    if (!state.sawPlay || !state.sawPause || !state.sawResume || !state.sawStop || state.connections < 2)
                        throw new Exception("Desktop controls and reconnect acceptance is incomplete.");
                    RiderIntegration.Stop();
                    ProjectScriptEditorSettings.instance.SetRiderIntegration(false);
                    Event("finished global_preference_unchanged=true");
                    EditorApplication.update -= Poll;
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception ex)
            {
                state.failure = ex.ToString();
                Save();
                Debug.LogException(ex);
                RiderIntegration.Stop();
                ProjectScriptEditorSettings.instance.SetRiderIntegration(false);
                EditorApplication.update -= Poll;
            }
        }

        private static void Save() => File.WriteAllText(StateFile, JsonUtility.ToJson(state, true));
        private static void Event(string message) => File.AppendAllText(EventsFile, DateTime.UtcNow.ToString("O") + " " + message + "\n");
    }
}
