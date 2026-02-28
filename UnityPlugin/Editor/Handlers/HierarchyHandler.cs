#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles hierarchy tools:
    /// unity_hierarchy_list, unity_hierarchy_reparent
    /// </summary>
    public static class HierarchyHandler
    {
        [Serializable] private class ReparentParams { public int childId; public int parentId; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_hierarchy_list": return HandleList();
                    case "unity_hierarchy_reparent": return HandleReparent(paramsJson);
                    default: return $"{{\"error\": \"Unknown hierarchy tool: {tool}\"}}";
                }
            });
        }

        private static string HandleList()
        {
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            
            var sb = new StringBuilder();
            sb.Append($"{{\"scene\":\"{scene.name}\",\"rootObjects\":[");
            
            for (int i = 0; i < roots.Length; i++)
            {
                if (i > 0) sb.Append(",");
                SerializeHierarchyNode(roots[i].transform, sb, 0);
            }
            
            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// Recursively serializes a Transform hierarchy into JSON.
        /// Limits depth to 10 levels to prevent infinite recursion.
        /// </summary>
        private static void SerializeHierarchyNode(Transform t, StringBuilder sb, int depth)
        {
            var go = t.gameObject;
            sb.Append($"{{\"instanceId\":{go.GetInstanceID()}" +
                      $",\"name\":\"{EscapeString(go.name)}\"" +
                      $",\"active\":{(go.activeSelf ? "true" : "false")}");

            // List component types
            var comps = go.GetComponents<Component>();
            sb.Append(",\"components\":[");
            bool first = true;
            foreach (var c in comps)
            {
                if (c == null) continue;
                if (!first) sb.Append(",");
                sb.Append($"\"{c.GetType().Name}\"");
                first = false;
            }
            sb.Append("]");

            // Recurse children (max depth 10)
            if (t.childCount > 0 && depth < 10)
            {
                sb.Append(",\"children\":[");
                for (int i = 0; i < t.childCount; i++)
                {
                    if (i > 0) sb.Append(",");
                    SerializeHierarchyNode(t.GetChild(i), sb, depth + 1);
                }
                sb.Append("]");
            }
            else if (t.childCount > 0)
            {
                sb.Append($",\"childCount\":{t.childCount}");
            }

            sb.Append("}");
        }

        private static string HandleReparent(string paramsJson)
        {
            var p = JsonUtility.FromJson<ReparentParams>(paramsJson);
            var child = EditorUtility.InstanceIDToObject(p.childId) as GameObject;
            if (child == null)
                return $"{{\"error\":\"Child GameObject {p.childId} not found\"}}";

            Undo.SetTransformParent(child.transform,
                p.parentId != 0
                    ? (EditorUtility.InstanceIDToObject(p.parentId) as GameObject)?.transform
                    : null,
                "MCP Reparent");

            return $"{{\"reparented\":true,\"childId\":{p.childId},\"parentId\":{p.parentId}}}";
        }

        private static string EscapeString(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
