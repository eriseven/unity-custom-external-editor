using System.Text.Json;

var output = Environment.GetEnvironmentVariable("PROJECT_SCRIPT_EDITOR_CAPTURE");
if (string.IsNullOrEmpty(output)) return 2;
File.WriteAllText(output, JsonSerializer.Serialize(new
{
    arguments = args,
    workingDirectory = Environment.CurrentDirectory,
    nodeMode = Environment.GetEnvironmentVariable("ELECTRON_RUN_AS_NODE"),
    ipcHook = Environment.GetEnvironmentVariable("VSCODE_IPC_HOOK_CLI")
}));
return 0;
