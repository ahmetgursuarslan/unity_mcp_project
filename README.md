# Unity MCP — Model Context Protocol for Unity Editor

AI-powered Unity Editor control via the Model Context Protocol (MCP). Connect any MCP-compatible AI IDE (Antigravity, Cursor, Windsurf, Claude Code, Gemini CLI, Codex Terminal, VS Code Copilot) to Unity for full editor automation.

This project bridges the gap between Large Language Models and the Unity Editor, allowing AIs to seamlessly inspect scenes, modify game objects, analyze UI hierarchies, compile code, and read live resources like the Unity Console.

![MCP Unity Architecture](https://raw.githubusercontent.com/modelcontextprotocol/servers/main/docs/mcp-logo.png)

## Documentation

Comprehensive documentation has been split into specialized guides to help you get started quickly:

1. **[Installation & Quick Start Guide](INSTALLATION.md)**
   - Prerequisites
   - Installing the Unity Plugin
   - Building and launching the .NET Router
   - Connecting your IDE (Antigravity, Cursor, etc.)

2. **[Architecture Overview](docs/architecture.md)**
   - How the .NET Router and Unity WebSocket Bridge operate.
   - Understanding the `std_io` / JSON-RPC translation layer.

3. **[Tools & Resources Reference](docs/tools_and_resources.md)**
   - Breakdown of the 145+ available tools (Auto-fixers, UI Dumpers, Scene manipulation).
   - Live MCP Resources (`unity://console/errors`, `unity://scene/hierarchy`, `unity://project/info`).

4. **[Configuration & IDE Setup](docs/configuration_and_ide.md)**
   - Using the **MCP Control Panel** inside Unity.
   - Pre-built presets (2D, 3D, Multiplayer).
   - Security features (API Keys, Rate Limiting, Read-Only mode).

---

## Features at a Glance

- **~145 tools** across 31 categories covering all Unity subsystems.
- **MCP Resources** providing live context feeds to the AI without active polling.
- **Auto-Fixers** built-in for the AI to resolve compilation errors and missing references.
- **MCP Control Panel** — Unity EditorWindow for server control, tool management, and IDE config export.
- **Multi-IDE support** — one-click config generation for 7 major AI IDEs (including Codex Terminal).
- **Enterprise Security** — API key auth, rate limiting, read-only mode, audit logging.
- **Multi-client** — multiple IDEs can connect simultaneously to the single Unity instance.

## Requirements

- Unity 6 (6000.0+)
- .NET 8 SDK (for the Router)
- An MCP-compatible AI Assistant

## License

Proprietary License - All Rights Reserved. See [LICENSE](LICENSE) for details.
Copyright (c) 2026 Ahmet Gürsu Arslan (https://github.com/ahmetgursuarslan)
