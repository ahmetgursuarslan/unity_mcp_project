#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles player settings and global project settings tools:
    /// unity_player_settings, unity_player_resolution, unity_time_settings,
    /// unity_color_space, unity_graphics_api, unity_scripting_backend,
    /// unity_script_execution_order
    /// </summary>
    public static class PlayerSettingsHandler
    {
        [Serializable] private class PlayerParams { public string companyName; public string productName; public string bundleId; }
        [Serializable] private class ResolutionParams { public int width; public int height; public int fullscreen = -1; public int vSync = -1; }
        [Serializable] private class TimeParams { public float fixedTimestep = -1; public float maxTimestep = -1; public float timeScale = -1; }
        [Serializable] private class ColorSpaceParams { public string colorSpace; }
        [Serializable] private class ScriptingParams { public string backend; }
        [Serializable] private class ExecOrderParams { public string scriptName; public int order; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_player_settings": return HandlePlayerSettings(paramsJson);
                    case "unity_player_resolution": return HandleResolution(paramsJson);
                    case "unity_time_settings": return HandleTime(paramsJson);
                    case "unity_color_space": return HandleColorSpace(paramsJson);
                    case "unity_graphics_api": return HandleGraphicsApi();
                    case "unity_scripting_backend": return HandleScriptingBackend(paramsJson);
                    case "unity_script_execution_order": return HandleExecOrder(paramsJson);
                    default: return $"{{\"error\":\"Unknown settings tool: {tool}\"}}";
                }
            });
        }

        private static string HandlePlayerSettings(string paramsJson)
        {
            var p = JsonUtility.FromJson<PlayerParams>(paramsJson);
            if (!string.IsNullOrEmpty(p.companyName)) PlayerSettings.companyName = p.companyName;
            if (!string.IsNullOrEmpty(p.productName)) PlayerSettings.productName = p.productName;
            if (!string.IsNullOrEmpty(p.bundleId)) PlayerSettings.SetApplicationIdentifier(EditorUserBuildSettings.selectedBuildTargetGroup, p.bundleId);

            return $"{{\"companyName\":\"{PlayerSettings.companyName}\"" +
                   $",\"productName\":\"{PlayerSettings.productName}\"" +
                   $",\"bundleId\":\"{PlayerSettings.GetApplicationIdentifier(EditorUserBuildSettings.selectedBuildTargetGroup)}\"}}";
        }

        private static string HandleResolution(string paramsJson)
        {
            var p = JsonUtility.FromJson<ResolutionParams>(paramsJson);
            if (p.width > 0) PlayerSettings.defaultScreenWidth = p.width;
            if (p.height > 0) PlayerSettings.defaultScreenHeight = p.height;
            if (p.fullscreen >= 0) PlayerSettings.fullScreenMode = p.fullscreen == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (p.vSync >= 0) QualitySettings.vSyncCount = p.vSync;

            return $"{{\"width\":{PlayerSettings.defaultScreenWidth},\"height\":{PlayerSettings.defaultScreenHeight}}}";
        }

        private static string HandleTime(string paramsJson)
        {
            var p = JsonUtility.FromJson<TimeParams>(paramsJson);
            if (p.fixedTimestep > 0) Time.fixedDeltaTime = p.fixedTimestep;
            if (p.maxTimestep > 0) Time.maximumDeltaTime = p.maxTimestep;
            if (p.timeScale >= 0) Time.timeScale = p.timeScale;

            return $"{{\"fixedDeltaTime\":{Time.fixedDeltaTime},\"maximumDeltaTime\":{Time.maximumDeltaTime},\"timeScale\":{Time.timeScale}}}";
        }

        private static string HandleColorSpace(string paramsJson)
        {
            var p = JsonUtility.FromJson<ColorSpaceParams>(paramsJson);
            if (!string.IsNullOrEmpty(p.colorSpace) && Enum.TryParse<ColorSpace>(p.colorSpace, true, out var cs))
                PlayerSettings.colorSpace = cs;
            return $"{{\"colorSpace\":\"{PlayerSettings.colorSpace}\"}}";
        }

        private static string HandleGraphicsApi()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var apis = PlayerSettings.GetGraphicsAPIs(target);
            var sb = new StringBuilder("[");
            for (int i = 0; i < apis.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"\"{apis[i]}\"");
            }
            sb.Append("]");
            return $"{{\"platform\":\"{target}\",\"graphicsAPIs\":{sb}}}";
        }

        private static string HandleScriptingBackend(string paramsJson)
        {
            var p = JsonUtility.FromJson<ScriptingParams>(paramsJson);
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            if (!string.IsNullOrEmpty(p.backend) && Enum.TryParse<ScriptingImplementation>(p.backend, true, out var si))
                PlayerSettings.SetScriptingBackend(group, si);
            return $"{{\"backend\":\"{PlayerSettings.GetScriptingBackend(group)}\"}}";
        }

        private static string HandleExecOrder(string paramsJson)
        {
            var p = JsonUtility.FromJson<ExecOrderParams>(paramsJson);
            // Find the MonoScript for the given script name
            var guids = AssetDatabase.FindAssets($"t:MonoScript {p.scriptName}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.name == p.scriptName)
                {
                    MonoImporter.SetExecutionOrder(script, p.order);
                    return $"{{\"set\":true,\"script\":\"{p.scriptName}\",\"order\":{p.order}}}";
                }
            }
            return $"{{\"error\":\"Script '{p.scriptName}' not found\"}}";
        }
    }
}
#endif
