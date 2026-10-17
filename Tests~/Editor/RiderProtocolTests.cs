using System;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Avg.ProjectScriptEditor.Tests
{
    // Real RD socket peer using Rider's protocol classes. This is not the Rider desktop client.
    public static class RiderProtocolTests
    {
        [Serializable] private sealed class Endpoint { public int Port; }
        [Serializable] private sealed class Endpoints { public Endpoint[] items; }
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        private static object Read(object instance, string name) => Read(instance.GetType(), instance, name);
        private static object Read(Type type, object instance, string name) =>
            type.GetProperty(name, Flags)?.GetValue(instance) ?? type.GetField(name, Flags)?.GetValue(instance);

        public static void Run()
        {
            var baseline = EditorPrefs.GetString("kScriptsDefaultApp", "");
            var exists = EditorPrefs.HasKey("kScriptsDefaultApp");
            var rider = Environment.GetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_RIDER");
            ProjectScriptEditorSettings.instance.SetRiderIntegration(false);
            if (!ProjectScriptEditorController.Remember(rider, EditorKind.Rider, "", out var error)) throw new Exception(error);
            ProjectScriptEditorSettings.instance.SetRiderIntegration(true);
            if (!RiderIntegration.TryStart(out error)) throw new Exception(error);
            var assembly = RiderIntegration.Plugin;
            Type T(string name) => assembly.GetType(name, true);
            var lifetime = Read(T("JetBrains.Rider.Unity.Editor.PluginEntryPoint"), null, "AppDomainLifetime");
            var scheduler = Read(T("JetBrains.Rider.Unity.Editor.MainThreadDispatcher"), null, "Instance");
            var endpoints = JsonUtility.FromJson<Endpoints>("{\"items\":" + File.ReadAllText("Library/ProtocolInstance.json") + "}");
            var wire = Activator.CreateInstance(T("JetBrains.Rd.Impl.SocketWire+Client"), lifetime, scheduler, endpoints.items[0].Port, "IsolationTestPeer");
            wire.GetType().GetProperty("BackwardsCompatibleWireFormat").SetValue(wire, true);
            var serializers = Activator.CreateInstance(T("JetBrains.Rd.Impl.Serializers"));
            var identities = Activator.CreateInstance(T("JetBrains.Rd.Impl.Identities"), Enum.Parse(T("JetBrains.Rd.IdKind"), "Client"));
            var contexts = Array.CreateInstance(T("JetBrains.Rd.RdContextBase"), 0);
            var protocol = Activator.CreateInstance(T("JetBrains.Rd.Impl.Protocol"), "IsolationTestPeer", serializers, identities, scheduler, wire, lifetime, contexts);
            var model = Activator.CreateInstance(T("JetBrains.Rider.Model.Unity.BackendUnity.BackendUnityModel"), lifetime, protocol);
            var logging = Read(model, "ConsoleLogging");
            var signal = Read(logging, "_OnConsoleLogEvent");
            var eventType = T("JetBrains.Rider.Model.Unity.LogEvent");
            var parameter = Expression.Parameter(eventType);
            bool receivedLog = false;
            const string logMarker = "ISOLATED_PROTOCOL_LOG_DELIVERY";
            Action<object> onLog = evt => { if (evt.ToString().Contains(logMarker)) receivedLog = true; };
            var handler = Expression.Lambda(typeof(Action<>).MakeGenericType(eventType), Expression.Invoke(Expression.Constant(onLog), Expression.Convert(parameter, typeof(object))), parameter).Compile();
            signal.GetType().GetMethods().Single(m => m.Name == "Advise" && m.GetParameters().Length == 2).Invoke(signal, new[] { lifetime, handler });
            var pause = Read(Read(model, "PlayControls"), "Pause");
            var deadline = EditorApplication.timeSinceStartup + 30;
            object task = null;
            int phase = 0;
            void Poll()
            {
                try
                {
                    if (EditorPrefs.HasKey("kScriptsDefaultApp") != exists || EditorPrefs.GetString("kScriptsDefaultApp", "") != baseline)
                        throw new Exception("Protocol changed global preference");
                    if (EditorApplication.timeSinceStartup > deadline) throw new Exception("RD handshake/RPC timeout; status=" + RiderIntegration.Status);
                    if (!RiderIntegration.Status.StartsWith("Connected")) return;
                    if (task == null)
                    {
                        Debug.Log(logMarker);
                        var request = Read(model, "GetUnityEditorState");
                        var start = request.GetType().GetMethods().Single(m => m.Name == "Start" && m.GetParameters().Length == 3);
                        task = start.Invoke(request, new[] { lifetime, Read(T("JetBrains.Core.Unit"), null, "Instance"), scheduler });
                        return;
                    }
                    var property = Read(task, "Result");
                    var maybe = Read(property, "Maybe");
                    if (maybe == null || !(bool)Read(maybe, "HasValue")) return;
                    var result = Read(maybe, "Value");
                    if (!result.ToString().Contains("Idle")) throw new Exception("Unexpected editor state RPC result: " + result);
                    if (!receivedLog) return;
                    if (phase == 0) { pause.GetType().GetProperty("Value").SetValue(pause, true); phase = 1; return; }
                    if (phase == 1)
                    {
                        if (!EditorApplication.isPaused) return;
                        pause.GetType().GetProperty("Value").SetValue(pause, false);
                        phase = 2;
                        return;
                    }
                    if (EditorApplication.isPaused) return;
                    EditorApplication.update -= Poll;
                    RiderIntegration.Stop();
                    ProjectScriptEditorSettings.instance.SetRiderIntegration(false);
                    Debug.Log("RIDER_REAL_PROTOCOL_PASSED handshake=true editor_state_rpc=Idle console_delivery=true pause_control=true global_preference_unchanged=true");
                    EditorApplication.Exit(0);
                }
                catch (Exception e)
                {
                    EditorApplication.update -= Poll;
                    RiderIntegration.Stop();
                    EditorApplication.isPaused = false;
                    Debug.LogException(e);
                    EditorApplication.Exit(1);
                }
            }
            EditorApplication.update += Poll;
        }
    }
}
