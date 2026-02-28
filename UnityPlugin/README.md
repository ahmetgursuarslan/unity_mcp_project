# Unity MCP — Model Context Protocol for Unity Editor

AI-powered Unity Editor control via the Model Context Protocol (MCP). Connect any MCP-compatible AI tool (Antigravity, VS Code Copilot, Cursor, Claude Code, Gemini CLI, Windsurf) to Unity for full editor automation.

## Features

- **~145 tools** across 31 categories covering all Unity subsystems
- **MCP Control Panel** — Unity EditorWindow for server control, tool management, IDE config export
- **Multi-IDE support** — one-click config generation for 6 IDEs
- **Security** — API key auth, rate limiting, read-only mode, audit logging
- **Multi-client** — multiple IDEs can connect simultaneously

## Quick Start

### 1. Install the Plugin
Copy `UnityPlugin/Editor/` into your Unity project's `Assets/Editor/Antigravity.MCP/` folder.

### 2. Build the Router
```bash
cd UnityMcpRouter
dotnet build
```

### 3. Configure Your IDE

**Option A (Automatic):** In Unity, go to **Window > Antigravity > MCP Control Panel > IDE Setup** and click "Export Config" for your IDE.

**Option B (Manual):** Add to your IDE's MCP config:
```json
{
  "mcpServers": {
    "unity-mcp-controller": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/UnityMcpRouter"],
      "env": {
        "UNITY_WS_PORT": "8090",
        "UNITY_REQUEST_TIMEOUT": "30",
        "LOG_LEVEL": "error"
      }
    }
  }
}
```

### 4. Start Using
Open Unity, the MCP server starts automatically. Your AI tool can now control the editor.

## Architecture

```
AI IDE → [stdio JSON-RPC] → .NET Router → [WebSocket :8090] → Unity Plugin → Unity Editor APIs
```

## Tool Categories

| Category | Tools | Description |
|----------|-------|-------------|
| Scene | 4 | Load, create, save, list scenes |
| GameObject | 7 | Create, delete, find, inspect, update, duplicate, find-by-path |
| Component | 3 | Add, remove, update components |
| Hierarchy | 2 | List tree, reparent |
| Editor Control | 5 | Play/stop, refresh, compile, menu items |
| Material | 5 | Create, assign, set properties, textures |
| Prefab | 5 | Create, instantiate, apply/revert/unpack |
| Asset | 7 | Import, move, delete, find, dependencies, labels |
| Import Settings | 4 | Texture, model, audio import config |
| Physics | 4 | Raycast, overlap, settings, collision matrix |
| Lighting | 5 | Lights, bake, probes, environment |
| Navigation | 5 | NavMesh bake, agents, obstacles, pathfinding |
| LOD & Performance | 6 | LOD groups, occlusion, static flags, profiler |
| Animation | 7 | Animator controllers, states, transitions, clips |
| Audio | 6 | Sources, mixers, snapshots |
| UI Toolkit | 7 | Query, create, style, bind, UXML/USS generation |
| Rendering | 6 | Cameras, post-processing, quality, screenshots |
| Player Settings | 7 | Build config, resolution, time, graphics |
| 2D Systems | 9 | Sprites, tilemaps, 2D physics, sorting |
| Spline | 5 | Create, knots, extrude, animate |
| Terrain | 6 | Create, heightmap, paint, trees, details |
| VFX | 4 | Particles, VFX Graph |
| ProBuilder | 6 | Mesh creation, export |
| Editor Utils | 8 | Console, selection, scene view, undo, prefs |
| Multiplayer | 5 | Netcode setup, NetworkObjects |
| ECS/DOTS | 5 | SubScene, entity guidance |
| Sentis ML | 4 | Model loading, inference guidance |
| Addressables | 4 | Mark, group, build |
| Script | 3 | Create, read, edit C# scripts |
| Build | 3 | Build player, settings, scene list |
| Package Manager | 4 | List, add, remove, search UPM packages |

## MCP Control Panel

Access via **Window > Antigravity > MCP Control Panel**:

- **Dashboard** — Server status, port, connected clients, messages
- **Tools** — Enable/disable categories, presets (Full, 3D Game, 2D Game, Core, Multiplayer)
- **IDE Setup** — Auto-detect and export configs, build Router exe
- **Security** — API key, rate limiting, read-only mode, audit log

## Requirements

- Unity 6 (6000.0+)
- .NET 8 SDK (for Router)
- MCP-compatible AI tool

## License

MIT
