# Unity MCP Architecture Overview

The Unity MCP project follows a decoupled, client-server architecture designed to keep the Unity Editor isolated from the heavy .NET dependencies of the Model Context Protocol (MCP) SDK, while allowing seamless two-way communication.

## System Diagram

```mermaid
graph LR
    IDE[AI IDE (Client)] -- stdio JSON-RPC --> Router[MCP Router (.NET 8)]
    Router -- WebSocket JSON --> UnityPlugin[Unity Plugin (Editor)]
    UnityPlugin -- Unity C# API --> Unity[Unity Subsystems]
    
    subgraph Unity Engine
        UnityPlugin
        Unity
    end
```

## 1. The MCP Router (`UnityMcpRouter`)
The Router is a standalone .NET 8 console application. It acts as the official MCP Server that the AI IDE connects to via standard input/output (`stdio`).

### Responsibilities:
- **MCP Protocol Handling:** Utilizes the official `ModelContextProtocol` SDK to handle the complex JSON-RPC handshakes, tool registrations, and resource bindings.
- **Tool Proxies:** Exposes `[McpServerTool]` definitions to the AI. When the AI calls a tool (e.g., `unity_object_create`), the Router intercepts it, serializes the arguments, and forwards it to Unity.
- **Resource Hosting:** Uses `[McpServerResource]` attributes to provide live data feeds (like `unity://scene/hierarchy`).
- **Authentication & Validation:** Checks for required environment variables (`MCP_API_KEY`, `UNITY_WS_PORT`) before connecting.

## 2. The WebSocket Bridge (`UnityWebSocketClient`)
Communication between the Router and Unity happens exclusively via a secure local WebSocket connection. 
- Fast serialization using `System.Text.Json`.
- Supports cancellation tokens and timeouts to prevent hanging the IDE if Unity enters playmode or compiles.

## 3. The Unity Plugin (`UnityPlugin`)
The plugin lives inside the `Assets/` or `Packages/` folder of the user's Unity project. 

### Core Components:
- **`McpEditorServer.cs`**: A lightweight WebSocket server running on a background thread in the Editor. Listens for incoming tool requests from the Router.
- **`MainThreadDispatcher.cs`**: Unity's API is not thread-safe. All commands received via the socket are queued into a ConcurrentQueue and executed during the `EditorApplication.update` loop on the main Unity thread.
- **`CommandDispatcher.cs`**: Routes incoming JSON payload to the correct internal C# method (e.g., mapping `"unity_object_delete"` to the internal delete handler).
- **`McpToolRegistry.cs`**: Maintains the list of available tools, categories, and handles enablement state based on User preferences set via the GUI.
- **`SecurityGuard.cs`**: Enforces Read-Only mode, validates API keys, and rate-limits incoming traffic to protect the Editor from rogue AI loops.

## AI Autonomy & Context (Phase 1 & 2 additions)
The architecture supports complex AI autonomy by providing non-destructive context tools:
- **Auto-Fixers:** `DeveloperToolsHandler` provides ways for the AI to read compiler errors from the invisible `LogEntries` API and resolve missing references.
- **Visual Context:** `UIExtractorHandler` dumps Unity Canvas hierarchies into DOM-like JSON strings, allowing text-based AIs to "see" the UI layout without screenshots.

## Resources (Phase 3 additions)
Instead of the AI constantly polling Unity for state via Tool Calls, the Router exposes **MCP Resources** (`unity://*`). IDEs that support resources (like Cursor or Claude) can silently attach these resources to the prompt context, giving the AI immediate visibility into the Project Info or Scene Hierarchy the moment it starts thinking.
