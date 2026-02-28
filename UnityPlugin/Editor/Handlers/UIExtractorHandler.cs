#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Assuming TextMeshPro is used since it's standard in Unity 2021+

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Phase 2: UI Extraction Tools
    /// Converts a Unity Canvas or UI Toolkit tree into a semantic, HTML-like structure
    /// that LLMs can easily read, understand, and target for modification.
    /// </summary>
    public static class UIExtractorHandler
    {
        [Serializable] private class DumpParams { public int rootInstanceId; public int depth = 5; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_ui_dump_hierarchy": return HandleDumpHierarchy(paramsJson);
                    default: return ResponseHelper.Error($"Unknown UI tool: {tool}");
                }
            });
        }

        private static string HandleDumpHierarchy(string paramsJson)
        {
            var p = JsonUtility.FromJson<DumpParams>(paramsJson);
            GameObject root = null;

            if (p.rootInstanceId != 0)
            {
                root = EditorUtility.InstanceIDToObject(p.rootInstanceId) as GameObject;
                if (root == null)
                    return ResponseHelper.Error($"GameObject with instanceId {p.rootInstanceId} not found");
            }
            else
            {
                // Find all root Canvases if no ID provided
                var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
                if (canvases.Length == 0) return ResponseHelper.Error("No Canvas found in scene.");
                
                // Pick the first root canvas
                foreach (var c in canvases)
                {
                    if (c.transform.parent == null || c.transform.parent.GetComponentInParent<Canvas>() == null)
                    {
                        root = c.gameObject;
                        break;
                    }
                }
                if (root == null) root = canvases[0].gameObject;
            }

            var sb = new StringBuilder();
            DumpNode(root.transform, sb, 0, p.depth == 0 ? 5 : p.depth);

            return ResponseHelper.Ok(
                JsonHelper.Str("rootName", root.name),
                JsonHelper.Num("instanceId", root.GetInstanceID()),
                JsonHelper.Str("uiTree", sb.ToString()));
        }

        private static void DumpNode(Transform t, StringBuilder sb, int currentDepth, int maxDepth)
        {
            if (currentDepth > maxDepth || t == null || !t.gameObject.activeSelf) return;

            string indent = new string(' ', currentDepth * 2);
            string goName = t.name.Replace("\"", "'");
            int id = t.gameObject.GetInstanceID();

            // Determine semantic tag based on components
            string tag = "Container";
            string attributes = "";
            string innerText = "";

            if (t.GetComponent<Button>() != null) { tag = "Button"; }
            else if (t.GetComponent<Toggle>() != null) { tag = "Toggle"; }
            else if (t.GetComponent<InputField>() != null || t.GetComponent<TMP_InputField>() != null) { tag = "Input"; }
            else if (t.GetComponent<Slider>() != null) { tag = "Slider"; }
            else if (t.GetComponent<ScrollRect>() != null) { tag = "ScrollArea"; }
            else if (t.GetComponent<Image>() != null || t.GetComponent<RawImage>() != null) { tag = "Image"; }
            
            // Extract text content
            var textComp = t.GetComponent<Text>();
            if (textComp != null) { innerText = textComp.text; tag = tag == "Container" ? "Text" : tag; }
            
            var tmpComp = t.GetComponent<TextMeshProUGUI>();
            if (tmpComp != null) { innerText = tmpComp.text; tag = tag == "Container" ? "Text" : tag; }

            // Extract RectTransform data for layout reasoning
            var rect = t.GetComponent<RectTransform>();
            if (rect != null)
            {
                attributes = $" id={id} name=\"{goName}\" pos=\"{rect.anchoredPosition.x:F0},{rect.anchoredPosition.y:F0}\" size=\"{rect.sizeDelta.x:F0},{rect.sizeDelta.y:F0}\" anchor=\"{rect.anchorMin.x:F1},{rect.anchorMin.y:F1}|{rect.anchorMax.x:F1},{rect.anchorMax.y:F1}\"";
            }
            else
            {
                attributes = $" id={id} name=\"{goName}\"";
            }

            sb.Append($"{indent}<{tag}{attributes}>");

            if (!string.IsNullOrEmpty(innerText))
            {
                string cleanText = innerText.Replace("\n", "\\n").Replace("\r", "").Replace("\"", "'");
                if (cleanText.Length > 50) cleanText = cleanText.Substring(0, 47) + "...";
                sb.Append($"{cleanText}</{tag}>\n");
            }
            else if (t.childCount == 0)
            {
                sb.Append($"</{tag}>\n");
            }
            else
            {
                sb.Append("\n");
                for (int i = 0; i < t.childCount; i++)
                {
                    DumpNode(t.GetChild(i), sb, currentDepth + 1, maxDepth);
                }
                sb.Append($"{indent}</{tag}>\n");
            }
        }
    }
}
#endif
