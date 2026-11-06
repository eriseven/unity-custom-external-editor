using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Avg.ProjectScriptEditor.Tests
{
    internal sealed class DiscoveryTestWindow : EditorWindow { }

    public static class EditorDiscoveryTests
    {
        private static int assertions;
        private static void Click(Button button) => typeof(Clickable).GetMethod("SimulateSingleClick",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            .Invoke(button.clickable, new object[] { null, 0 });
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("Discovery test failed: " + message);
            assertions++;
        }

        private static string Write(string root, string relative, string text = "not executed")
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return Path.GetFullPath(path);
        }

        private static string Snapshot() => string.Join("\n", new[] { "kScriptsDefaultApp", "com.jetbrains.rider.modifiedsourcefile",
            "Rider_SelectedLoggingLevel", "Rider_LogEventsCollectorEnabled", "ScriptCompilationDuringPlay", "kAutoRefresh", "kAutoRefreshMode" }
            .Select(k => k + ":" + EditorPrefs.HasKey(k) + ":" + EditorPrefs.GetString(k, "<missing>") + ":" + EditorPrefs.GetInt(k, -123456) + ":" + EditorPrefs.GetBool(k, false)));

        public static void RunSuite()
        {
            Run();
            SmokeTests.Run();
            ProcessLaunchTests.Run();
        }

        public static void Run()
        {
            assertions = 0;
            var baseline = Snapshot();
            var root = Path.GetFullPath("Library/DiscoveryTests/" + Guid.NewGuid().ToString("N"));
            var rider1 = Write(root, "Rider old folder/bin/rider64.exe");
            var riderAlias = Write(root, "Rider old folder/bin/rider.exe");
            Write(root, "Rider old folder/product-info.json", "{\"version\":\"2026.2.4.1\"}");
            var rider2 = Write(root, "Toolbox/Rider/ch-0/262.123/bin/rider64.exe");
            Write(root, "Toolbox/Rider/ch-0/262.123/product-info.json", "{\"version\":\"2026.3\"}");
            var code = Write(root, "Microsoft VS Code/Code.exe");
            Write(root, "Microsoft VS Code/resources/app/package.json", "{\"version\":\"1.100.0\"}");
            var insiders = Write(root, "Microsoft VS Code Insiders/Code - Insiders.exe");
            Write(root, "Microsoft VS Code Insiders/resources/app/package.json", "broken metadata");
            var duplicate = Path.Combine(root, "Microsoft VS Code", "..", "Microsoft VS Code", "Code.exe");
            var found = EditorDiscovery.Scan(new[] { root, root, Path.Combine(root, "missing") },
                new[] { code, duplicate, code.ToUpperInvariant(), riderAlias, Path.Combine(root, "gone/Code.exe"), "relative/Code.exe", null }, LaunchPlatform.Windows);
            Check(found.Count == 4, "multiple versions, stale paths and duplicate sources");
            Check(found.Count(i => i.Kind == EditorKind.Rider) == 2, "both Rider installations retained");
            Check(found.Single(i => i.Path == rider1).Version == "2026.2.4.1", "actual product metadata beats directory name");
            Check(found.Single(i => i.Path == rider2).Version == "2026.3", "legacy Toolbox nested build discovered");
            Check(found.Single(i => i.Path == code).Version == "1.100.0", "VS Code package version");
            Check(found.Single(i => i.Path == insiders).Label.Contains("Insiders"), "bad metadata does not hide installation");
            Check(found.Select(i => i.Label).Distinct().Count() == 4, "labels distinguish every installation");
            Check(EditorDiscovery.IconExecutable("\"" + rider1 + "\",0") == rider1, "quoted registry icon");
            Check(EditorDiscovery.IconExecutable(code + ",-1") == code, "unquoted registry icon with spaces");
            Check(EditorDiscovery.IconExecutable("\"broken") == null, "malformed registry icon ignored");

            var macRoot = Path.Combine(root, "mac");
            Write(macRoot, "Rider.app/Contents/MacOS/rider");
            Write(macRoot, "Rider.app/Contents/Resources/product-info.json", "{\"version\":\"2026.2\"}");
            Write(macRoot, "Visual Studio Code.app/Contents/MacOS/Electron");
            Check(EditorDiscovery.Scan(new[] { macRoot }, Array.Empty<string>(), LaunchPlatform.MacOS).Count == 2, "macOS bundle layout fixtures");
            var linuxRoot = Path.Combine(root, "linux");
            Write(linuxRoot, "Rider/bin/rider.sh");
            Write(linuxRoot, "code/code");
            Check(EditorDiscovery.Scan(new[] { linuxRoot }, Array.Empty<string>(), LaunchPlatform.Linux).Count == 2, "Linux directory layout fixtures");

            var settings = ProjectScriptEditorSettings.instance;
            settings.SetRiderIntegration(false);
            settings.Remember(rider1, EditorKind.Rider, "");
            var discover = ProjectScriptEditorSettingsProvider.Discover;
            var provider = ProjectScriptEditorSettingsProvider.CreateProvider();
            var window = ScriptableObject.CreateInstance<DiscoveryTestWindow>();
            try
            {
                ProjectScriptEditorSettingsProvider.Discover = () => found;
                window.Show();
                var ui = window.rootVisualElement;
                provider.OnActivate("", ui);
                var menu = ui.Q<DropdownField>("installedEditors");
                var path = ui.Q<TextField>("editorPath");
                var kind = ui.Q<DropdownField>("editorKind");
                Check(menu.choices.Count == 5 && menu.value == found.Single(i => i.Path == rider1).Label, "saved installation selected on opening");
                Check(menu.choices.All(label => !label.Contains("/")) && !new EditorInstallation { Kind = EditorKind.Rider, Path = "/opt/rider/bin/rider.sh" }.Label.Contains("/"), "menu labels cannot become slash-delimited submenus");
                menu.value = found.Single(i => i.Path == code).Label;
                Check(path.value == code && kind.index == (int)EditorKind.VSCode, "selection fills path and kind");
                Check(settings.EditorPath == rider1, "selection alone does not persist");
                Click(ui.Q<Button>("saveEditor"));
                Check(settings.EditorPath == code && settings.Kind == EditorKind.VSCode, "save applies selection to project");
                var manual = Path.Combine(root, "portable/Code.exe");
                path.value = manual;
                Check(menu.index == 0, "manual path leaves detected selection");
                ProjectScriptEditorSettingsProvider.Discover = () => found.Take(1).ToList();
                Click(ui.Q<Button>("refreshEditors"));
                Check(path.value == manual && menu.index == 0 && menu.choices.Count == 2, "refresh retains unsaved manual input");
                ProjectScriptEditorSettingsProvider.Discover = () => found;
                Click(ui.Q<Button>("refreshEditors"));
                menu.value = found.Single(i => i.Path == rider2).Label;
                Check(path.value == rider2 && kind.index == (int)EditorKind.Rider, "select another Rider version");
                ProjectScriptEditorSettingsProvider.Discover = () => found.Where(i => i.Path != rider2).ToList();
                Click(ui.Q<Button>("refreshEditors"));
                Check(path.value == rider2 && menu.index == 0, "removed installation retains explicit path, no silent fallback");
                ProjectScriptEditorSettingsProvider.Discover = () => new System.Collections.Generic.List<EditorInstallation>();
                Click(ui.Q<Button>("refreshEditors"));
                Check(menu.choices.Count == 1 && path.value == rider2, "empty scan preserves manual selection");
                Check(settings.EditorPath == code, "refresh and edits leave saved settings intact");
            }
            finally
            {
                ProjectScriptEditorSettingsProvider.Discover = discover;
                provider.OnDeactivate();
                window.Close();
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            var actual = EditorDiscovery.FindInstalled();
            watch.Stop();
            foreach (var installation in actual) Debug.Log("DISCOVERED_EDITOR " + installation.Label);
            var expectedRider = Environment.GetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_RIDER");
            var expectedCode = Environment.GetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_CODE");
            if (!string.IsNullOrEmpty(expectedRider)) Check(actual.Any(i => EditorDiscovery.PathComparer(LaunchPlatform.Windows).Equals(i.Path, expectedRider)), "actual installed Rider discovered");
            if (!string.IsNullOrEmpty(expectedCode)) Check(actual.Any(i => EditorDiscovery.PathComparer(LaunchPlatform.Windows).Equals(i.Path, expectedCode)), "actual installed VS Code discovered");
            Check(Snapshot() == baseline, "native global preferences unchanged across discovery, UI selection and save");
            Debug.Log("EDITOR_DISCOVERY_TESTS_PASSED assertions=" + assertions + " actual_installations=" + actual.Count + " scan_ms=" + watch.ElapsedMilliseconds);
        }

        public static void Preview()
        {
            if (Application.isBatchMode) throw new Exception("Preview requires a GUI test Editor.");
            var baseline = Snapshot();
            var deadline = EditorApplication.timeSinceStartup + 600;
            File.WriteAllText("Library/discovery-preview-ready.txt", baseline);
            ProjectScriptEditorController.OpenSettings();
            EditorApplication.update += () =>
            {
                if (Snapshot() != baseline || EditorApplication.timeSinceStartup > deadline)
                {
                    Debug.LogError("DISCOVERY_PREVIEW_FAILED preferences changed or timed out");
                    EditorApplication.Exit(1);
                }
                else if (File.Exists("Library/discovery-preview-finish"))
                {
                    Debug.Log("DISCOVERY_PREVIEW_PASSED global_preference_unchanged=true saved=" + ProjectScriptEditorSettings.instance.EditorPath);
                    EditorApplication.Exit(0);
                }
            };
        }
    }
}
