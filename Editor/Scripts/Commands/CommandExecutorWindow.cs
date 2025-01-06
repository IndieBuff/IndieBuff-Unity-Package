using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;
using System;
using System.Linq;

// Data structure to hold parsed command information
public class CommandData
{
    public string MethodName { get; set; }
    public Dictionary<string, string> Parameters { get; set; }
    public string ExecutionResult { get; set; }

    public override string ToString()
    {
        return $"{MethodName}, {string.Join(", ", Parameters.Select(p => $"[\"{p.Key}\":\"{p.Value}\"]"))}";
    }
}

public class CommandExecutorWindow : EditorWindow
{
    private string commandInput = "";
    private Vector2 inputScrollPos;
    private Vector2 commandListScrollPos;
    private List<CommandData> parsedCommands = new List<CommandData>();
    private GUIStyle wrappedTextArea;
    private GUIStyle errorStyle;

    [MenuItem("Window/Command Executor")]
    public static void ShowWindow()
    {
        GetWindow<CommandExecutorWindow>("Command Executor");
    }

    private void OnEnable()
    {
        // Initialize GUI styles
        wrappedTextArea = new GUIStyle(EditorStyles.textArea)
        {
            wordWrap = true
        };

        errorStyle = new GUIStyle(EditorStyles.label)
        {
            normal = { textColor = Color.red },
            wordWrap = true
        };
    }

    private void OnGUI()
    {
        GUILayout.Label("Command Input", EditorStyles.boldLabel);
        
        // Input area
        EditorGUILayout.BeginVertical(GUI.skin.box);
        inputScrollPos = EditorGUILayout.BeginScrollView(inputScrollPos, GUILayout.Height(100));
        commandInput = EditorGUILayout.TextArea(commandInput, wrappedTextArea);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // Parse and Execute All buttons
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Parse Commands"))
        {
            ParseCommands();
        }
        
        if (GUILayout.Button("Execute All"))
        {
            ExecuteAllCommands();
        }
        EditorGUILayout.EndHorizontal();

        // Display parsed commands
        GUILayout.Label("Parsed Commands", EditorStyles.boldLabel);
        commandListScrollPos = EditorGUILayout.BeginScrollView(commandListScrollPos);
        
        for (int i = 0; i < parsedCommands.Count; i++)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            
            // Command display
            EditorGUILayout.LabelField($"Command {i + 1}:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(parsedCommands[i].ToString(), wrappedTextArea);

            // Execute button
            if (GUILayout.Button("Execute"))
            {
                ExecuteCommand(parsedCommands[i]);
            }

            // Display execution result if it exists
            if (!string.IsNullOrEmpty(parsedCommands[i].ExecutionResult))
            {
                EditorGUILayout.LabelField("Result:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(parsedCommands[i].ExecutionResult, 
                    parsedCommands[i].ExecutionResult.Contains("Failed") ? errorStyle : EditorStyles.label);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
        
        EditorGUILayout.EndScrollView();
    }
    
    private void ParseCommands()
    {
        parsedCommands.Clear();
        string[] lines = commandInput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (string line in lines)
        {
            try
            {
                Debug.Log($"Parsing command: {line}");
                var commandData = ParseCommandLine(line.Trim());
                if (commandData != null)
                {
                    parsedCommands.Add(commandData);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing command: {line}\nError: {e.Message}");
            }
        }
    }

    private CommandData ParseCommandLine(string line)
    {
        // Split the command and parameters
        var parts = line.Split(new[] { ',' }, 2);
        if (parts.Length != 2)
        {
            Debug.LogError($"Invalid command format: {line}");
            return null;
        }

        string methodName = parts[0].Trim();
        string paramString = parts[1].Trim();

        // Parse parameters by splitting on "][" to separate multiple parameter pairs
        string[] paramPairs = paramString.Trim('[', ']').Split(new[] { "],[" }, StringSplitOptions.RemoveEmptyEntries);


        var result = new Dictionary<string, string>();
        
        // This regex pattern matches key-value pairs where:
        // - Keys and values are wrapped in quotes
        // - Handles escaped quotes within the values
        // - Accounts for whitespace
        var pattern = @"""((?:[^""\\]|\\.)*)""\s*:\s*""((?:[^""\\]|\\.)*)""";
        
        var matches = Regex.Matches(paramPairs[0], pattern);
        
        foreach (Match match in matches)
        {
            if (match.Groups.Count == 3) // Group 0 is full match, 1 is key, 2 is value
            {
                string key = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                
                // Unescape any escaped characters if needed
                key = Regex.Unescape(key);
                value = Regex.Unescape(value);
                
                result[key] = value;
            }
        }


        return new CommandData
        {
            MethodName = methodName,
            Parameters = result
        };
    }

    private void ExecuteCommand(CommandData command)
    {
        try
        {
            // Get the PropertyManager type

            MethodInfo methodInfo = FindMethod(command.MethodName);

            if (methodInfo == null)
            {
                command.ExecutionResult = $"Failed: Method {command.MethodName} not found";
                return;
            }

            // Execute the method
            object result = methodInfo.Invoke(null, new object[] { command.Parameters });
            command.ExecutionResult = result?.ToString() ?? "Command executed successfully";
        }
        catch (Exception e)
        {
            command.ExecutionResult = $"Failed: {e.Message}";
            Debug.LogError($"Error executing command {command.MethodName}: {e}");
        }

        // Force the window to repaint to show the result
        Repaint();
    }

    private void ExecuteAllCommands()
    {
        foreach (var command in parsedCommands)
        {
            //Debug.Log($"Executing command: {command.MethodName}");
            //Debug.Log($"Parameters: {command.Parameters}");
            ExecuteCommand(command);
        }
    }

    public MethodInfo FindMethod(string methodName)
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(IndieBuff.Editor.ICommandManager).IsAssignableFrom(t) && !t.IsInterface)
            .Select(t => t.GetMethod(methodName, 
                BindingFlags.Public | BindingFlags.NonPublic | 
                BindingFlags.Instance | BindingFlags.Static))
            .FirstOrDefault(m => m != null);
    }
}