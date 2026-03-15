#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor
{
    /// <summary>
    /// Generates MCP config files for various IDEs.
    /// Detects installed IDEs and creates appropriate configuration.
    /// </summary>
    public static class McpConfigExporter
    {
        private static string RouterProjectPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "../../UnityMcpRouter"))
                .Replace("\\", "/");

        private static string RouterExePath =>
            Path.Combine(RouterProjectPath, "bin/Release/net8.0/UnityMcpRouter.exe")
                .Replace("\\", "/");

        private static int Port => McpEditorServer.Port;

        // ─── Config template ─────────────────────
        private static string BuildConfig(bool useExe = false)
        {
            var apiKey = EditorPrefs.GetString("MCP_API_KEY", "");
            var apiKeyLine = string.IsNullOrEmpty(apiKey) ? "" : $",\n        \"MCP_API_KEY\": \"{apiKey}\"";

            if (useExe && File.Exists(RouterExePath.Replace("/", "\\")))
            {
                return $@"{{
  ""mcpServers"": {{
    ""nefertiti-controller"": {{
      ""command"": ""{RouterExePath}"",
      ""args"": [],
      ""env"": {{
        ""UNITY_WS_PORT"": ""{Port}"",
        ""UNITY_REQUEST_TIMEOUT"": ""30"",
        ""LOG_LEVEL"": ""error""{apiKeyLine}
      }}
    }}
  }}
}}";
            }

            return $@"{{
  ""mcpServers"": {{
    ""nefertiti-controller"": {{
      ""command"": ""dotnet"",
      ""args"": [""run"", ""--project"", ""{RouterProjectPath}""],
      ""env"": {{
        ""UNITY_WS_PORT"": ""{Port}"",
        ""UNITY_REQUEST_TIMEOUT"": ""30"",
        ""LOG_LEVEL"": ""error""{apiKeyLine}
      }}
    }}
  }}
}}";
        }

        // ─── IDE Exporters ───────────────────────

        public static string ExportForAntigravity()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var dir = Path.Combine(projectRoot, ".antigravity");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, BuildConfig(true));
            Debug.Log($"[Nefertiti] Antigravity config exported to: {path}");
            return path;
        }

        public static string ExportForVSCode()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var dir = Path.Combine(projectRoot, ".vscode");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "mcp.json");
            File.WriteAllText(path, BuildConfig());
            Debug.Log($"[MCP] VS Code config exported to: {path}");
            return path;
        }

        public static string ExportForCursor()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = Path.Combine(home, ".cursor");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "mcp.json");
            File.WriteAllText(path, BuildConfig());
            Debug.Log($"[MCP] Cursor config exported to: {path}");
            return path;
        }

        public static string ExportForClaudeCode()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = Path.Combine(home, ".claude");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "mcp_settings.json");
            File.WriteAllText(path, BuildConfig());
            Debug.Log($"[MCP] Claude Code config exported to: {path}");
            return path;
        }

        public static string ExportForGeminiCLI()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = Path.Combine(home, ".gemini");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "settings.json");

            // Gemini CLI may already have a config, merge mcpServers into it
            string existingContent = File.Exists(path) ? File.ReadAllText(path) : "{}";
            if (existingContent.Contains("\"mcpServers\""))
            {
                Debug.LogWarning("[MCP] Gemini CLI settings.json already contains mcpServers. Please merge manually.");
                var fallback = Path.Combine(dir, "unity_mcp_config.json");
                File.WriteAllText(fallback, BuildConfig());
                return fallback;
            }
            File.WriteAllText(path, BuildConfig());
            Debug.Log($"[MCP] Gemini CLI config exported to: {path}");
            return path;
        }

        public static string ExportForWindsurf()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = Path.Combine(home, ".codeium", "windsurf");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "mcp_config.json");
            File.WriteAllText(path, BuildConfig());
            Debug.Log($"[MCP] Windsurf config exported to: {path}");
            return path;
        }

        public static string ExportForCodex()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var dir = Path.Combine(home, ".codex");
            Directory.CreateDirectory(dir);
            // Using mcp.json as the standard configuration file for Codex CLI
            var path = Path.Combine(dir, "mcp.json");
            File.WriteAllText(path, BuildConfig());
            Debug.Log($"[MCP] Codex config exported to: {path}");
            return path;
        }

        // ─── IDE Detection ───────────────────────

        public static bool IsAntigravityInstalled()
        {
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Directory.Exists(Path.Combine(localApp, "Antigravity")) ||
                   Directory.Exists(Path.Combine(localApp, "Programs", "antigravity"));
        }

        public static bool IsVSCodeInstalled()
        {
            var localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Directory.Exists(Path.Combine(localApp, "Programs", "Microsoft VS Code")) ||
                   File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "code.exe"));
        }

        public static bool IsCursorInstalled()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Directory.Exists(Path.Combine(home, ".cursor"));
        }

        public static bool IsClaudeCodeInstalled()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Directory.Exists(Path.Combine(home, ".claude"));
        }

        public static bool IsGeminiCLIInstalled()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Directory.Exists(Path.Combine(home, ".gemini"));
        }

        public static bool IsWindsurfInstalled()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Directory.Exists(Path.Combine(home, ".codeium"));
        }

        public static bool IsCodexInstalled()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Directory.Exists(Path.Combine(home, ".codex"));
        }

        // ─── Manual Config Example ──────────────

        public static string GetManualConfigExample()
        {
            return BuildConfig();
        }
    }
}
#endif
