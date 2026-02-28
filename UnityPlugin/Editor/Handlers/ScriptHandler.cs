#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles C# script creation and management:
    /// unity_create_script, unity_read_script, unity_edit_script
    /// </summary>
    public static class ScriptHandler
    {
        [Serializable] private class CreateParams { public string scriptName; public string scriptType; public string namespaceName; public string savePath; public string content; }
        [Serializable] private class ReadParams { public string scriptPath; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_create_script": return HandleCreate(paramsJson);
                    case "unity_read_script": return HandleRead(paramsJson);
                    case "unity_edit_script": return HandleEdit(paramsJson);
                    default: return $"{{\"error\":\"Unknown script tool: {tool}\"}}";
                }
            });
        }

        private static string HandleCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateParams>(paramsJson);
            if (string.IsNullOrEmpty(p.scriptName)) return "{\"error\":\"scriptName is required\"}";

            var path = p.savePath ?? $"Assets/Scripts/{p.scriptName}.cs";
            SecurityGuard.ValidatePath(path);

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string content;
            if (!string.IsNullOrEmpty(p.content))
            {
                content = p.content;
            }
            else
            {
                var ns = p.namespaceName ?? "Game";
                var type = (p.scriptType ?? "MonoBehaviour").ToLower();
                content = type switch
                {
                    "monobehaviour" => GenerateMonoBehaviour(p.scriptName, ns),
                    "scriptableobject" => GenerateScriptableObject(p.scriptName, ns),
                    "editor" => GenerateEditorScript(p.scriptName, ns),
                    "interface" => GenerateInterface(p.scriptName, ns),
                    "static" => GenerateStaticClass(p.scriptName, ns),
                    "enum" => GenerateEnum(p.scriptName, ns),
                    _ => GenerateMonoBehaviour(p.scriptName, ns),
                };
            }

            File.WriteAllText(path, content);
            AssetDatabase.Refresh();

            return $"{{\"created\":true,\"path\":\"{path}\",\"lines\":{content.Split('\n').Length}}}";
        }

        private static string HandleRead(string paramsJson)
        {
            var p = JsonUtility.FromJson<ReadParams>(paramsJson);
            SecurityGuard.ValidatePath(p.scriptPath);

            if (!File.Exists(p.scriptPath)) return $"{{\"error\":\"File not found: {p.scriptPath}\"}}";

            var content = File.ReadAllText(p.scriptPath);
            var escaped = content.Replace("\\", "\\\\").Replace("\"", "\\\"")
                                 .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
            return $"{{\"path\":\"{p.scriptPath}\",\"lines\":{content.Split('\n').Length},\"content\":\"{escaped}\"}}";
        }

        private static string HandleEdit(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateParams>(paramsJson);
            var path = p.savePath ?? p.scriptName;
            SecurityGuard.ValidatePath(path);

            if (!File.Exists(path)) return $"{{\"error\":\"File not found: {path}\"}}";
            if (string.IsNullOrEmpty(p.content)) return "{\"error\":\"content is required\"}";

            File.WriteAllText(path, p.content);
            AssetDatabase.Refresh();

            return $"{{\"edited\":true,\"path\":\"{path}\"}}";
        }

        // ─── Template Generators ─────────────────

        private static string GenerateMonoBehaviour(string name, string ns)
        {
            return $@"using UnityEngine;

namespace {ns}
{{
    public class {name} : MonoBehaviour
    {{
        void Start()
        {{
            
        }}

        void Update()
        {{
            
        }}
    }}
}}
";
        }

        private static string GenerateScriptableObject(string name, string ns)
        {
            return $@"using UnityEngine;

namespace {ns}
{{
    [CreateAssetMenu(fileName = ""{name}"", menuName = ""{ns}/{name}"")]
    public class {name} : ScriptableObject
    {{
        
    }}
}}
";
        }

        private static string GenerateEditorScript(string name, string ns)
        {
            return $@"#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace {ns}
{{
    public class {name} : EditorWindow
    {{
        [MenuItem(""Window/{ns}/{name}"")]
        public static void ShowWindow()
        {{
            GetWindow<{name}>(""{name}"");
        }}

        private void OnGUI()
        {{
            
        }}
    }}
}}
#endif
";
        }

        private static string GenerateInterface(string name, string ns)
        {
            return $@"namespace {ns}
{{
    public interface {name}
    {{
        
    }}
}}
";
        }

        private static string GenerateStaticClass(string name, string ns)
        {
            return $@"namespace {ns}
{{
    public static class {name}
    {{
        
    }}
}}
";
        }

        private static string GenerateEnum(string name, string ns)
        {
            return $@"namespace {ns}
{{
    public enum {name}
    {{
        None,
    }}
}}
";
        }
    }
}
#endif
