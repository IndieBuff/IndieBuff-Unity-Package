using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;

public static class ScriptConstants
{
    public const string PENDING_SCRIPTS_COUNT = "PendingScriptsCount";
    public const string PENDING_SCRIPT_CLASS = "PendingScriptClass_";
    public const string PENDING_SCRIPT_OBJECT = "PendingScriptObject_";
}

public class ScriptInfo
{
    public string Content { get; set; }
    public string FilePath { get; set; }
    public GameObject TargetObject { get; set; }
}

namespace IndieBuff.Editor
{
    public static class IndieBuff_DynamicScriptUtility
    {
        private static readonly string[] curatedAssemblyPrefixes = { "Assembly-CSharp", "UnityEngine", "UnityEditor", "Unity.", "netstandard" };
        private static readonly Dictionary<string, ReportDiagnostic> suppressedDiagnostics = new()
        {
            { "CS0168", ReportDiagnostic.Suppress },
            { "CS0219", ReportDiagnostic.Suppress },
            { "CS8321", ReportDiagnostic.Suppress }
        };

        public static bool ExecuteDynamicScript(string codeContent, out string error)
        {
            error = string.Empty;

            try
            {
                System.Reflection.Assembly assembly = CompileWithRoslyn(codeContent, out error);
                if (assembly == null) return false;

                Type type = assembly.GetType("IndieBuff_DynamicClass");
                MethodInfo method = type.GetMethod("Execute");
                method.Invoke(null, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.Log(ex.Message);
                error = ex.Message;
                return false;
            }
        }

        public static bool GenerateAndAttachMultipleScripts(List<(string scriptContent, string pathName, GameObject targetObject)> scriptDataList, out string error)
        {
            error = string.Empty;

            // First verify all scripts compile

            foreach (var (scriptContent, pathName, _) in scriptDataList)
            {
                if (CompileWithRoslyn(scriptContent, out string compilationLog) == null)
                {
                    error = $"Compilation failed for {pathName}:\n{compilationLog}";
                    Debug.LogError(error);
                    return false;
                }
            }


            try
            {

                EditorPrefs.SetInt(ScriptConstants.PENDING_SCRIPTS_COUNT, scriptDataList.Count);


                for (int i = 0; i < scriptDataList.Count; i++)
                {
                    var (scriptContent, pathName, targetObject) = scriptDataList[i];

                    string scriptPath = pathName;

                    var className = Path.GetFileNameWithoutExtension(pathName);


                    File.WriteAllText(scriptPath, scriptContent);

                    if (targetObject != null)
                    {
                        EditorPrefs.SetString(ScriptConstants.PENDING_SCRIPT_CLASS + i, className);
                        EditorPrefs.SetString(ScriptConstants.PENDING_SCRIPT_OBJECT + i, targetObject.GetInstanceID().ToString());
                    }
                }

                EditorPrefs.SetBool("ShouldAttachDynamicScript", true);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (!EditorApplication.isCompiling)
                {
                    Debug.Log("No compilation detected. Forcing compilation...");
                    CompilationPipeline.RequestScriptCompilation();
                }
                else
                {
                    Debug.Log("Compilation is already in progress.");
                }
                return true;
            }
            catch (Exception ex)
            {
                error = $"Failed to generate scripts: {ex.Message}";
                return false;
            }
        }


        private static List<MetadataReference> GetRequiredReferences()
        {
            var references = new List<MetadataReference>();

            references.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Debug).Assembly.Location));

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

        private static System.Reflection.Assembly CompileWithRoslyn(string scriptContent, out string compilationLog)
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
                    return null;
                }

                compilationLog = "Compilation successful";
                ms.Seek(0, SeekOrigin.Begin);
                return System.Reflection.Assembly.Load(ms.ToArray());
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



        private static List<ScriptInfo> pendingMonoBehaviours = new();

        public static void RegisterMonoBehaviourScript(string content, string filePath, GameObject target = null)
        {
            pendingMonoBehaviours.Add(new ScriptInfo
            {
                Content = content,
                FilePath = filePath,
                TargetObject = target
            });
        }

        public static void ExecuteRuntimeScript(string runtimeCode)
        {
            pendingMonoBehaviours.Clear();

            if (!ExecuteDynamicScript(runtimeCode, out string error))
            {
                Debug.LogError($"Failed to execute code: {error}");
                return;
            }

            // Process the collected scripts
            ProcessPendingScripts();
        }

        private static void ProcessPendingScripts()
        {
            var scriptsToAttach = new List<(string scriptContent, string pathName, GameObject targetObject)>();

            foreach (var script in pendingMonoBehaviours)
            {
                if (script.TargetObject != null)
                {
                    // This script needs to be attached, use your existing system
                    scriptsToAttach.Add((script.Content, script.FilePath, script.TargetObject));
                }
                else
                {
                    // Just write to file
                    File.WriteAllText(script.FilePath, script.Content);
                }
            }

            if (scriptsToAttach.Count > 0)
            {
                Debug.Log(scriptsToAttach.Count);
                if (!GenerateAndAttachMultipleScripts(
                    scriptsToAttach, out string error))
                {
                    Debug.LogError($"Failed to generate and attach scripts: {error}");
                }
            }
        }
    }
}