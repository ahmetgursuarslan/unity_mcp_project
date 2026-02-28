#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles build pipeline tools:
    /// unity_build_player, unity_build_settings, unity_build_scene_list
    /// </summary>
    public static class BuildHandler
    {
        [Serializable] private class BuildParams { public string target; public string path; public string[] scenes; }
        [Serializable] private class SettingsParams { public string development; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_build_player": return HandleBuild(paramsJson);
                    case "unity_build_settings": return HandleSettings(paramsJson);
                    case "unity_build_scene_list": return HandleSceneList();
                    default: return $"{{\"error\":\"Unknown build tool: {tool}\"}}";
                }
            });
        }

        private static string HandleBuild(string paramsJson)
        {
            var p = JsonUtility.FromJson<BuildParams>(paramsJson);
            
            BuildTarget target = (p.target ?? "").ToLower() switch
            {
                "windows" or "win64" or "standalonewindows64" => BuildTarget.StandaloneWindows64,
                "mac" or "osx" or "standaloneosx" => BuildTarget.StandaloneOSX,
                "linux" or "standalonelinux64" => BuildTarget.StandaloneLinux64,
                "android" => BuildTarget.Android,
                "ios" => BuildTarget.iOS,
                "webgl" => BuildTarget.WebGL,
                _ => EditorUserBuildSettings.activeBuildTarget
            };

            var scenes = p.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                var buildScenes = EditorBuildSettings.scenes;
                scenes = new string[buildScenes.Length];
                for (int i = 0; i < buildScenes.Length; i++)
                    scenes[i] = buildScenes[i].path;
            }

            var outputPath = p.path ?? $"Builds/{target}/{Application.productName}";
            if (target == BuildTarget.StandaloneWindows64 && !outputPath.EndsWith(".exe"))
                outputPath += ".exe";

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = EditorUserBuildSettings.development ? BuildOptions.Development : BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            
            return $"{{\"result\":\"{report.summary.result}\"," +
                   $"\"target\":\"{target}\"," +
                   $"\"path\":\"{outputPath}\"," +
                   $"\"totalTime\":\"{report.summary.totalTime}\"," +
                   $"\"totalErrors\":{report.summary.totalErrors}," +
                   $"\"totalWarnings\":{report.summary.totalWarnings}," +
                   $"\"totalSize\":{report.summary.totalSize}}}";
        }

        private static string HandleSettings(string paramsJson)
        {
            var p = JsonUtility.FromJson<SettingsParams>(paramsJson);
            if (p.development != null)
                EditorUserBuildSettings.development = p.development == "true" || p.development == "1";

            return $"{{\"activeBuildTarget\":\"{EditorUserBuildSettings.activeBuildTarget}\"," +
                   $"\"development\":{(EditorUserBuildSettings.development ? "true" : "false")}}}";
        }

        private static string HandleSceneList()
        {
            var scenes = EditorBuildSettings.scenes;
            var sb = new System.Text.StringBuilder("[");
            for (int i = 0; i < scenes.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"path\":\"{scenes[i].path}\",\"enabled\":{(scenes[i].enabled ? "true" : "false")}}}");
            }
            sb.Append("]");
            return $"{{\"scenes\":{sb}}}";
        }
    }
}
#endif
