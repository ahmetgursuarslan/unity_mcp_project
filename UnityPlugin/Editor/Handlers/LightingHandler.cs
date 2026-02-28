#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles lighting tools:
    /// unity_light_create, unity_light_bake, unity_reflection_probe_add,
    /// unity_light_probe_group, unity_environment_settings
    /// </summary>
    public static class LightingHandler
    {
        [Serializable] private class LightCreateParams { public string type; public float[] color; public float intensity = 1; public int shadows = -1; public string name; public float[] position; }
        [Serializable] private class EnvParams { public string skyboxPath; public float[] ambientColor; public string ambientMode; public int fog = -1; public float[] fogColor; public float fogDensity = -1; }
        [Serializable] private class ProbeParams { public float[] position; public float[] size; public string mode; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_light_create": return HandleLightCreate(paramsJson);
                    case "unity_light_bake": return HandleBake();
                    case "unity_reflection_probe_add": return HandleReflectionProbe(paramsJson);
                    case "unity_light_probe_group": return HandleLightProbeGroup(paramsJson);
                    case "unity_environment_settings": return HandleEnvironment(paramsJson);
                    default: return $"{{\"error\":\"Unknown lighting tool: {tool}\"}}";
                }
            });
        }

        private static string HandleLightCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<LightCreateParams>(paramsJson);
            var go = new GameObject(p.name ?? "New Light");
            var light = go.AddComponent<Light>();

            if (Enum.TryParse<LightType>(p.type ?? "Directional", true, out var lt))
                light.type = lt;
            if (p.color != null && p.color.Length >= 3)
                light.color = new Color(p.color[0], p.color[1], p.color[2]);
            light.intensity = p.intensity;
            if (p.shadows >= 0)
                light.shadows = (LightShadows)p.shadows;
            if (p.position != null && p.position.Length == 3)
                go.transform.position = new Vector3(p.position[0], p.position[1], p.position[2]);

            Undo.RegisterCreatedObjectUndo(go, "MCP Create Light");
            return $"{{\"instanceId\":{go.GetInstanceID()},\"type\":\"{light.type}\"}}";
        }

        private static string HandleBake()
        {
            Lightmapping.BakeAsync();
            return "{\"bakeStarted\":true}";
        }

        private static string HandleReflectionProbe(string paramsJson)
        {
            var p = JsonUtility.FromJson<ProbeParams>(paramsJson);
            var go = new GameObject("Reflection Probe");
            var probe = go.AddComponent<ReflectionProbe>();

            if (p.position != null && p.position.Length == 3) go.transform.position = new Vector3(p.position[0], p.position[1], p.position[2]);
            if (p.size != null && p.size.Length == 3) probe.size = new Vector3(p.size[0], p.size[1], p.size[2]);
            if (!string.IsNullOrEmpty(p.mode) && Enum.TryParse<ReflectionProbeMode>(p.mode, true, out var m)) probe.mode = m;

            Undo.RegisterCreatedObjectUndo(go, "MCP Add Reflection Probe");
            return $"{{\"instanceId\":{go.GetInstanceID()}}}";
        }

        private static string HandleLightProbeGroup(string paramsJson)
        {
            var p = JsonUtility.FromJson<ProbeParams>(paramsJson);
            var go = new GameObject("Light Probe Group");
            go.AddComponent<LightProbeGroup>();
            if (p.position != null && p.position.Length == 3)
                go.transform.position = new Vector3(p.position[0], p.position[1], p.position[2]);

            Undo.RegisterCreatedObjectUndo(go, "MCP Add Light Probe Group");
            return $"{{\"instanceId\":{go.GetInstanceID()}}}";
        }

        private static string HandleEnvironment(string paramsJson)
        {
            var p = JsonUtility.FromJson<EnvParams>(paramsJson);
            if (!string.IsNullOrEmpty(p.skyboxPath))
            {
                var skyMat = AssetDatabase.LoadAssetAtPath<Material>(p.skyboxPath);
                if (skyMat != null) RenderSettings.skybox = skyMat;
            }
            if (p.ambientColor != null && p.ambientColor.Length >= 3)
                RenderSettings.ambientLight = new Color(p.ambientColor[0], p.ambientColor[1], p.ambientColor[2]);
            if (!string.IsNullOrEmpty(p.ambientMode) && Enum.TryParse<AmbientMode>(p.ambientMode, true, out var am))
                RenderSettings.ambientMode = am;
            if (p.fog >= 0) RenderSettings.fog = p.fog == 1;
            if (p.fogColor != null && p.fogColor.Length >= 3)
                RenderSettings.fogColor = new Color(p.fogColor[0], p.fogColor[1], p.fogColor[2]);
            if (p.fogDensity >= 0) RenderSettings.fogDensity = p.fogDensity;

            return $"{{\"updated\":true,\"fog\":{(RenderSettings.fog ? "true" : "false")},\"ambientMode\":\"{RenderSettings.ambientMode}\"}}";
        }
    }
}
#endif
