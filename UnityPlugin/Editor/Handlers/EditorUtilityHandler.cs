#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles editor utility tools:
    /// unity_console_get_logs, unity_console_clear, unity_selection_set,
    /// unity_scene_view_focus, unity_scene_view_set_camera, unity_undo_perform,
    /// unity_editor_prefs, unity_run_tests
    /// </summary>
    public static class EditorUtilityHandler
    {
        [Serializable] private class SelectionParams { public int[] instanceIds; }
        [Serializable] private class FocusParams { public int instanceId; }
        [Serializable] private class CameraParams { public float[] position; public float[] rotation; public float size; }
        [Serializable] private class UndoParams { public string action; }
        [Serializable] private class PrefsParams { public string key; public string value; public string type; }
        [Serializable] private class LogParams { public int count; public string filter; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_console_get_logs": return HandleGetLogs(paramsJson);
                    case "unity_console_clear": return HandleClearConsole();
                    case "unity_selection_set": return HandleSelection(paramsJson);
                    case "unity_scene_view_focus": return HandleFocus(paramsJson);
                    case "unity_scene_view_set_camera": return HandleSetCamera(paramsJson);
                    case "unity_undo_perform": return HandleUndo(paramsJson);
                    case "unity_editor_prefs": return HandleEditorPrefs(paramsJson);
                    case "unity_run_tests": return HandleRunTests();
                    default: return $"{{\"error\":\"Unknown editor utility tool: {tool}\"}}";
                }
            });
        }

        private static string HandleGetLogs(string paramsJson)
        {
            // Unity doesn't expose console logs directly in a simple API
            // We can use Application.logMessageReceived but it's runtime
            return "{\"info\":\"Console log history not directly accessible in Editor API. Use Debug.Log for new messages, or check Editor.log file.\"}";
        }

        private static string HandleClearConsole()
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(UnityEditor.Editor));
            var type = assembly.GetType("UnityEditor.LogEntries");
            var method = type.GetMethod("Clear");
            method.Invoke(null, null);
            return "{\"cleared\":true}";
        }

        private static string HandleSelection(string paramsJson)
        {
            var p = JsonUtility.FromJson<SelectionParams>(paramsJson);
            if (p.instanceIds == null) return "{\"error\":\"instanceIds is required\"}";

            var objects = new UnityEngine.Object[p.instanceIds.Length];
            for (int i = 0; i < p.instanceIds.Length; i++)
            {
                objects[i] = EditorUtility.InstanceIDToObject(p.instanceIds[i]);
            }
            Selection.objects = objects;
            return $"{{\"selected\":{p.instanceIds.Length}}}";
        }

        private static string HandleFocus(string paramsJson)
        {
            var p = JsonUtility.FromJson<FocusParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            Selection.activeGameObject = go;
            SceneView.lastActiveSceneView?.FrameSelected();
            return $"{{\"focused\":true,\"name\":\"{go.name}\"}}";
        }

        private static string HandleSetCamera(string paramsJson)
        {
            var p = JsonUtility.FromJson<CameraParams>(paramsJson);
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return "{\"error\":\"No active Scene View\"}";

            if (p.position != null && p.position.Length == 3)
                sv.pivot = new Vector3(p.position[0], p.position[1], p.position[2]);
            if (p.rotation != null && p.rotation.Length == 3)
                sv.rotation = Quaternion.Euler(p.rotation[0], p.rotation[1], p.rotation[2]);
            if (p.size > 0) sv.size = p.size;

            sv.Repaint();
            return $"{{\"set\":true,\"pivot\":[{sv.pivot.x},{sv.pivot.y},{sv.pivot.z}]}}";
        }

        private static string HandleUndo(string paramsJson)
        {
            var p = JsonUtility.FromJson<UndoParams>(paramsJson);
            switch ((p.action ?? "undo").ToLower())
            {
                case "undo": Undo.PerformUndo(); return "{\"performed\":\"undo\"}";
                case "redo": Undo.PerformRedo(); return "{\"performed\":\"redo\"}";
                default: return "{\"error\":\"action must be 'undo' or 'redo'\"}";
            }
        }

        private static string HandleEditorPrefs(string paramsJson)
        {
            var p = JsonUtility.FromJson<PrefsParams>(paramsJson);
            if (string.IsNullOrEmpty(p.key)) return "{\"error\":\"key is required\"}";

            if (p.value != null)
            {
                // Set
                switch ((p.type ?? "string").ToLower())
                {
                    case "int": EditorPrefs.SetInt(p.key, int.Parse(p.value)); break;
                    case "float": EditorPrefs.SetFloat(p.key, float.Parse(p.value, System.Globalization.CultureInfo.InvariantCulture)); break;
                    case "bool": EditorPrefs.SetBool(p.key, p.value == "true" || p.value == "1"); break;
                    default: EditorPrefs.SetString(p.key, p.value); break;
                }
                return $"{{\"set\":true,\"key\":\"{p.key}\"}}";
            }
            else
            {
                // Get
                if (EditorPrefs.HasKey(p.key))
                    return $"{{\"key\":\"{p.key}\",\"value\":\"{EditorPrefs.GetString(p.key)}\"}}";
                return $"{{\"key\":\"{p.key}\",\"exists\":false}}";
            }
        }

        private static string HandleRunTests()
        {
            var testRunnerType = Type.GetType("UnityEditor.TestTools.TestRunner.Api.TestRunnerApi, UnityEditor.TestRunner");
            if (testRunnerType == null)
                return "{\"info\":\"Test Runner API available via menu: Window > General > Test Runner.\"}";

            return "{\"info\":\"Tests can be triggered via Window > General > Test Runner, or use unity_execute_menu_item.\"}";
        }
    }
}
#endif
