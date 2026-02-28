#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles rendering and camera tools:
    /// unity_camera_setup, unity_post_processing_add, unity_render_settings,
    /// unity_screenshot_capture, unity_cinemachine_vcam_create, unity_cinemachine_set_body_aim
    /// </summary>
    public static class RenderingHandler
    {
        [Serializable] private class CameraParams { public int instanceId; public float fov = -1; public float nearClip = -1; public float farClip = -1; public string clearFlags; public int depth = -1; }
        [Serializable] private class PostProcParams { public int instanceId; public string profilePath; public int isGlobal = 1; }
        [Serializable] private class RenderSetParams { public string qualityLevel; }
        [Serializable] private class ScreenshotParams { public string savePath; public int width; public int height; public int superSize = 1; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_camera_setup": return HandleCamera(paramsJson);
                    case "unity_post_processing_add": return HandlePostProc(paramsJson);
                    case "unity_render_settings": return HandleRenderSettings(paramsJson);
                    case "unity_screenshot_capture": return HandleScreenshot(paramsJson);
                    case "unity_cinemachine_vcam_create": return HandleCinemachineCreate();
                    case "unity_cinemachine_set_body_aim": return "{\"info\":\"Use unity_component_update to set Cinemachine component properties via Reflection.\"}";
                    default: return $"{{\"error\":\"Unknown rendering tool: {tool}\"}}";
                }
            });
        }

        private static string HandleCamera(string paramsJson)
        {
            var p = JsonUtility.FromJson<CameraParams>(paramsJson);
            GameObject go;
            Camera cam;

            if (p.instanceId != 0)
            {
                go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
                if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";
                cam = go.GetComponent<Camera>();
                if (cam == null) cam = Undo.AddComponent<Camera>(go);
            }
            else
            {
                go = new GameObject("Camera");
                cam = go.AddComponent<Camera>();
                Undo.RegisterCreatedObjectUndo(go, "MCP Create Camera");
            }

            Undo.RecordObject(cam, "MCP Camera Setup");
            if (p.fov > 0) cam.fieldOfView = p.fov;
            if (p.nearClip > 0) cam.nearClipPlane = p.nearClip;
            if (p.farClip > 0) cam.farClipPlane = p.farClip;
            if (p.depth >= 0) cam.depth = p.depth;
            if (!string.IsNullOrEmpty(p.clearFlags) && Enum.TryParse<CameraClearFlags>(p.clearFlags, true, out var cf))
                cam.clearFlags = cf;

            EditorUtility.SetDirty(cam);
            return $"{{\"configured\":true,\"instanceId\":{go.GetInstanceID()},\"fov\":{cam.fieldOfView}}}";
        }

        private static string HandlePostProc(string paramsJson)
        {
            var p = JsonUtility.FromJson<PostProcParams>(paramsJson);
            var go = p.instanceId != 0
                ? EditorUtility.InstanceIDToObject(p.instanceId) as GameObject
                : new GameObject("Post-Process Volume");

            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var volume = go.GetComponent<Volume>();
            if (volume == null)
            {
                volume = Undo.AddComponent<Volume>(go);
                if (p.instanceId == 0) Undo.RegisterCreatedObjectUndo(go, "MCP Add Volume");
            }

            volume.isGlobal = p.isGlobal == 1;

            if (!string.IsNullOrEmpty(p.profilePath))
            {
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(p.profilePath);
                if (profile != null) volume.profile = profile;
            }
            else if (volume.profile == null)
            {
                volume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            }

            EditorUtility.SetDirty(volume);
            return $"{{\"added\":true,\"isGlobal\":{(volume.isGlobal ? "true" : "false")}}}";
        }

        private static string HandleRenderSettings(string paramsJson)
        {
            var p = JsonUtility.FromJson<RenderSetParams>(paramsJson);
            if (!string.IsNullOrEmpty(p.qualityLevel))
            {
                var names = QualitySettings.names;
                for (int i = 0; i < names.Length; i++)
                {
                    if (names[i].Equals(p.qualityLevel, StringComparison.OrdinalIgnoreCase))
                    {
                        QualitySettings.SetQualityLevel(i);
                        break;
                    }
                }
            }

            var sb = new StringBuilder("{");
            sb.Append($"\"currentLevel\":\"{QualitySettings.names[QualitySettings.GetQualityLevel()]}\"");
            sb.Append($",\"levels\":[");
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"\"{QualitySettings.names[i]}\"");
            }
            sb.Append("]");
            sb.Append($",\"shadowDistance\":{QualitySettings.shadowDistance}");
            sb.Append($",\"lodBias\":{QualitySettings.lodBias}");
            sb.Append($",\"vSyncCount\":{QualitySettings.vSyncCount}");
            sb.Append("}");
            return sb.ToString();
        }

        private static string HandleScreenshot(string paramsJson)
        {
            var p = JsonUtility.FromJson<ScreenshotParams>(paramsJson);
            var path = p.savePath ?? "Assets/Screenshots/screenshot.png";
            SecurityGuard.ValidatePath(path);

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            ScreenCapture.CaptureScreenshot(path, p.superSize > 0 ? p.superSize : 1);
            return $"{{\"captured\":true,\"path\":\"{path}\"}}";
        }

        private static string HandleCinemachineCreate()
        {
            // Check if Cinemachine package is available
            var cmType = Type.GetType("Unity.Cinemachine.CinemachineCamera, Unity.Cinemachine");
            if (cmType == null)
                cmType = Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine");

            if (cmType == null)
                return "{\"error\":\"Cinemachine package not installed. Install via Package Manager.\"}";

            var go = new GameObject("CM vcam");
            go.AddComponent(cmType);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create Virtual Camera");
            return $"{{\"created\":true,\"instanceId\":{go.GetInstanceID()}}}";
        }
    }
}
#endif
