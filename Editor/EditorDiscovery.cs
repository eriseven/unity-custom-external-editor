using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using Microsoft.Win32;
using UnityEngine;

namespace Avg.ProjectScriptEditor
{
    internal sealed class EditorInstallation
    {
        internal string Path;
        internal EditorKind Kind;
        internal string Version;
        internal string Label => ((Kind == EditorKind.Rider ? "Rider" :
            Path.IndexOf("insiders", StringComparison.OrdinalIgnoreCase) >= 0 ? "VS Code Insiders" : "VS Code") +
            (string.IsNullOrEmpty(Version) ? "" : " " + Version) + " — " + Path).Replace("/", "\u2215");
    }

    internal static class EditorDiscovery
    {
        [Serializable] private sealed class ProductInfo { public string version; }
        internal static StringComparer PathComparer(LaunchPlatform platform) =>
            platform == LaunchPlatform.Windows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        internal static List<EditorInstallation> FindInstalled()
        {
            var platform = ProjectScriptEditorController.Platform;
            var roots = new List<string>();
            var files = new List<string>();
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (platform == LaunchPlatform.Windows)
            {
                foreach (var folder in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs") })
                {
                    roots.AddRange(Directories(folder).Where(d => IsEditorDirectory(Path.GetFileName(d))));
                    roots.AddRange(Directories(Path.Combine(folder, "JetBrains")).Where(d => IsEditorDirectory(Path.GetFileName(d))));
                }
                roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JetBrains/Toolbox/apps"));
                ReadWindowsInstallations(roots, files);
            }
            else if (platform == LaunchPlatform.MacOS)
            {
                roots.AddRange(Directories("/Applications").Where(d => IsEditorDirectory(Path.GetFileName(d))));
                roots.AddRange(Directories(Path.Combine(home, "Applications")).Where(d => IsEditorDirectory(Path.GetFileName(d))));
                roots.Add(Path.Combine(home, "Library/Application Support/JetBrains/Toolbox/apps"));
            }
            else
            {
                roots.AddRange(Directories("/opt").Where(d => IsEditorDirectory(Path.GetFileName(d))));
                roots.Add("/usr/share/code");
                roots.Add("/usr/share/code-insiders");
                var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (string.IsNullOrEmpty(data) || !Path.IsPathRooted(data)) data = Path.Combine(home, ".local/share");
                roots.Add(Path.Combine(data, "JetBrains/Toolbox/apps"));
            }
            foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                var directory = entry.Trim().Trim('"');
                if (!LocalAbsolute(directory)) continue;
                foreach (var name in platform == LaunchPlatform.Windows
                    ? new[] { "rider64.exe", "rider.exe", "Code.exe", "Code - Insiders.exe" }
                    : new[] { "rider", "rider.sh", "code", "code-insiders" }) files.Add(Path.Combine(directory, name));
                if (string.Equals(Path.GetFileName(directory), "bin", StringComparison.OrdinalIgnoreCase))
                {
                    var parent = Path.GetDirectoryName(directory);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        files.Add(Path.Combine(parent, "Code.exe"));
                        files.Add(Path.Combine(parent, "Code - Insiders.exe"));
                    }
                }
            }
            return Scan(roots, files, platform);
        }

        private static bool IsEditorDirectory(string name) => name.IndexOf("rider", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.StartsWith("Microsoft VS Code", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Microsoft Visual Studio Code", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("Visual Studio Code", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("vscode", StringComparison.OrdinalIgnoreCase);

        internal static List<EditorInstallation> Scan(IEnumerable<string> roots, IEnumerable<string> files, LaunchPlatform platform)
        {
            var found = new Dictionary<string, EditorInstallation>(PathComparer(platform));
            foreach (var file in files) Add(file, platform, found);
            var visited = new HashSet<string>(PathComparer(platform));
            var pending = new Queue<KeyValuePair<string, int>>();
            foreach (var root in roots) if (LocalAbsolute(root)) pending.Enqueue(new KeyValuePair<string, int>(root, 0));
            while (pending.Count > 0 && visited.Count < 1024)
            {
                var item = pending.Dequeue();
                string directory;
                try { directory = Path.GetFullPath(item.Key); }
                catch (Exception ex) when (IsPathError(ex)) { continue; }
                if (!visited.Add(directory) || !Directory.Exists(directory)) continue;
                if (platform == LaunchPlatform.MacOS && directory.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                {
                    Add(directory, platform, found);
                    continue;
                }
                var installation = false;
                foreach (var relative in platform == LaunchPlatform.Windows
                    ? new[] { "bin/rider64.exe", "bin/rider.exe", "Code.exe", "Code - Insiders.exe" }
                    : new[] { "bin/rider.sh", "bin/rider", "code", "code-insiders" })
                    installation |= Add(Path.Combine(directory, relative), platform, found);
                // Stop at installation roots; only explore version/channel directories, not IDE contents.
                if (installation || item.Value >= 4) continue;
                foreach (var child in Directories(directory))
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0)
                            pending.Enqueue(new KeyValuePair<string, int>(child, item.Value + 1));
                    }
                    catch (Exception ex) when (IsPathError(ex)) { }
                }
            }
            return found.Values.OrderBy(i => i.Kind).ThenBy(i => i.Label, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool Add(string file, LaunchPlatform platform, Dictionary<string, EditorInstallation> found)
        {
            if (!LocalAbsolute(file)) return false;
            try
            {
                var path = Path.GetFullPath(file.Trim().Trim('"'));
                if (!File.Exists(path) && !(platform == LaunchPlatform.MacOS && Directory.Exists(path))) return false;
                var kind = EditorLauncher.DetectKind(path);
                if (platform == LaunchPlatform.Windows && kind == EditorKind.Rider &&
                    string.Equals(Path.GetFileName(path), "rider.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var preferred = Path.Combine(Path.GetDirectoryName(path), "rider64.exe");
                    if (File.Exists(preferred)) path = preferred;
                }
                if (found.ContainsKey(path)) return true;
                var executable = EditorLauncher.ResolveExecutable(path, kind, platform);
                var root = Path.GetDirectoryName(executable);
                if (kind == EditorKind.Rider) root = Path.GetDirectoryName(root);
                var metadata = kind == EditorKind.Rider ? Path.Combine(root, "product-info.json") : Path.Combine(root, "resources/app/package.json");
                if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                    metadata = Path.Combine(path, kind == EditorKind.Rider ? "Contents/Resources/product-info.json" : "Contents/Resources/app/package.json");
                var version = ReadVersion(metadata);
                if (string.IsNullOrEmpty(version) && platform == LaunchPlatform.Windows)
                {
                    try { version = FileVersionInfo.GetVersionInfo(executable).ProductVersion; }
                    catch (Exception ex) when (IsPathError(ex)) { }
                }
                found.Add(path, new EditorInstallation { Path = path, Kind = kind, Version = version });
                return true;
            }
            catch (Exception ex) when (IsPathError(ex)) { return false; }
        }

        private static string ReadVersion(string file)
        {
            try
            {
                if (!File.Exists(file) || new FileInfo(file).Length > 1024 * 1024) return null;
                return JsonUtility.FromJson<ProductInfo>(File.ReadAllText(file))?.version;
            }
            catch (Exception ex) when (IsPathError(ex)) { return null; }
        }

        private static bool LocalAbsolute(string path)
        {
            try { return !string.IsNullOrWhiteSpace(path) && Path.IsPathRooted(path) && !path.StartsWith(@"\\", StringComparison.Ordinal); }
            catch (Exception ex) when (IsPathError(ex)) { return false; }
        }
        private static bool IsPathError(Exception ex) => ex is IOException || ex is UnauthorizedAccessException ||
            ex is SecurityException || ex is ArgumentException || ex is NotSupportedException || ex is System.ComponentModel.Win32Exception;
        private static string[] Directories(string path)
        {
            try { return LocalAbsolute(path) && Directory.Exists(path) ? Directory.GetDirectories(path) : Array.Empty<string>(); }
            catch (Exception ex) when (IsPathError(ex)) { return Array.Empty<string>(); }
        }

        internal static string IconExecutable(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = Environment.ExpandEnvironmentVariables(value.Trim());
            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                var end = value.IndexOf('"', 1);
                return end > 1 ? value.Substring(1, end - 1) : null;
            }
            var comma = value.LastIndexOf(',');
            if (comma >= 0 && int.TryParse(value.Substring(comma + 1), out _)) value = value.Substring(0, comma);
            return value.Trim();
        }

        private static void ReadWindowsInstallations(List<string> roots, List<string> files)
        {
            foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (var registry = RegistryKey.OpenBaseKey(hive, view))
                    {
                        using (var uninstall = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false))
                        {
                            foreach (var key in uninstall?.GetSubKeyNames() ?? Array.Empty<string>())
                            {
                                try
                                {
                                    using (var app = uninstall.OpenSubKey(key, false))
                                    {
                                        var name = app?.GetValue("DisplayName") as string ?? "";
                                        if (!IsEditorDirectory(name)) continue;
                                        var location = app.GetValue("InstallLocation") as string;
                                        if (!string.IsNullOrEmpty(location)) roots.Add(Environment.ExpandEnvironmentVariables(location.Trim().Trim('"')));
                                        files.Add(IconExecutable(app.GetValue("DisplayIcon") as string));
                                    }
                                }
                                catch (Exception ex) when (IsPathError(ex)) { }
                            }
                        }
                        foreach (var name in new[] { "rider64.exe", "rider.exe", "Code.exe", "Code - Insiders.exe" })
                        using (var app = registry.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + name, false))
                            files.Add(IconExecutable(app?.GetValue("") as string));
                    }
                }
                catch (Exception ex) when (IsPathError(ex)) { }
            }
        }
    }
}
