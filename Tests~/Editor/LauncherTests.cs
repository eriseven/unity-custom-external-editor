using System;
using System.IO;
using System.Linq;

namespace Avg.ProjectScriptEditor.Tests
{
    internal static class LauncherTests
    {
        internal static int Run(string root)
        {
            int count = 0;
            void Check(bool value, string name)
            {
                if (!value) throw new Exception("Launcher test failed: " + name);
                count++;
            }
            void Reject(Action action, string name)
            {
                try { action(); } catch (Exception) { count++; return; }
                throw new Exception("Launcher unexpectedly accepted: " + name);
            }
            Directory.CreateDirectory(root);
            var project = Path.Combine(root, "工程 & project spaces");
            Directory.CreateDirectory(project);
            var rider = Path.Combine(root, "rider64.exe");
            var code = Path.Combine(root, "Code.exe");
            var file = Path.Combine(project, "Script with spaces.cs");
            File.WriteAllText(rider, "never executed");
            File.WriteAllText(code, "never executed");
            File.WriteAllText(file, "// fixture");
            EditorLaunchPlan Build(string editor, EditorKind kind = EditorKind.Automatic, string solution = "", int line = 42, int column = 7) =>
                EditorLauncher.Build(editor, kind, solution, project, file, line, column, LaunchPlatform.Windows);
            var plan = Build(rider);
            Check(plan.Arguments.SequenceEqual(new[] { project, "--line", "42", "--column", "7", file }), "Rider folder and caret");
            plan = Build(code);
            Check(plan.Arguments.SequenceEqual(new[] { project, "--goto", file + ":42:7" }), "VS Code project and caret");
            Check(!plan.Arguments.Contains("--reuse-window"), "do not force the last active project's window");
            Check(plan.WorkingDirectory == project && plan.Executable == code, "explicit executable and working directory");
            Check(Build(code, line: 4, column: -1).Arguments.Last() == file + ":4:1", "missing column normalized");
            Check(Build(code, line: -1).Arguments.SequenceEqual(new[] { project, file }), "VS Code no line number");
            Check(Build(rider, line: -1).Arguments.SequenceEqual(new[] { project, file }), "Rider no line number");
            Check(Build(rider, column: -1).Arguments.SequenceEqual(new[] { project, "--line", "42", file }), "Rider no column");
            var projectOnly = EditorLauncher.Build(code, EditorKind.VSCode, "", project, null, -1, -1, LaunchPlatform.Windows);
            Check(projectOnly.Arguments.SequenceEqual(new[] { project }), "open project without a script");
            var solution = Path.Combine(project, "Chosen.slnx");
            File.WriteAllText(solution, "fixture");
            Check(Build(rider).Arguments[0] == solution, "single solution discovered");
            File.WriteAllText(Path.Combine(project, "Second.sln"), "fixture");
            Reject(() => Build(rider), "ambiguous solutions");
            Check(Build(rider, solution: "Chosen.slnx").Arguments[0] == solution, "explicit relative solution");
            var expected = Path.Combine(project, new DirectoryInfo(project).Name + ".sln");
            File.WriteAllText(expected, "fixture");
            Check(Build(rider).Arguments[0] == expected, "project-named solution takes priority");
            Check(Build(code).Arguments[0] == project, "VS Code ignores solutions");
            Reject(() => Build(rider, solution: "missing.sln"), "missing solution");
            Reject(() => Build(""), "empty executable");
            Reject(() => Build("Code.exe"), "relative executable");
            Reject(() => Build(code + ".missing", EditorKind.VSCode), "missing executable");
            var unknown = Path.Combine(root, "Portable.exe");
            File.WriteAllText(unknown, "fixture");
            Reject(() => Build(unknown), "unknown automatic editor");
            Check(Build(unknown, EditorKind.VSCode).Executable == unknown, "manual editor for portable install");
            var batch = Path.Combine(root, "code.cmd");
            File.WriteAllText(batch, "never executed");
            Reject(() => Build(batch, EditorKind.VSCode), "Windows command scripts");
            Reject(() => EditorLauncher.Build(code, EditorKind.VSCode, "", project, "missing.cs", 1, 1, LaunchPlatform.Windows), "missing script");
            Reject(() => Build(code, (EditorKind)99), "unknown editor enum");
            var bundle = Path.Combine(root, "Rider.app");
            Directory.CreateDirectory(Path.Combine(bundle, "Contents", "MacOS"));
            var bundleExecutable = Path.Combine(bundle, "Contents", "MacOS", "rider");
            File.WriteAllText(bundleExecutable, "fixture");
            Check(EditorLauncher.ResolveExecutable(bundle, EditorKind.Rider, LaunchPlatform.MacOS) == bundleExecutable, "macOS Rider bundle");
            Reject(() => EditorLauncher.ResolveExecutable(bundle, EditorKind.Rider, LaunchPlatform.Windows), "macOS bundle on Windows");
            Check(EditorLauncher.DetectKind("Visual Studio Code.app") == EditorKind.VSCode, "macOS Code detection");
            Check(EditorLauncher.DetectKind("rider.sh") == EditorKind.Rider, "Linux Rider detection");
            var info = EditorLauncher.CreateStartInfo(plan);
            Check(!info.UseShellExecute && info.CreateNoWindow, "no command shell");
            Check(EditorLauncher.QuoteArgument("", LaunchPlatform.Windows) == "\"\"", "empty argument");
            Check(EditorLauncher.QuoteArgument("C:\\Space Dir\\", LaunchPlatform.Windows) == "\"C:\\Space Dir\\\\\"", "trailing backslash");
            Check(EditorLauncher.QuoteArgument("a\"b", LaunchPlatform.Windows) == "\"a\\\"b\"", "embedded quote");
            Reject(() => EditorLauncher.QuoteArgument("a\0b", LaunchPlatform.Windows), "NUL in arguments");
            return count;
        }
    }
}
