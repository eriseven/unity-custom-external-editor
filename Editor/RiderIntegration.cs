using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Avg.ProjectScriptEditor
{
    [InitializeOnLoad]
    public static class RiderIntegration
    {
        private static Assembly plugin;
        private static CancellationTokenSource lifetime;
        private static string loadedPath;
        private static string state = "Disabled";
        internal static bool Running => lifetime != null && !lifetime.IsCancellationRequested;
        internal static Assembly Plugin => plugin;

        static RiderIntegration()
        {
            EditorApplication.delayCall += Reconcile;
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }

        internal static string Status
        {
            get
            {
                if (!Running) return state;
                try
                {
                    var models = plugin.GetType("JetBrains.Rider.Unity.Editor.UnityEditorProtocol")
                        .GetField("Models", BindingFlags.Static | BindingFlags.Public).GetValue(null) as IEnumerable;
                    foreach (var model in models)
                    {
                        var connected = model.GetType().GetField("Connected").GetValue(model);
                        if ((bool)connected.GetType().GetProperty("Value").GetValue(connected)) return "Connected to Rider (isolated, experimental)";
                    }
                }
                catch { return "Listening; connection status unavailable"; }
                return "Listening for Rider (isolated, experimental)";
            }
        }

        internal static void Reconcile()
        {
            var settings = ProjectScriptEditorSettings.instance;
            if (!settings.OverrideEnabled || !settings.RiderIntegrationEnabled)
            {
                if (Running) Stop();
                return;
            }
            if (Application.isBatchMode) return;
            if (!TryStart(out var error)) state = error;
        }

        internal static bool TryStart(out string error)
        {
            try
            {
                var settings = ProjectScriptEditorSettings.instance;
                if (!settings.OverrideEnabled || !settings.RiderIntegrationEnabled) throw new InvalidOperationException("Rider integration is disabled.");
                if (Application.isBatchMode && !Environment.GetCommandLineArgs().Contains("-riderIntegrationTests"))
                    throw new InvalidOperationException("Rider integration does not start in batch mode.");
                var kind = settings.Kind == EditorKind.Automatic ? EditorLauncher.DetectKind(settings.EditorPath) : settings.Kind;
                if (kind != EditorKind.Rider) { Stop(); throw new InvalidOperationException("Rider integration requires this project's Rider selection."); }
                if (Application.platform != RuntimePlatform.WindowsEditor) throw new NotSupportedException("This experimental adapter is validated on Windows only.");
                if (!Application.unityVersion.StartsWith("2022.3.", StringComparison.Ordinal) && !Application.unityVersion.StartsWith("6000.5.", StringComparison.Ordinal))
                    throw new NotSupportedException("This preview supports Unity 2022.3 and 6000.5 only.");
                var path = EditorLauncher.ResolveExecutable(settings.EditorPath, kind, LaunchPlatform.Windows);
                var project = ProjectScriptEditorController.ProjectDirectory;
                var solution = EditorLauncher.Build(path, kind, settings.SolutionPath, project, null, -1, -1, LaunchPlatform.Windows).Arguments[0];
                if (!solution.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || !string.Equals(Path.GetDirectoryName(solution), project, StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException("Isolated integration requires an existing .sln in this Unity project's root. .slnx and external solutions are not supported by this preview.");
                if (Running && loadedPath == path) { error = null; return true; }
                if (Running) Stop();
                if (plugin != null) throw new InvalidOperationException("Reload scripts or restart this Unity project to change/re-enable the Rider connection.");
                if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name.StartsWith("JetBrains.Rider.Unity.Editor.Plugin.", StringComparison.Ordinal)))
                    throw new InvalidOperationException("A Rider integration is already loaded by another component. Restart with the global editor set to VS Code before enabling isolated integration.");
                var root = Directory.GetParent(Path.GetDirectoryName(path)).FullName;
                var dll = Path.Combine(root, "plugins", "rider-unity", "EditorPlugin", RiderAssemblyAdapter.PluginName);
                var bytes = RiderAssemblyAdapter.Adapt(File.ReadAllBytes(dll), out var redirected);
                ProjectRiderPreferences.Initialize(ProjectScriptEditorController.ProjectDirectory, path);
                plugin = Assembly.Load(bytes);
                var method = plugin.GetType(RiderAssemblyAdapter.EntryType, true).GetMethod("Initialize",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(CancellationToken) }, null);
                if (method == null) throw new MissingMethodException("Rider Initialize(CancellationToken) not found.");
                lifetime = new CancellationTokenSource();
                method.Invoke(null, new object[] { lifetime.Token });
                var initialized = plugin.GetType(RiderAssemblyAdapter.EntryType).GetField("ourInitialised", BindingFlags.Static | BindingFlags.NonPublic);
                if (initialized == null || !(bool)initialized.GetValue(null)) throw new InvalidOperationException("Rider did not initialize (batch mode requires the integration test flag).");
                loadedPath = path;
                state = "Listening for Rider (isolated, experimental)";
                Debug.Log("[Project Script Editor] Isolated Rider integration initialized; redirected preference calls=" + redirected);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                Stop();
                error = exception.GetBaseException().Message;
                state = error;
                return false;
            }
        }

        internal static void Stop()
        {
            if (lifetime != null && !lifetime.IsCancellationRequested)
            {
                try { lifetime.Cancel(); }
                catch (Exception exception) { Debug.LogWarning("[Project Script Editor] Rider connection cleanup: " + exception.Message); }
            }
            state = plugin == null ? "Disabled" : "Stopped; reload scripts or restart to reconnect";
        }

        // Called only by the adapted DLL. Never invokes the globally selected IDE or its initialization.
        public static void RefreshAssets() => AssetDatabase.Refresh();
    }
}
