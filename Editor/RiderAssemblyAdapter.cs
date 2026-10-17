using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEditor;

namespace Avg.ProjectScriptEditor
{
    internal static class RiderAssemblyAdapter
    {
        // Audited Windows Rider 2026.2.3.1, build 262.10968.170, Unity 2022.3+ plugin.
        internal const string SupportedHash = "1E6818D3C65AB2B2DA92632F5721612324EE34F208C042C449FF7353B662A48A";
        internal const string PluginName = "JetBrains.Rider.Unity.Editor.Plugin.2022.3.Net46.Repacked.dll";
        internal const string EntryType = "JetBrains.Rider.Unity.Editor.PluginEntryPoint";

        internal static byte[] Adapt(byte[] original, out int redirected)
        {
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(original)).Replace("-", "") != SupportedHash)
                    throw new NotSupportedException("This Rider DLL has not been audited. Supported: Windows Rider 2026.2.3.1 (262.10968.170). Independent launching remains available.");

            using (var resolver = new DefaultAssemblyResolver())
            using (var input = new MemoryStream(original))
            {
                resolver.AddSearchDirectory(Path.GetDirectoryName(typeof(object).Assembly.Location));
                resolver.AddSearchDirectory(Path.Combine(EditorApplication.applicationContentsPath, "NetStandard/ref/2.1.0"));
                resolver.AddSearchDirectory(Path.Combine(EditorApplication.applicationContentsPath, "Managed"));
                resolver.AddSearchDirectory(Path.Combine(EditorApplication.applicationContentsPath, "Managed/UnityEngine"));
                using (var assembly = AssemblyDefinition.ReadAssembly(input, new ReaderParameters { AssemblyResolver = resolver }))
                {
                    var module = assembly.MainModule;
                    redirected = 0;
                    int refreshMethods = 0;
                    foreach (var type in module.Types.SelectMany(AllTypes))
                    foreach (var method in type.Methods.Where(m => m.HasBody))
                    {
                        // Do not call UPM initialization or the globally selected IDE's solution generator.
                        if (method.Name == "SyncSolution" && method.Parameters.Count == 0 &&
                            (type.FullName == "JetBrains.Rider.Unity.Editor.RiderPackageInterop" ||
                             type.FullName == "JetBrains.Rider.Unity.Editor.UnityUtils"))
                        {
                            method.Body = new Mono.Cecil.Cil.MethodBody(method);
                            var il = method.Body.GetILProcessor();
                            il.Append(il.Create(OpCodes.Call, module.ImportReference(typeof(RiderIntegration).GetMethod(nameof(RiderIntegration.RefreshAssets)))));
                            il.Append(il.Create(OpCodes.Ret));
                            refreshMethods++;
                        }
                        foreach (var instruction in method.Body.Instructions)
                        {
                            if (!(instruction.Operand is MethodReference target)) continue;
                            if (target.DeclaringType.FullName == "UnityEditor.EditorPrefs")
                            {
                                var replacement = typeof(ProjectRiderPreferences).GetMethods(BindingFlags.Public | BindingFlags.Static)
                                    .SingleOrDefault(m => m.Name == target.Name && m.ReturnType.FullName == target.ReturnType.FullName &&
                                        m.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(target.Parameters.Select(p => p.ParameterType.FullName)));
                                if (replacement == null) throw new NotSupportedException("Unsupported Rider preference API: " + target.FullName);
                                instruction.Operand = module.ImportReference(replacement);
                                redirected++;
                            }
                            else if (target.DeclaringType.FullName == "Unity.CodeEditor.CodeEditor" ||
                                     target.Name == "SetExternalScriptEditor" || target.Name == "SetCodeEditor")
                                throw new NotSupportedException("Unexpected global editor access: " + target.FullName);
                        }
                    }
                    if (redirected == 0 || refreshMethods != 2) throw new InvalidDataException("Unexpected Rider DLL structure.");
                    using (var output = new MemoryStream()) { assembly.Write(output); return output.ToArray(); }
                }
            }
        }

        private static IEnumerable<TypeDefinition> AllTypes(TypeDefinition type) =>
            new[] { type }.Concat(type.NestedTypes.SelectMany(AllTypes));
    }
}
