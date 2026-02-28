# Unity MCP Installation Guide

This guide will walk you through the process of installing and configuring the Unity Model Context Protocol (MCP) Bridge. The system consists of two parts: the **Unity Plugin** (runs inside Unity) and the **MCP Router** (a .NET console application that connects your IDE to Unity).

## Prerequisites
- **Unity Editor:** Unity 6 (6000.0 or newer)
- **.NET SDK:** .NET 8.0 SDK (required to build and run the MCP Router)
- **MCP-Compatible AI IDE:** Antigravity, Cursor, Windsurf, Claude Code, Gemini CLI, Codex Terminal, or VS Code (with Cline/Roo/Copilot).

---

## Step 1: Install the Unity Plugin

You can install the Unity Plugin using the Unity Package Manager (UPM) or by directly copying the files.

### Option A: Install via Unity Package Manager (Recommended)
1. Open your Unity project.
2. Open the **Package Manager** (`Window > Package Manager`).
3. Click the **+** button in the top left corner and select **Add package from disk...**.
4. Navigate to the downloaded `UnityPlugin` folder and select the `package.json` file.
5. Unity will install the **Antigravity MCP Bridge** package.

### Option B: Manual Installation
1. Copy the `UnityPlugin` folder into your Unity project's `Assets` directory (e.g., `Assets/Antigravity.MCP/`).
2. Wait for Unity to compile the scripts.

---

## Step 2: Configure the Plugin and Extract the Router

Once the plugin is installed, an MCP Control Panel will be available in the Unity Editor.

1. In Unity, go to **Window > Antigravity > MCP Control Panel**.
2. The server should state **● RUNNING** in green. By default, it runs on port `8090`. (You can change this in the Dashboard tab).
3. Switch to the **IDE Setup** tab.

### Compiling the Router
For the best performance and to avoid having the AI run `dotnet run` every time, compile the Router into a standalone executable:
1. In the **IDE Setup** tab, scroll down to **Router Build**.
2. Click the **🔨 Build Router (Release)** button.
3. Wait for the success dialogue. This creates a `.exe` file that your IDE configurations will automatically use.
*(If you do not do this, the IDE will run the Router directly from the source code using `dotnet run`, which has a slight startup delay).*

---

## Step 3: Connect your AI IDE

The MCP Control Panel provides 1-click configuration for popular IDEs.

1. Ensure your Unity Editor is open and the MCP server is running.
2. In the MCP Control Panel, go to the **IDE Setup** tab.
3. Find your IDE in the list (e.g., Antigravity, Cursor, Windsurf).
   - If detected, it will show a ✅ icon.
4. Click **Export Config** next to your IDE.
5. The JSON configuration will be automatically placed in the correct location on your system (e.g., `.cursor/mcp.json`, `.antigravity/settings.json`).

*Note: Restart your IDE or refresh its MCP connection (if applicable) for the changes to take effect.*

### Manual IDE Configuration
If your IDE is not listed or the auto-export fails, you can manually add the following to your IDE's MCP configuration settings:

```json
{
  "mcpServers": {
    "unity-mcp-controller": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/Path/To/Your/unity_mcp_project/UnityMcpRouter"
      ],
      "env": {
        "UNITY_WS_PORT": "8090",
        "UNITY_REQUEST_TIMEOUT": "30",
        "LOG_LEVEL": "error"
      }
    }
  }
}
```
*(If you built the Router .exe, point the `command` directly to the `UnityMcpRouter.exe` instead and leave `args` empty).*

---

## Step 4: Security and Advanced Settings (Optional)

In modern enterprise or secure environments, you might want to lock down the MCP server.

Go to the **Security** tab in the MCP Control Panel:
- **API Key Authentication:** Generate a unique API key. If generated, the MCP Router will require this key in its environment variables (`MCP_API_KEY`). The **Export Config** buttons will automatically include this key for you.
- **Read-Only Mode:** If enabled, the AI will only be able to *read* data (finding objects, listing hierarchies, reading properties) but will be blocked from creating, modifying, or deleting any Unity assets or GameObjects.
- **Rate Limiting:** Protect the Unity main thread by limiting the maximum number of requests a rogue AI loop can send per second.
- **Audit Logging:** Logs every command executed by the AI into an audit trail file (`Logs/mcp_audit.log`).

## Troubleshooting

- **Connection Refused / Router fails to start:** Ensure port `8090` is not being used by another application. Change the port in the Unity Dashboard and recreate your IDE config.
- **Missing Resource Errors / Attribute Errors in Router:** Ensure you are using .NET 8 and standard `ModelContextProtocol` NuGet package (v1.0.0).
- **Unity freezing:** Ensure you have not set the Rate Limit too high if the AI is sending massive amounts of Data (like huge UI Hierarchy dumps).
