using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Avg.ProjectScriptEditor
{
    // Public because the in-memory Rider assembly calls these methods. Never writes EditorPrefs.
    public static class ProjectRiderPreferences
    {
        [Serializable] private sealed class Entry { public string key; public string value; }
        [Serializable] private sealed class Data { public List<Entry> entries = new List<Entry>(); }
        private static readonly object Gate = new object();
        private static Data data;
        private static string file;
        private static string riderPath;

        internal static void Initialize(string project, string editor)
        {
            lock (Gate)
            {
                file = Path.Combine(project, "UserSettings", "ProjectRiderIntegration.json");
                riderPath = editor;
                data = File.Exists(file) ? JsonUtility.FromJson<Data>(File.ReadAllText(file)) : new Data();
                if (data == null || data.entries == null) throw new InvalidDataException("Invalid project Rider preferences.");
            }
        }

        public static bool HasKey(string key)
        {
            lock (Gate) return key == "kScriptsDefaultApp" || data.entries.Exists(e => e.key == key);
        }
        public static string GetString(string key) => GetString(key, "");
        public static string GetString(string key, string fallback)
        {
            lock (Gate)
            {
                if (key == "kScriptsDefaultApp") return riderPath;
                return data.entries.Find(e => e.key == key)?.value ?? fallback;
            }
        }
        public static int GetInt(string key) => GetInt(key, 0);
        public static int GetInt(string key, int fallback)
        {
            // Respect the user's auto-refresh preference, without changing it.
            if (!HasKey(key) && (key == "kAutoRefreshMode" || key == "ScriptCompilationDuringPlay"))
                return EditorPrefs.GetInt(key, fallback);
            return int.TryParse(GetString(key, null), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }
        public static bool GetBool(string key) => GetBool(key, false);
        public static bool GetBool(string key, bool fallback)
        {
            if (!HasKey(key) && key == "kAutoRefresh") return EditorPrefs.GetBool(key, fallback);
            return bool.TryParse(GetString(key, null), out var value) ? value : fallback;
        }
        public static float GetFloat(string key) => GetFloat(key, 0);
        public static float GetFloat(string key, float fallback) =>
            float.TryParse(GetString(key, null), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        public static void SetString(string key, string value)
        {
            lock (Gate)
            {
                // The chosen executable is controlled exclusively by our project settings.
                if (key == "kScriptsDefaultApp") return;
                var entry = data.entries.Find(e => e.key == key);
                if (entry == null) data.entries.Add(new Entry { key = key, value = value });
                else entry.value = value;
                Save();
            }
        }
        public static void SetInt(string key, int value) => SetString(key, value.ToString(CultureInfo.InvariantCulture));
        public static void SetBool(string key, bool value) => SetString(key, value.ToString());
        public static void SetFloat(string key, float value) => SetString(key, value.ToString("R", CultureInfo.InvariantCulture));
        public static void DeleteKey(string key) { lock (Gate) { data.entries.RemoveAll(e => e.key == key); Save(); } }
        public static void DeleteAll() { lock (Gate) { data.entries.Clear(); Save(); } }
        private static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            // Mono's File.Replace is unreliable on some Windows filesystems. A failed write
            // can affect this optional local file only; initialization rejects malformed JSON.
            File.WriteAllText(file, JsonUtility.ToJson(data, true));
        }
    }
}
