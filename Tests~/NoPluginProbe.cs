using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Copy only this file into Assets/Editor of a disposable project WITHOUT the package.
// Set PROJECT_SCRIPT_EDITOR_BASELINE to B's native-preference-baseline.json file.
public static class NoPluginProbe
{
    [Serializable]
    private sealed class Snapshot
    {
        public bool exists;
        public string path;
    }

    public static void Verify()
    {
        var file = Environment.GetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_BASELINE");
        if (string.IsNullOrEmpty(file))
            throw new InvalidOperationException("Set PROJECT_SCRIPT_EDITOR_BASELINE to the test snapshot file.");

        var expected = JsonUtility.FromJson<Snapshot>(File.ReadAllText(file));
        var exists = EditorPrefs.HasKey("kScriptsDefaultApp");
        var path = EditorPrefs.GetString("kScriptsDefaultApp", "");
        if (exists != expected.exists || (exists && path != expected.path))
            throw new Exception("Project without the package observed a changed global script editor after B exited.");

        Debug.Log("NO_PLUGIN_CROSS_VERSION_PREFERENCE_PASSED");
    }
}
