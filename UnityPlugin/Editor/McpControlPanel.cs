#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor
{
    /// <summary>
    /// MCP Control Panel — Unity EditorWindow for managing the MCP server,
    /// tool categories, IDE integration, and security settings.
    /// Window > Antigravity > MCP Control Panel
    /// </summary>
    public class McpControlPanel : EditorWindow
    {
        private int _selectedTab = 0;
        private readonly string[] _tabNames = { "Dashboard", "Tools", "IDE Setup", "Security" };
        private Vector2 _toolsScroll;
        private Vector2 _ideScroll;
        private string _manualConfigPreview = "";
        private int _newPort = 8090;

        // ─── Styles ──────────────────────────────
        private GUIStyle _headerStyle;
        private GUIStyle _statusStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInitialized;

        [MenuItem("Window/Antigravity/MCP Control Panel")]
        public static void ShowWindow()
        {
            var window = GetWindow<McpControlPanel>("MCP Control Panel");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }

        private void OnEnable()
        {
            McpEditorServer.OnStateChanged += Repaint;
            _newPort = McpEditorServer.Port;
        }

        private void OnDisable()
        {
            McpEditorServer.OnStateChanged -= Repaint;
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;
            _headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            _statusStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, richText = true };
            _boxStyle = new GUIStyle("box") { padding = new RectOffset(10, 10, 10, 10) };
            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            // Title bar
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("⚡ MCP Control Panel", _headerStyle);
            GUILayout.FlexibleSpace();

            // Quick status indicator
            var statusColor = McpEditorServer.IsRunning ? "<color=#4CAF50>● RUNNING</color>" : "<color=#F44336>● STOPPED</color>";
            GUILayout.Label(statusColor, _statusStyle);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabNames);
            EditorGUILayout.Space(10);

            switch (_selectedTab)
            {
                case 0: DrawDashboard(); break;
                case 1: DrawTools(); break;
                case 2: DrawIDESetup(); break;
                case 3: DrawSecurity(); break;
            }
        }

        // ═══════════════════════════════════════════
        //  TAB 0: DASHBOARD
        // ═══════════════════════════════════════════
        private void DrawDashboard()
        {
            EditorGUILayout.BeginVertical(_boxStyle);

            // Server controls
            EditorGUILayout.LabelField("Server Status", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Status:", GUILayout.Width(120));
            EditorGUILayout.LabelField(McpEditorServer.IsRunning ? "Running" : "Stopped");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Port:", GUILayout.Width(120));
            EditorGUILayout.LabelField(McpEditorServer.Port.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Connected Clients:", GUILayout.Width(120));
            EditorGUILayout.LabelField(McpEditorServer.ConnectedClientCount.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Messages Processed:", GUILayout.Width(120));
            EditorGUILayout.LabelField(McpEditorServer.TotalMessagesProcessed.ToString());
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Last Message:", GUILayout.Width(120));
            EditorGUILayout.LabelField(McpEditorServer.LastMessageTime?.ToString("HH:mm:ss") ?? "—");
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(McpEditorServer.LastError))
            {
                EditorGUILayout.HelpBox($"Last Error: {McpEditorServer.LastError}", MessageType.Error);
            }

            EditorGUILayout.Space(10);

            // Controls
            EditorGUILayout.BeginHorizontal();
            if (McpEditorServer.IsRunning)
            {
                if (GUILayout.Button("⏹ Stop Server", GUILayout.Height(30)))
                    McpEditorServer.Stop();
                if (GUILayout.Button("🔄 Restart", GUILayout.Height(30)))
                    McpEditorServer.Restart();
            }
            else
            {
                if (GUILayout.Button("▶ Start Server", GUILayout.Height(30)))
                    McpEditorServer.Start();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Port config
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Port:", GUILayout.Width(40));
            _newPort = EditorGUILayout.IntField(_newPort, GUILayout.Width(80));
            if (GUILayout.Button("Apply", GUILayout.Width(60)))
            {
                McpEditorServer.SetPort(_newPort);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Quick stats
            EditorGUILayout.LabelField("Tool Statistics", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Enabled Tools:", GUILayout.Width(120));
            EditorGUILayout.LabelField($"{McpToolRegistry.EnabledToolCount} / {McpToolRegistry.TotalToolCount}");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ═══════════════════════════════════════════
        //  TAB 1: TOOL MANAGEMENT
        // ═══════════════════════════════════════════
        private void DrawTools()
        {
            // Preset buttons
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Presets:", GUILayout.Width(60));
            foreach (var preset in McpToolRegistry.Presets)
            {
                if (GUILayout.Button(preset.Key, GUILayout.MaxWidth(100)))
                {
                    McpToolRegistry.ApplyPreset(preset.Key);
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Enable All", GUILayout.Height(25)))
                McpToolRegistry.EnableAll();
            if (GUILayout.Button("Disable All", GUILayout.Height(25)))
                McpToolRegistry.DisableAll();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Enabled: {McpToolRegistry.EnabledToolCount} / {McpToolRegistry.TotalToolCount}");
            EditorGUILayout.Space(5);

            // Category toggles
            _toolsScroll = EditorGUILayout.BeginScrollView(_toolsScroll);

            foreach (var cat in McpToolRegistry.Categories)
            {
                EditorGUILayout.BeginHorizontal("box");

                var enabled = McpToolRegistry.IsCategoryEnabled(cat.Id);
                var newEnabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(20));
                if (newEnabled != enabled)
                    McpToolRegistry.SetCategoryEnabled(cat.Id, newEnabled);

                var style = new GUIStyle(EditorStyles.label);
                if (!enabled) style.normal.textColor = Color.gray;

                EditorGUILayout.LabelField(cat.DisplayName, style);
                EditorGUILayout.LabelField($"{cat.Tools.Length} tools", style, GUILayout.Width(60));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        // ═══════════════════════════════════════════
        //  TAB 2: IDE SETUP
        // ═══════════════════════════════════════════
        private void DrawIDESetup()
        {
            _ideScroll = EditorGUILayout.BeginScrollView(_ideScroll);

            EditorGUILayout.LabelField("Auto-Configure IDE", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Click a button to automatically generate MCP config for your IDE. The config will point to this project's MCP Router.", MessageType.Info);
            EditorGUILayout.Space(5);

            DrawIDEButton("Antigravity", McpConfigExporter.IsAntigravityInstalled(), McpConfigExporter.ExportForAntigravity);
            DrawIDEButton("VS Code (Copilot)", McpConfigExporter.IsVSCodeInstalled(), McpConfigExporter.ExportForVSCode);
            DrawIDEButton("Cursor", McpConfigExporter.IsCursorInstalled(), McpConfigExporter.ExportForCursor);
            DrawIDEButton("Claude Code", McpConfigExporter.IsClaudeCodeInstalled(), McpConfigExporter.ExportForClaudeCode);
            DrawIDEButton("Gemini CLI", McpConfigExporter.IsGeminiCLIInstalled(), McpConfigExporter.ExportForGeminiCLI);
            DrawIDEButton("Windsurf", McpConfigExporter.IsWindsurfInstalled(), McpConfigExporter.ExportForWindsurf);
            DrawIDEButton("Codex CLI", McpConfigExporter.IsCodexInstalled(), McpConfigExporter.ExportForCodex);

            EditorGUILayout.Space(15);

            // Manual config preview
            EditorGUILayout.LabelField("Manual Configuration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Copy this JSON into your IDE's MCP config file if auto-config doesn't work.", MessageType.None);

            if (GUILayout.Button("Generate Config Preview"))
                _manualConfigPreview = McpConfigExporter.GetManualConfigExample();

            if (!string.IsNullOrEmpty(_manualConfigPreview))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.TextArea(_manualConfigPreview, GUILayout.MinHeight(120));

                if (GUILayout.Button("📋 Copy to Clipboard"))
                {
                    EditorGUIUtility.systemCopyBuffer = _manualConfigPreview;
                    Debug.Log("[MCP] Config copied to clipboard.");
                }
            }

            EditorGUILayout.Space(15);

            // Build router
            EditorGUILayout.LabelField("Router Build", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Build a standalone Router .exe for faster startup. IDE configs will use the exe instead of 'dotnet run'.", MessageType.None);

            if (GUILayout.Button("🔨 Build Router (Release)", GUILayout.Height(30)))
            {
                BuildRouter();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawIDEButton(string ideName, bool detected, Func<string> exporter)
        {
            EditorGUILayout.BeginHorizontal();
            var icon = detected ? "✅" : "⬜";
            EditorGUILayout.LabelField($"{icon} {ideName}", GUILayout.Width(180));

            if (detected)
            {
                EditorGUILayout.LabelField("Detected", GUILayout.Width(70));
            }
            else
            {
                EditorGUILayout.LabelField("Not found", GUILayout.Width(70));
            }

            if (GUILayout.Button("Export Config", GUILayout.Width(100)))
            {
                var path = exporter();
                EditorUtility.DisplayDialog("MCP Config Exported",
                    $"Config written to:\n{path}", "OK");
            }

            EditorGUILayout.EndHorizontal();
        }

        private void BuildRouter()
        {
            var routerDir = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../../UnityMcpRouter"));

            EditorUtility.DisplayProgressBar("Building Router", "Running dotnet publish...", 0.5f);
            try
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.FileName = "dotnet";
                process.StartInfo.Arguments = "publish -c Release -r win-x64 --self-contained false";
                process.StartInfo.WorkingDirectory = routerDir;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.Start();
                process.WaitForExit(60000);

                if (process.ExitCode == 0)
                {
                    EditorUtility.DisplayDialog("Build Success",
                        "Router built successfully! IDE configs will now use the exe.", "OK");
                }
                else
                {
                    var error = process.StandardError.ReadToEnd();
                    EditorUtility.DisplayDialog("Build Failed", error, "OK");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Build Error", ex.Message, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ═══════════════════════════════════════════
        //  TAB 3: SECURITY
        // ═══════════════════════════════════════════
        private void DrawSecurity()
        {
            EditorGUILayout.BeginVertical(_boxStyle);

            EditorGUILayout.LabelField("API Key Authentication", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Set an API key to require authentication from MCP Router connections. Leave empty to disable auth.", MessageType.Info);

            var currentKey = EditorPrefs.GetString("MCP_API_KEY", "");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("API Key:", GUILayout.Width(60));
            var newKey = EditorGUILayout.PasswordField(currentKey);
            if (newKey != currentKey)
                EditorPrefs.SetString("MCP_API_KEY", newKey);

            if (GUILayout.Button("Generate", GUILayout.Width(70)))
            {
                var key = Guid.NewGuid().ToString("N").Substring(0, 24);
                EditorPrefs.SetString("MCP_API_KEY", key);
                Debug.Log($"[MCP] New API key generated: {key}");
            }
            if (GUILayout.Button("Clear", GUILayout.Width(50)))
            {
                EditorPrefs.SetString("MCP_API_KEY", "");
                Debug.Log("[MCP] API key cleared (auth disabled).");
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);

            // Read-only mode
            EditorGUILayout.LabelField("Access Control", EditorStyles.boldLabel);
            var readOnly = EditorPrefs.GetBool("MCP_READ_ONLY", false);
            var newReadOnly = EditorGUILayout.Toggle("Read-Only Mode", readOnly);
            if (newReadOnly != readOnly)
                EditorPrefs.SetBool("MCP_READ_ONLY", newReadOnly);

            if (readOnly)
            {
                EditorGUILayout.HelpBox("Read-Only Mode: Only inspection/query tools are active. Create/modify/delete operations will be rejected.", MessageType.Warning);
            }

            EditorGUILayout.Space(15);

            // Rate limiting
            EditorGUILayout.LabelField("Rate Limiting", EditorStyles.boldLabel);
            var rateLimit = EditorPrefs.GetInt("MCP_RATE_LIMIT", 120);
            var newRate = EditorGUILayout.IntSlider("Max Requests/sec", rateLimit, 10, 500);
            if (newRate != rateLimit)
                EditorPrefs.SetInt("MCP_RATE_LIMIT", newRate);

            EditorGUILayout.Space(15);

            // HTTPS
            EditorGUILayout.LabelField("Transport Security", EditorStyles.boldLabel);
            var useHttps = EditorPrefs.GetBool("MCP_USE_HTTPS", false);
            var newHttps = EditorGUILayout.Toggle("Enable HTTPS/WSS", useHttps);
            if (newHttps != useHttps)
            {
                EditorPrefs.SetBool("MCP_USE_HTTPS", newHttps);
                if (newHttps)
                    EditorGUILayout.HelpBox("HTTPS requires a localhost SSL certificate bound to the port. On Windows: use 'netsh http add sslcert' or 'dotnet dev-certs'. Falls back to HTTP if cert is missing.", MessageType.Warning);
            }

            EditorGUILayout.Space(15);

            // Logging
            EditorGUILayout.LabelField("Command Logging", EditorStyles.boldLabel);
            var logging = EditorPrefs.GetBool("MCP_LOGGING", false);
            var newLogging = EditorGUILayout.Toggle("Enable Audit Log", logging);
            if (newLogging != logging)
                EditorPrefs.SetBool("MCP_LOGGING", newLogging);

            if (logging)
            {
                var logPath = System.IO.Path.Combine(Application.dataPath, "../Logs/mcp_audit.log");
                EditorGUILayout.LabelField($"Log: {logPath}", EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }
    }
}
#endif
