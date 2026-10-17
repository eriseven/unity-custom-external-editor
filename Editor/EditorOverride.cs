using System;
using System.IO;

namespace Avg.ProjectScriptEditor
{
    // Kept independent of Unity's global preferences so restoration can be tested safely.
    internal static class EditorOverride
    {
        internal static bool ValidatePath(string path, out string error)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Choose a specific editor. Open by file extension cannot be saved as a project override.";
                return false;
            }

            if (!Path.IsPathRooted(path) ||
                (!File.Exists(path) && !(path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path))))
            {
                error = "Editor not found. Choose an existing executable or macOS .app bundle using an absolute path.";
                return false;
            }

            error = null;
            return true;
        }

        internal static bool TryRestore(bool enabled, string path, bool batchMode, bool focused,
            Action<string> setEditor, out string error)
        {
            error = null;
            if (!enabled || batchMode || !focused)
                return false;

            if (!ValidatePath(path, out error))
                return false;

            try
            {
                // Always initialize the process-local editor integration as well as the shared preference.
                // Another Unity process may have changed the preference since our last focus event.
                setEditor(path);
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not activate the editor: " + exception.Message;
                return false;
            }
        }
    }
}
