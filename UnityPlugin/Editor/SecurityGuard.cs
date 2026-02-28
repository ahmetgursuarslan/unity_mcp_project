#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor
{
    /// <summary>
    /// Security guard for MCP operations.
    /// Features: path validation, sliding-window rate limiting (thread-safe), 
    /// read-only mode, and audit logging.
    /// </summary>
    public static class SecurityGuard
    {
        // ─── Sliding Window Rate Limiter (Thread-Safe) ───
        private static long _windowStartTicks;
        private static int _windowCount;
        private static readonly object _rateLock = new object();
        private const long TicksPerSecond = TimeSpan.TicksPerSecond;

        // ─── Read-only tool whitelist (comprehensive) ────
        private static readonly string[] ReadOnlyTools = {
            // Scene & hierarchy inspection
            "unity_scene_list", "unity_object_find", "unity_object_inspect",
            "unity_object_find_by_path", "unity_hierarchy_list",
            // Editor state
            "unity_get_editor_state", "unity_get_compilation_result",
            // Material & shader inspection
            "unity_material_get_properties", "unity_shader_get_properties",
            // Asset inspection
            "unity_asset_find", "unity_asset_get_dependencies",
            // Console & dev tools
            "unity_console_get_logs", "unity_dev_get_compile_errors",
            "unity_dev_find_missing_references", "unity_dev_find_asset_dependencies",
            // Package inspection
            "unity_package_list", "unity_package_search",
            // Script reading
            "unity_read_script",
            // Build info
            "unity_build_scene_list", "unity_build_settings",
            // Performance inspection
            "unity_profiler_capture", "unity_memory_snapshot",
            // 2D inspection
            "unity_sorting_layer_manage",
            // UI inspection
            "unity_ui_query", "unity_ui_dump_hierarchy",
            // Rendering inspection
            "unity_graphics_api", "unity_render_settings",
            // Import settings (read-only get)
            "unity_texture_import_settings", "unity_model_import_settings",
            "unity_audio_import_settings",
            // Physics inspection
            "unity_physics_raycast", "unity_physics_overlap",
            // NavMesh inspection
            "unity_navmesh_find_path", "unity_navmesh_set_area"
        };

        /// <summary>
        /// Validates that a file path is within allowed project directories.
        /// Throws UnauthorizedAccessException if path is outside bounds.
        /// </summary>
        public static void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            // Normalize the path
            var fullPath = Path.GetFullPath(path).Replace("\\", "/");
            var projectPath = Path.GetFullPath(Application.dataPath + "/..").Replace("\\", "/");

            // Allow Assets/ and Packages/ within the project
            var assetsPath = Path.GetFullPath(Application.dataPath).Replace("\\", "/");
            var packagesPath = Path.Combine(projectPath, "Packages").Replace("\\", "/");

            bool isAllowed = fullPath.StartsWith(assetsPath, StringComparison.OrdinalIgnoreCase) ||
                             fullPath.StartsWith(packagesPath, StringComparison.OrdinalIgnoreCase);

            // Also allow relative paths like "Assets/..." 
            if (!isAllowed && (path.StartsWith("Assets/") || path.StartsWith("Assets\\") ||
                               path.StartsWith("Packages/") || path.StartsWith("Packages\\")))
            {
                isAllowed = true;
            }

            // Check user-defined additional paths
            var extraPaths = EditorPrefs.GetString("MCP_ALLOWED_PATHS", "");
            if (!string.IsNullOrEmpty(extraPaths))
            {
                foreach (var allowed in extraPaths.Split(';'))
                {
                    if (!string.IsNullOrEmpty(allowed) && fullPath.StartsWith(allowed.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        isAllowed = true;
                        break;
                    }
                }
            }

            if (!isAllowed)
            {
                throw new UnauthorizedAccessException(
                    $"Path '{path}' is outside the allowed project directories (Assets/, Packages/). " +
                    "Add allowed paths in MCP Control Panel > Security.");
            }

            // Check for path traversal attempts
            if (path.Contains("..") || path.Contains("~"))
            {
                throw new UnauthorizedAccessException(
                    $"Path traversal detected in '{path}'. Use absolute paths within the project.");
            }
        }

        /// <summary>
        /// Thread-safe sliding window rate limiter.
        /// Returns true if the request is allowed, false if rate limit exceeded.
        /// Uses Stopwatch ticks for high-resolution timing.
        /// </summary>
        public static bool CheckRateLimit()
        {
            var limit = EditorPrefs.GetInt("MCP_RATE_LIMIT", 120);
            var now = Stopwatch.GetTimestamp();

            lock (_rateLock)
            {
                // If more than 1 second has elapsed, reset the window
                if (now - _windowStartTicks > Stopwatch.Frequency)
                {
                    _windowStartTicks = now;
                    _windowCount = 1;
                    return true;
                }

                _windowCount++;
                return _windowCount <= limit;
            }
        }

        /// <summary>
        /// Check if a tool is allowed in read-only mode.
        /// </summary>
        public static bool IsAllowedInReadOnlyMode(string toolName)
        {
            if (!EditorPrefs.GetBool("MCP_READ_ONLY", false)) return true;
            return Array.IndexOf(ReadOnlyTools, toolName) >= 0;
        }

        /// <summary>
        /// Log a command for audit trail.
        /// </summary>
        public static void LogCommand(string toolName, string result)
        {
            if (!EditorPrefs.GetBool("MCP_LOGGING", false)) return;

            try
            {
                var logDir = Path.Combine(Application.dataPath, "../Logs");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

                var logPath = Path.Combine(logDir, "mcp_audit.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var isError = result.Contains("\"error\"");
                var line = $"[{timestamp}] {(isError ? "ERR" : "OK ")} {toolName}\n";

                File.AppendAllText(logPath, line);
            }
            catch { /* Don't let logging break operations */ }
        }
    }
}
#endif
