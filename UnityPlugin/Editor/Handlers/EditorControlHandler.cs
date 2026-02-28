#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles editor control tools:
    /// unity_play_control, unity_refresh_assets, unity_get_compilation_result,
    /// unity_execute_menu_item, unity_get_editor_state
    /// </summary>
    public static class EditorControlHandler
    {
        [Serializable] private class PlayParams { public string state; }
        [Serializable] private class MenuParams { public string menuPath; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_play_control": return HandlePlayControl(paramsJson);
                    case "unity_refresh_assets": return HandleRefresh();
                    case "unity_get_compilation_result": return HandleCompilation();
                    case "unity_execute_menu_item": return HandleMenuItem(paramsJson);
                    case "unity_get_editor_state": return HandleEditorState();
                    default: return $"{{\"error\": \"Unknown editor tool: {tool}\"}}";
                }
            });
        }

        private static string HandlePlayControl(string paramsJson)
        {
            var p = JsonUtility.FromJson<PlayParams>(paramsJson);
            switch (p.state?.ToLower())
            {
                case "play":
                    EditorApplication.isPlaying = true;
                    return "{\"state\":\"playing\"}";
                case "pause":
                    EditorApplication.isPaused = !EditorApplication.isPaused;
                    return $"{{\"state\":\"paused\",\"isPaused\":{(EditorApplication.isPaused ? "true" : "false")}}}";
                case "stop":
                    EditorApplication.isPlaying = false;
                    return "{\"state\":\"stopped\"}";
                default:
                    return $"{{\"error\":\"Invalid state: {p.state}. Use 'play', 'pause', or 'stop'\"}}";
            }
        }

        private static string HandleRefresh()
        {
            AssetDatabase.Refresh();
            return "{\"refreshed\":true}";
        }

        private static string HandleCompilation()
        {
            var messages = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.All);
            
            // Check for compilation errors via the log entries
            var sb = new StringBuilder("{");
            sb.Append($"\"isCompiling\":{(EditorApplication.isCompiling ? "true" : "false")}");

            // Get compiler messages from all assemblies  
            var allMessages = new System.Collections.Generic.List<CompilerMessage>();
            var assemblyNames = CompilationPipeline.GetAssemblies();
            foreach (var asm in assemblyNames)
            {
                var msgs = CompilationPipeline.GetCompilerMessages(asm.name);
                if (msgs != null)
                    allMessages.AddRange(msgs);
            }

            var errors = allMessages.FindAll(m => m.type == CompilerMessageType.Error);
            var warnings = allMessages.FindAll(m => m.type == CompilerMessageType.Warning);

            sb.Append($",\"errorCount\":{errors.Count}");
            sb.Append($",\"warningCount\":{warnings.Count}");

            // Include error details (max 20)
            sb.Append(",\"errors\":[");
            for (int i = 0; i < Math.Min(errors.Count, 20); i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"message\":\"{EscapeString(errors[i].message)}\"" +
                          $",\"file\":\"{EscapeString(errors[i].file)}\"" +
                          $",\"line\":{errors[i].line}}}");
            }
            sb.Append("]");

            // Include warning details (max 10)
            sb.Append(",\"warnings\":[");
            for (int i = 0; i < Math.Min(warnings.Count, 10); i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"message\":\"{EscapeString(warnings[i].message)}\"" +
                          $",\"file\":\"{EscapeString(warnings[i].file)}\"" +
                          $",\"line\":{warnings[i].line}}}");
            }
            sb.Append("]");

            sb.Append("}");
            return sb.ToString();
        }

        private static string HandleMenuItem(string paramsJson)
        {
            var p = JsonUtility.FromJson<MenuParams>(paramsJson);
            if (string.IsNullOrEmpty(p.menuPath))
                return "{\"error\":\"menuPath is required\"}";

            var executed = EditorApplication.ExecuteMenuItem(p.menuPath);
            return $"{{\"executed\":{(executed ? "true" : "false")},\"menuPath\":\"{EscapeString(p.menuPath)}\"}}";
        }

        private static string HandleEditorState()
        {
            var scene = SceneManager.GetActiveScene();
            var selected = Selection.gameObjects;

            var sb = new StringBuilder("{");
            sb.Append($"\"isPlaying\":{(EditorApplication.isPlaying ? "true" : "false")}");
            sb.Append($",\"isPaused\":{(EditorApplication.isPaused ? "true" : "false")}");
            sb.Append($",\"isCompiling\":{(EditorApplication.isCompiling ? "true" : "false")}");
            sb.Append($",\"activeScene\":{{\"name\":\"{scene.name}\",\"path\":\"{scene.path}\",\"isDirty\":{(scene.isDirty ? "true" : "false")}}}");
            sb.Append($",\"platform\":\"{EditorUserBuildSettings.activeBuildTarget}\"");
            
            sb.Append(",\"selectedObjects\":[");
            for (int i = 0; i < selected.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"instanceId\":{selected[i].GetInstanceID()},\"name\":\"{EscapeString(selected[i].name)}\"}}");
            }
            sb.Append("]");

            sb.Append($",\"unityVersion\":\"{Application.unityVersion}\"");
            sb.Append("}");
            return sb.ToString();
        }

        private static string EscapeString(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r");
        }
    }
}
#endif
