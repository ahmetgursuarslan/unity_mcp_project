using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace UnityMcpRouter;

/// <summary>
/// Provides MCP Resources (Live Data Feeds) from Unity.
/// </summary>
[McpServerResourceType]
public class UnityResourcesProvider
{
    [McpServerResource(UriTemplate = "unity://console/errors", Name = "unity_console_errors", MimeType = "text/plain")]
    [System.ComponentModel.Description("Compilation and runtime errors from the Unity Console (last 50)")]
    public static async Task<string> ReadConsoleErrors(UnityWebSocketClient client, CancellationToken ct = default)
    {
        var response = await client.SendCommandAsync("unity_dev_get_compile_errors", null, ct);
        if (response.TryGetProperty("error", out var errorProp))
            throw new Exception($"Failed to read console: {errorProp.GetString()}");

        if (response.TryGetProperty("data", out var data))
            return data.ToString() ?? "{}";
            
        return "{}";
    }

    [McpServerResource(UriTemplate = "unity://scene/hierarchy", Name = "unity_scene_hierarchy", MimeType = "text/html")]
    [System.ComponentModel.Description("Abstract HTML-like tree representation of all active Canvases/UI in the scene")]
    public static async Task<string> ReadSceneHierarchy(UnityWebSocketClient client, CancellationToken ct = default)
    {
        var paramsJson = JsonDocument.Parse(JsonSerializer.Serialize(new { rootInstanceId = 0, depth = 10 })).RootElement;
        var response = await client.SendCommandAsync("unity_ui_dump_hierarchy", paramsJson, ct);
        if (response.TryGetProperty("error", out var errorProp))
            throw new Exception($"Failed to dump hierarchy: {errorProp.GetString()}");

        if (response.TryGetProperty("data", out var data) && data.TryGetProperty("uiTree", out var treeProp))
            return treeProp.GetString() ?? "<error/>";
            
        return "<error/>";
    }

    [McpServerResource(UriTemplate = "unity://project/info", Name = "unity_project_info", MimeType = "application/json")]
    [System.ComponentModel.Description("Basic Unity version, project name, and active scene info")]
    public static async Task<string> ReadProjectInfo(UnityWebSocketClient client, CancellationToken ct = default)
    {
        var response = await client.SendCommandAsync("unity_scene_list", null, ct);
        if (response.TryGetProperty("data", out var data))
            return data.ToString() ?? "{}";
            
        return "{}";
    }
}
