# Configuration & IDE Setup

This document explains the various configuration options available to you via the Unity **MCP Control Panel** (`Window > Antigravity > MCP Control Panel`) and how IDE integration works.

## The MCP Control Panel
The Control Panel is a Unity EditorWindow containing 4 main tabs.

### 1. Dashboard
The Dashboard provides a real-time statistical overview of the MCP Server running inside your Unity Editor.
- **Port:** The WebSocket port the router uses to talk to Unity (default: `8090`).
- **Server Status:** You can manually Stop or Restart the server.
- **Connected Clients:** Ensure this is '1' when your IDE's MCP client attaches.
- **Messages Processed:** Useful for debugging if the AI claims it sent a command but nothing happened.

### 2. Tools
By default, all 145+ tools are registered with the IDE. If you instruct your AI on a simple 2D game, having 3D Terrain tools registered wastes prompt tokens.
- You can enable/disable specific categories (e.g., Disable "Multiplayer" and "ProBuilder").
- **Presets:** Quickly apply a preset like "2D Game" or "Core" to instantly disable irrelevant categories.
*(Changes here require restarting your IDE's MCP connection to fetch the new tool list).*

### 3. IDE Setup
The Unity Plugin attempts to detect installations for:
- Antigravity
- Cursor
- Windsurf
- Claude Code Desktop
- Gemini CLI
- VS Code (Copilot/Cline)

If the **"Export Config"** button is clicked, it will generate the necessary `.json` file (`mcp.json`, `settings.json`, etc.) in the user's home or project directory, pointing directly to this project's MCP Router. 

**Router Build Button:**
- Clicking `Build Router (Release)` invokes `dotnet publish` to compile the Router. 
- Using the pre-built `.exe` drops Router startup time from ~1000ms down to ~50ms since it doesn't have to evaluate `dotnet run` dependencies every time the IDE spins up.

### 4. Security
Running an MCP server means giving an AI program full administrative rights over your Unity project codebase and assets. The Security tab allows you to restrict this:

**API Key Authentication:**
- Click `Generate` to create a 24-character hex key. 
- The Unity Server will now reject WebSocket commands that lack this key.
- IDE config generation automatically embeds this key into the config's `env` object. 

**Read-Only Mode:**
- If checked, destructive tools (`unity_object_delete`, `unity_asset_delete`, etc.) and mutation tools (`unity_object_create`, `unity_component_update`) will return an error string to the AI outlining that it is restricted.
- Inspection tools (`unity_object_find`, `unity_hierarchy_list`) will continue to work normally.

**Rate Limiting:**
- Prevents the AI from accidentally crashing Unity by locking the main thread in a tight loop of commands. Default is 60 commands / second.

**Audit Logging:**
- Outputs a line-by-line history of every command the AI executed, the arguments passed, and the outcome into a log file at `Logs/mcp_audit.log`. Useful if the AI deleted a file and you need to see exactly when and why it happened.
