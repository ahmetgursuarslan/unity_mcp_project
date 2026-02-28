#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles UI Toolkit tools:
    /// unity_ui_query, unity_ui_create_element, unity_ui_set_style,
    /// unity_ui_bind_data, unity_ui_generate_uxml, unity_ui_generate_uss,
    /// unity_ui_register_callback
    /// </summary>
    public static class UIToolkitHandler
    {
        [Serializable] private class QueryParams { public string selector; public string windowType; }
        [Serializable] private class CreateParams { public string elementType; public string name; public string text; public string parentSelector; }
        [Serializable] private class StyleParams { public string selector; }
        [Serializable] private class UxmlParams { public string savePath; public string content; }
        [Serializable] private class UssParams { public string savePath; public string content; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_ui_query": return HandleQuery(paramsJson);
                    case "unity_ui_create_element": return HandleCreate(paramsJson);
                    case "unity_ui_set_style": return HandleSetStyle(paramsJson);
                    case "unity_ui_bind_data": return "{\"info\":\"Data binding requires runtime context. Define bindings in UXML.\"}";
                    case "unity_ui_generate_uxml": return HandleGenerateUxml(paramsJson);
                    case "unity_ui_generate_uss": return HandleGenerateUss(paramsJson);
                    case "unity_ui_register_callback": return "{\"info\":\"Callbacks require C# code. Use script generation tools.\"}";
                    default: return $"{{\"error\":\"Unknown UI tool: {tool}\"}}";
                }
            });
        }

        private static string HandleQuery(string paramsJson)
        {
            var p = JsonUtility.FromJson<QueryParams>(paramsJson);
            // Query from focused editor window
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var sb = new StringBuilder("[");
            int count = 0;
            foreach (var w in windows)
            {
                var root = w.rootVisualElement;
                if (root == null) continue;
                var elements = root.Query(p.selector).ToList();
                foreach (var el in elements)
                {
                    if (count > 0) sb.Append(",");
                    sb.Append($"{{\"name\":\"{el.name}\",\"type\":\"{el.GetType().Name}\",\"window\":\"{w.titleContent.text}\"}}");
                    count++;
                    if (count >= 50) break;
                }
                if (count >= 50) break;
            }
            sb.Append("]");
            return $"{{\"count\":{count},\"elements\":{sb}}}";
        }

        private static string HandleCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateParams>(paramsJson);
            return $"{{\"info\":\"UI elements must be created via UXML or C# scripts. Use unity_ui_generate_uxml to create a UXML file with the desired elements.\"" +
                   $",\"suggestedUxml\":\"<ui:UXML><ui:{p.elementType ?? "Button"} name=\\\"{p.name ?? "element"}\\\" text=\\\"{p.text ?? ""}\\\" /></ui:UXML>\"}}";
        }

        private static string HandleSetStyle(string paramsJson)
        {
            return "{\"info\":\"Styles should be defined in USS files. Use unity_ui_generate_uss to create stylesheets.\"}";
        }

        private static string HandleGenerateUxml(string paramsJson)
        {
            var p = JsonUtility.FromJson<UxmlParams>(paramsJson);
            if (string.IsNullOrEmpty(p.savePath)) return "{\"error\":\"savePath is required\"}";
            SecurityGuard.ValidatePath(p.savePath);

            var dir = System.IO.Path.GetDirectoryName(p.savePath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var content = p.content ?? "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">\n</ui:UXML>";
            System.IO.File.WriteAllText(p.savePath, content);
            AssetDatabase.ImportAsset(p.savePath);
            return $"{{\"created\":true,\"path\":\"{p.savePath}\"}}";
        }

        private static string HandleGenerateUss(string paramsJson)
        {
            var p = JsonUtility.FromJson<UssParams>(paramsJson);
            if (string.IsNullOrEmpty(p.savePath)) return "{\"error\":\"savePath is required\"}";
            SecurityGuard.ValidatePath(p.savePath);

            var dir = System.IO.Path.GetDirectoryName(p.savePath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllText(p.savePath, p.content ?? "/* USS Stylesheet */\n");
            AssetDatabase.ImportAsset(p.savePath);
            return $"{{\"created\":true,\"path\":\"{p.savePath}\"}}";
        }
    }
}
#endif
