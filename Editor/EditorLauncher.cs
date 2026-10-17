using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Avg.ProjectScriptEditor
{
    internal enum EditorKind { Automatic, Rider, VSCode }
    internal enum LaunchPlatform { Windows, MacOS, Linux }

    internal sealed class EditorLaunchPlan
    {
        internal string Executable;
        internal string WorkingDirectory;
        internal string[] Arguments;
        internal LaunchPlatform Platform;
    }

    // Pure launch planning: no Unity preferences, editor registration or IDE integration callbacks.
    internal static class EditorLauncher
    {
        internal static EditorKind DetectKind(string path)
        {
            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (name.StartsWith("rider", StringComparison.OrdinalIgnoreCase)) return EditorKind.Rider;
            if (name.Equals("code", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("code.exe", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("code-insiders", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("code - insiders.exe", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Visual Studio Code.app", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Visual Studio Code - Insiders.app", StringComparison.OrdinalIgnoreCase)) return EditorKind.VSCode;
            throw new ArgumentException("Cannot identify this executable. Select Rider or VS Code explicitly.");
        }

        internal static string ResolveExecutable(string path, EditorKind kind, LaunchPlatform platform)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw new ArgumentException("Choose an absolute editor executable path.");
            if (path.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
            {
                if (platform != LaunchPlatform.MacOS)
                    throw new ArgumentException("macOS .app bundles can only be launched on macOS.");
                path = Path.Combine(path, "Contents", "MacOS", kind == EditorKind.Rider ? "rider" : "Electron");
            }
            if (!File.Exists(path)) throw new FileNotFoundException("Editor executable not found.", path);
            if (platform == LaunchPlatform.Windows && !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("On Windows select Rider's rider64.exe or VS Code's Code.exe, not a .cmd/.bat launcher.");
            return Path.GetFullPath(path);
        }

        internal static EditorLaunchPlan Build(string editorPath, EditorKind kind, string solutionPath,
            string projectDirectory, string filePath, int line, int column, LaunchPlatform platform)
        {
            editorPath = editorPath?.Trim();
            if (string.IsNullOrEmpty(editorPath)) throw new ArgumentException("Choose an editor executable first.");
            if (kind == EditorKind.Automatic) kind = DetectKind(editorPath);
            if (kind != EditorKind.Rider && kind != EditorKind.VSCode)
                throw new ArgumentException("Only Rider and VS Code are supported.");
            var executable = ResolveExecutable(editorPath, kind, platform);
            var project = Path.GetFullPath(projectDirectory);
            if (!Directory.Exists(project)) throw new DirectoryNotFoundException("Unity project directory not found.");
            var args = new List<string>();
            args.Add(kind == EditorKind.Rider ? ResolveRiderProject(project, solutionPath) : project);
            if (!string.IsNullOrEmpty(filePath))
            {
                var file = Path.GetFullPath(Path.IsPathRooted(filePath) ? filePath : Path.Combine(project, filePath));
                if (!File.Exists(file)) throw new FileNotFoundException("Script file not found.", file);
                if (kind == EditorKind.VSCode)
                {
                    if (line > 0)
                    {
                        args.Add("--goto");
                        args.Add(file + ":" + line + ":" + Math.Max(column, 1));
                    }
                    else args.Add(file);
                }
                else
                {
                    if (line > 0)
                    {
                        args.Add("--line");
                        args.Add(line.ToString());
                        if (column > 0) { args.Add("--column"); args.Add(column.ToString()); }
                    }
                    args.Add(file);
                }
            }
            return new EditorLaunchPlan
            {
                Executable = executable, WorkingDirectory = project, Arguments = args.ToArray(), Platform = platform
            };
        }

        private static string ResolveRiderProject(string project, string solution)
        {
            if (!string.IsNullOrWhiteSpace(solution))
            {
                var path = Path.GetFullPath(Path.IsPathRooted(solution) ? solution : Path.Combine(project, solution));
                if ((!path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) &&
                    !path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)) || !File.Exists(path))
                    throw new ArgumentException("Select an existing .sln or .slnx solution, or clear the solution field.");
                return path;
            }
            var projectName = new DirectoryInfo(project).Name;
            foreach (var extension in new[] { ".sln", ".slnx" })
            {
                var expected = Path.Combine(project, projectName + extension);
                if (File.Exists(expected)) return expected;
            }
            var solutions = Directory.GetFiles(project).Where(p =>
                p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (solutions.Length > 1)
                throw new ArgumentException("Multiple solutions found. Select this project's Rider solution in Project Script Editor settings.");
            return solutions.Length == 1 ? solutions[0] : project;
        }

        internal static string QuoteArgument(string argument, LaunchPlatform platform)
        {
            if (argument.IndexOf('\0') >= 0) throw new ArgumentException("Arguments cannot contain a NUL character.");
            if (platform != LaunchPlatform.Windows)
                return "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

            var result = new StringBuilder("\"");
            var slashes = 0;
            foreach (var character in argument)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                result.Append(character);
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }

        internal static ProcessStartInfo CreateStartInfo(EditorLaunchPlan plan)
        {
            var info = new ProcessStartInfo
            {
                FileName = plan.Executable,
                WorkingDirectory = plan.WorkingDirectory,
                Arguments = string.Join(" ", plan.Arguments.Select(arg => QuoteArgument(arg, plan.Platform))),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            // An IDE/agent terminal may export these; do not start Code.exe as Node or target its terminal's window.
            info.EnvironmentVariables.Remove("ELECTRON_RUN_AS_NODE");
            info.EnvironmentVariables.Remove("VSCODE_IPC_HOOK_CLI");
            return info;
        }

        internal static void Start(EditorLaunchPlan plan)
        {
            using (var process = Process.Start(CreateStartInfo(plan)))
                if (process == null) throw new InvalidOperationException("The editor process could not be started.");
        }
    }
}
