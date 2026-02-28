#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antigravity.MCP.Editor.Handlers
{
    public static class SceneHandler
    {
        [Serializable] private class LoadParams { public string scenePath; }
        [Serializable] private class CreateParams { public string sceneName; public string savePath; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_scene_load": return HandleLoad(paramsJson);
                    case "unity_scene_create": return HandleCreate(paramsJson);
                    case "unity_scene_save": return HandleSave();
                    case "unity_scene_list": return HandleList();
                    default: return ResponseHelper.Error($"Unknown scene tool: {tool}");
                }
            });
        }

        private static string HandleLoad(string paramsJson)
        {
            var p = JsonUtility.FromJson<LoadParams>(paramsJson);
            if (string.IsNullOrEmpty(p.scenePath))
                return ResponseHelper.Error("scenePath is required");

            SecurityGuard.ValidatePath(p.scenePath);
            var scene = EditorSceneManager.OpenScene(p.scenePath, OpenSceneMode.Single);
            return ResponseHelper.Ok(
                JsonHelper.Str("sceneName", scene.name),
                JsonHelper.Str("path", scene.path),
                JsonHelper.Num("rootCount", scene.rootCount));
        }

        private static string HandleCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateParams>(paramsJson);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            if (!string.IsNullOrEmpty(p.savePath))
            {
                SecurityGuard.ValidatePath(p.savePath);
                var dir = Path.GetDirectoryName(p.savePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                EditorSceneManager.SaveScene(scene, p.savePath);
            }

            return ResponseHelper.Ok(
                JsonHelper.Str("sceneName", scene.name),
                JsonHelper.Str("path", scene.path));
        }

        private static string HandleSave()
        {
            var scene = SceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            return ResponseHelper.Ok(
                JsonHelper.Bool("saved", true),
                JsonHelper.Str("sceneName", scene.name),
                JsonHelper.Str("path", scene.path));
        }

        private static string HandleList()
        {
            // List ALL scene files in project, not just Build Settings
            var guids = AssetDatabase.FindAssets("t:Scene");
            var items = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                // Check if scene is in build settings
                bool inBuildSettings = false;
                int buildIndex = -1;
                for (int b = 0; b < SceneManager.sceneCountInBuildSettings; b++)
                {
                    if (SceneUtility.GetScenePathByBuildIndex(b) == path)
                    {
                        inBuildSettings = true;
                        buildIndex = b;
                        break;
                    }
                }
                items[i] = JsonHelper.Obj(
                    JsonHelper.Str("path", path),
                    JsonHelper.Bool("inBuildSettings", inBuildSettings),
                    JsonHelper.Num("buildIndex", buildIndex));
            }
            return $"{{\"scenes\":{JsonHelper.Arr(items)}}}";
        }
    }
}
#endif
