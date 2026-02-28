#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles prefab tools:
    /// unity_prefab_create, unity_prefab_instantiate, unity_prefab_apply_overrides,
    /// unity_prefab_revert, unity_prefab_unpack
    /// </summary>
    public static class PrefabHandler
    {
        [Serializable] private class CreateParams { public int instanceId; public string savePath; }
        [Serializable] private class InstantiateParams { public string prefabPath; public float[] position; public float[] rotation; public int parentId; }
        [Serializable] private class IdParams { public int instanceId; }
        [Serializable] private class UnpackParams { public int instanceId; public string mode; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_prefab_create": return HandleCreate(paramsJson);
                    case "unity_prefab_instantiate": return HandleInstantiate(paramsJson);
                    case "unity_prefab_apply_overrides": return HandleApply(paramsJson);
                    case "unity_prefab_revert": return HandleRevert(paramsJson);
                    case "unity_prefab_unpack": return HandleUnpack(paramsJson);
                    default: return $"{{\"error\":\"Unknown prefab tool: {tool}\"}}";
                }
            });
        }

        private static string HandleCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var path = p.savePath ?? $"Assets/Prefabs/{go.name}.prefab";
            SecurityGuard.ValidatePath(path);

            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            bool success;
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction, out success);
            return success
                ? $"{{\"created\":true,\"path\":\"{path}\",\"name\":\"{prefab.name}\"}}"
                : "{\"error\":\"Failed to create prefab\"}";
        }

        private static string HandleInstantiate(string paramsJson)
        {
            var p = JsonUtility.FromJson<InstantiateParams>(paramsJson);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.prefabPath);
            if (prefab == null) return $"{{\"error\":\"Prefab not found at {p.prefabPath}\"}}";

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (p.position != null && p.position.Length == 3)
                instance.transform.position = new Vector3(p.position[0], p.position[1], p.position[2]);
            if (p.rotation != null && p.rotation.Length == 3)
                instance.transform.eulerAngles = new Vector3(p.rotation[0], p.rotation[1], p.rotation[2]);

            if (p.parentId != 0)
            {
                var parent = EditorUtility.InstanceIDToObject(p.parentId) as GameObject;
                if (parent != null) instance.transform.SetParent(parent.transform);
            }

            Undo.RegisterCreatedObjectUndo(instance, "MCP Instantiate Prefab");
            return $"{{\"instanceId\":{instance.GetInstanceID()},\"name\":\"{instance.name}\"}}";
        }

        private static string HandleApply(string paramsJson)
        {
            var p = JsonUtility.FromJson<IdParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);
            return "{\"applied\":true}";
        }

        private static string HandleRevert(string paramsJson)
        {
            var p = JsonUtility.FromJson<IdParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            PrefabUtility.RevertPrefabInstance(go, InteractionMode.UserAction);
            return "{\"reverted\":true}";
        }

        private static string HandleUnpack(string paramsJson)
        {
            var p = JsonUtility.FromJson<UnpackParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var mode = (p.mode ?? "root").ToLower() == "completely"
                ? PrefabUnpackMode.Completely
                : PrefabUnpackMode.OutermostRoot;

            PrefabUtility.UnpackPrefabInstance(go, mode, InteractionMode.UserAction);
            return $"{{\"unpacked\":true,\"mode\":\"{mode}\"}}";
        }
    }
}
#endif
