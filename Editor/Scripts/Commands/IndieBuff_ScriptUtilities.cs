using UnityEngine;
using UnityEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Reflection;

namespace IndieBuff.Editor
{
    public static class IndieBuff_CheckCompilation
    {

        private static readonly string[] curatedAssemblyPrefixes = { "Assembly-CSharp", "UnityEngine", "UnityEditor", "Unity.", "netstandard" };
        private static readonly Dictionary<string, ReportDiagnostic> suppressedDiagnostics = new()
    {
        { "CS0168", ReportDiagnostic.Suppress },
        { "CS0219", ReportDiagnostic.Suppress },
        { "CS8321", ReportDiagnostic.Suppress }
    };


        private static List<MetadataReference> GetRequiredReferences()
        {
            var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Debug).Assembly.Location)
        };

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                {
                    if (curatedAssemblyPrefixes.Any(prefix => assembly.FullName.StartsWith(prefix)))
                    {
                        try
                        {
                            references.Add(MetadataReference.CreateFromFile(assembly.Location));
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"Failed to add reference for assembly {assembly.FullName}: {e.Message}");
                        }
                    }
                }
            }

            string netstandardPath = Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location), "netstandard.dll");
            if (File.Exists(netstandardPath))
            {
                references.Add(MetadataReference.CreateFromFile(netstandardPath));
            }

            return references;
        }

        public static bool CompileWithRoslyn(string scriptContent, out string compilationLog)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(scriptContent);

            var compilationOptions = new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                allowUnsafe: true
            )
            .WithSpecificDiagnosticOptions(suppressedDiagnostics);

            var compilation = CSharpCompilation.Create(
                "DynamicScriptAssembly",
                new[] { syntaxTree },
                GetRequiredReferences(),
                compilationOptions
            );

            using (var ms = new MemoryStream())
            {
                var result = compilation.Emit(ms);

                if (!result.Success)
                {
                    compilationLog = GetCompilationLogs(result);
                    return false;
                }

                compilationLog = "Compilation successful";
                return true;
            }
        }

        private static string GetCompilationLogs(EmitResult result)
        {
            var diagnosticLogs = new StringBuilder();

            foreach (var diagnostic in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                var location = diagnostic.Location;
                if (location.IsInSource)
                {
                    var lineSpan = location.GetLineSpan();
                    diagnosticLogs.AppendLine(
                        $"Error {diagnostic.Id}: {diagnostic.GetMessage()} " +
                        $"(Line: {lineSpan.StartLinePosition.Line + 1}, " +
                        $"Column: {lineSpan.StartLinePosition.Character + 1})");
                }
                else
                {
                    diagnosticLogs.AppendLine($"Error {diagnostic.Id}: {diagnostic.GetMessage()}");
                }
            }

            return diagnosticLogs.ToString();
        }
    }




    public class ScriptAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {

            if (didDomainReload && EditorPrefs.GetBool(ScriptManager.WaitingToExecuteKey, false))
            {
                Debug.Log("Domain reload complete, executing pending command");
                ScriptManager.domainReloadInProgress = false;

                string paramsString = EditorPrefs.GetString(ScriptManager.PendingParamsKey, "");
                string[] cmds = paramsString.Split(new string[] { "<|>" }, StringSplitOptions.None);

                foreach (string cmd in cmds)
                {
                    if (!string.IsNullOrEmpty(cmd))
                    {
                        string[] methodAndParams = cmd.Split(new[] { "||" }, 2, StringSplitOptions.None);
                        if (methodAndParams.Length != 2) continue;

                        string methodName = methodAndParams[0];
                        string paramString = methodAndParams[1];

                        var parameters = new Dictionary<string, string>();
                        string[] pairs = paramString.Split('|');
                        foreach (string pair in pairs)
                        {
                            string[] keyValue = pair.Split('=');
                            if (keyValue.Length == 2)
                            {
                                parameters[keyValue[0]] = keyValue[1];
                            }
                        }

                        MethodInfo methodInfo = IndieBuff_CommandParser.FindMethod(methodName);

                        object result = methodInfo.Invoke(null, new object[] { parameters });
                        string cmdResult = result?.ToString() ?? "Command executed successfully";
                        Debug.Log(cmdResult);
                    }
                }
                EditorPrefs.SetBool(ScriptManager.WaitingToExecuteKey, false);
                EditorPrefs.SetString(ScriptManager.PendingParamsKey, "");

                EditorPrefs.DeleteKey(ScriptManager.WaitingToExecuteKey);
                EditorPrefs.DeleteKey(ScriptManager.PendingParamsKey);
            }
        }
    }
}