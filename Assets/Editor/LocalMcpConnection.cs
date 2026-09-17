using System.IO;
using UnityEditor;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;

// Opt-in editor authoring connection. The marker lives in ignored Logs, so clones
// never connect automatically. This file and the MCP package are editor-only.
[InitializeOnLoad]
public static class LocalMcpConnection
{
    static bool connecting;
    static double nextAttempt;
    static LocalMcpConnection() { EditorApplication.update += TryConnect; }
    static async void TryConnect()
    {
        if (!File.Exists("Logs/Mcp/connect") || connecting || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextAttempt) return;
        nextAttempt = EditorApplication.timeSinceStartup + 15;
        if (MCPServiceLocator.TransportManager.IsRunning(TransportMode.Http)) return;
        connecting = true;
        try
        {
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
            EditorPrefs.SetString("MCPForUnity.HttpUrl", "http://127.0.0.1:8765");
            EditorPrefs.SetBool("MCPForUnity.TelemetryDisabled", true);
            await MCPServiceLocator.TransportManager.StartAsync(TransportMode.Http);
        }
        finally { connecting = false; }
    }
}
