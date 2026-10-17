using System;
using System.IO;

namespace Avg.ProjectScriptEditor.Tests
{
    internal static class PolicyTests
    {
        internal static int Run(string fixtureDirectory)
        {
            var assertions = 0;
            void Check(bool condition, string description)
            {
                if (!condition)
                    throw new Exception("Project Script Editor policy test failed: " + description);
                assertions++;
            }

            Directory.CreateDirectory(fixtureDirectory);
            var executable = Path.Combine(fixtureDirectory, "fake-editor.exe");
            File.WriteAllText(executable, "test fixture; never executed");
            var bundle = Path.Combine(fixtureDirectory, "Fake.app");
            Directory.CreateDirectory(bundle);

            Check(!EditorOverride.ValidatePath(null, out _), "reject null");
            Check(!EditorOverride.ValidatePath("", out _), "reject OS association");
            Check(!EditorOverride.ValidatePath("  ", out _), "reject whitespace");
            Check(!EditorOverride.ValidatePath("fake-editor.exe", out _), "reject relative path");
            Check(!EditorOverride.ValidatePath(executable + ".missing", out _), "reject removed executable");
            Check(!EditorOverride.ValidatePath(fixtureDirectory, out _), "reject ordinary directory");
            Check(EditorOverride.ValidatePath(executable, out _), "accept executable path");
            Check(EditorOverride.ValidatePath(bundle, out _), "accept macOS bundle");

            var calls = 0;
            string selected = null;
            Action<string> select = path => { calls++; selected = path; };
            Check(!EditorOverride.TryRestore(false, executable, false, true, select, out _), "disabled override is inert");
            Check(!EditorOverride.TryRestore(true, executable, true, true, select, out _), "batch mode is inert");
            Check(!EditorOverride.TryRestore(true, executable, false, false, select, out _), "background window is inert");
            Check(calls == 0, "skipped restores do not touch preferences");
            Check(!EditorOverride.TryRestore(true, executable + ".missing", false, true, select, out var missingError)
                && missingError != null && calls == 0, "missing editor preserves preference");
            Check(EditorOverride.TryRestore(true, executable, false, true, select, out _)
                && selected == executable && calls == 1, "focused project restores its editor");
            selected = "another project's editor";
            Check(EditorOverride.TryRestore(true, executable, false, true, select, out _)
                && selected == executable && calls == 2, "subsequent focus restores again");
            Check(!EditorOverride.TryRestore(true, executable, false, true,
                path => throw new InvalidOperationException("integration failure"), out var failure)
                && failure.Contains("integration failure"), "integration exceptions are reported");
            return assertions;
        }
    }
}
