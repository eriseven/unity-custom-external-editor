using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using UnityEditor.Compilation;
using UnityEngine;

namespace Avg.ProjectScriptEditor.Tests
{
    // Requires the official Lib.Harmony 2.2.0 net472 DLL, auto-referenced, in the test project.
    public static class HarmonyCoexistenceTests
    {
        public static void Run()
        {
            var harmony = System.Reflection.Assembly.Load("0Harmony");
            var embeddedType = harmony.GetType("Mono.Cecil.TypeDefinition", true);
            Require(harmony.GetName().Version == new Version(2, 2, 0, 0), "Expected Harmony 2.2.0");
            Require(embeddedType != typeof(TypeDefinition), "Both Cecil implementations must be present");
            Require(typeof(TypeDefinition).Assembly.GetName().Name == "Mono.Cecil", "Use standalone Cecil");

            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            foreach (var name in new[] { "Avg.ProjectScriptEditor.Editor", "Avg.ProjectScriptEditor.Tests" })
            {
                var references = assemblies.Single(a => a.name == name).compiledAssemblyReferences;
                Require(references.Any(p => Path.GetFileName(p) == "Mono.Cecil.dll"), name + " must reference Cecil");
                Require(!references.Any(p => Path.GetFileName(p) == "0Harmony.dll"), name + " must exclude Harmony");
            }

            // The fix must not disable Harmony's auto-reference for other project scripts.
            var consumer = assemblies.Single(a => a.name == "Assembly-CSharp-Editor");
            Require(consumer.compiledAssemblyReferences.Any(p => Path.GetFileName(p) == "0Harmony.dll"),
                "Other editor scripts must still reference Harmony");
            using (var assembly = AssemblyDefinition.ReadAssembly(typeof(ProjectScriptEditorController).Assembly.Location))
                Require(assembly.MainModule.Types.Any(t => t.Name == "RiderAssemblyAdapter"), "Cecil can read the plugin assembly");

            SmokeTests.Run();
            ProcessLaunchTests.Run();
            Debug.Log("HARMONY_COEXISTENCE_PASSED version=2.2.0 independent_references=true");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
