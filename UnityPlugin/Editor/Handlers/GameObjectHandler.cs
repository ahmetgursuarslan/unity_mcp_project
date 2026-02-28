#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    public static class GameObjectHandler
    {
        [Serializable] private class CreateParams 
        { 
            public string name; 
            public string primitiveType; 
            public int parentId; 
        }
        [Serializable] private class DeleteParams { public int instanceId; }
        [Serializable] private class FindParams 
        { 
            public string name; 
            public string tag; 
            public string componentType;
            public string path; // NEW: hierarchy path lookup
        }
        [Serializable] private class InspectParams { public int instanceId; }
        [Serializable] private class UpdateParams
        {
            public int instanceId;
            public string name;
            public string tag;
            public int layer = -1;
            public int isActive = -1;
            public float[] position;
            public float[] rotation;
            public float[] scale;
        }
        [Serializable] private class DuplicateParams { public int instanceId; public string newName; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_object_create": return HandleCreate(paramsJson);
                    case "unity_object_delete": return HandleDelete(paramsJson);
                    case "unity_object_find": return HandleFind(paramsJson);
                    case "unity_object_inspect": return HandleInspect(paramsJson);
                    case "unity_object_update": return HandleUpdate(paramsJson);
                    case "unity_object_duplicate": return HandleDuplicate(paramsJson);
                    case "unity_object_find_by_path": return HandleFindByPath(paramsJson);
                    default: return ResponseHelper.Error($"Unknown object tool: {tool}");
                }
            });
        }

        private static string HandleCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateParams>(paramsJson);
            GameObject go;

            if (!string.IsNullOrEmpty(p.primitiveType))
            {
                if (Enum.TryParse<PrimitiveType>(p.primitiveType, true, out var prim))
                {
                    go = GameObject.CreatePrimitive(prim);
                    go.name = p.name ?? go.name;
                }
                else
                {
                    return ResponseHelper.Error($"Invalid primitiveType: {p.primitiveType}. Valid: Cube, Sphere, Capsule, Cylinder, Plane, Quad");
                }
            }
            else
            {
                go = new GameObject(p.name ?? "New GameObject");
            }

            if (p.parentId != 0)
            {
                var parent = EditorUtility.InstanceIDToObject(p.parentId) as GameObject;
                if (parent != null)
                    go.transform.SetParent(parent.transform);
            }

            Undo.RegisterCreatedObjectUndo(go, $"MCP Create {go.name}");
            return ResponseHelper.Ok(
                JsonHelper.Num("instanceId", go.GetInstanceID()),
                JsonHelper.Str("name", go.name));
        }

        private static string HandleDelete(string paramsJson)
        {
            var p = JsonUtility.FromJson<DeleteParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null)
                return ResponseHelper.Error($"GameObject with instanceId {p.instanceId} not found");

            Undo.DestroyObjectImmediate(go);
            return ResponseHelper.Ok(JsonHelper.Bool("deleted", true));
        }

        private static string HandleFind(string paramsJson)
        {
            var p = JsonUtility.FromJson<FindParams>(paramsJson);
            var results = new List<GameObject>();

            if (!string.IsNullOrEmpty(p.tag))
            {
                try { results.AddRange(GameObject.FindGameObjectsWithTag(p.tag)); }
                catch (UnityException) { }
            }
            else
            {
                var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                results.AddRange(allObjects);
            }

            if (!string.IsNullOrEmpty(p.name))
            {
                results.RemoveAll(go => 
                    !go.name.Contains(p.name, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrEmpty(p.componentType))
            {
                var type = ResolveUnityType(p.componentType);
                if (type != null)
                    results.RemoveAll(go => go.GetComponent(type) == null);
            }

            var items = new string[results.Count];
            for (int i = 0; i < results.Count; i++)
            {
                var go = results[i];
                items[i] = JsonHelper.Obj(
                    JsonHelper.Num("instanceId", go.GetInstanceID()),
                    JsonHelper.Str("name", go.name),
                    JsonHelper.Str("tag", go.tag),
                    JsonHelper.Bool("activeSelf", go.activeSelf));
            }
            return JsonHelper.Arr(items);
        }

        private static string HandleInspect(string paramsJson)
        {
            var p = JsonUtility.FromJson<InspectParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null)
                return ResponseHelper.Error($"GameObject with instanceId {p.instanceId} not found");

            var t = go.transform;
            var components = go.GetComponents<Component>();
            var compItems = new List<string>();
            foreach (var c in components)
            {
                if (c == null) continue;
                compItems.Add(JsonHelper.Obj(
                    JsonHelper.Str("type", c.GetType().Name),
                    JsonHelper.Num("instanceId", c.GetInstanceID())));
            }

            var pos = $"[{t.position.x},{t.position.y},{t.position.z}]";
            var rot = $"[{t.eulerAngles.x},{t.eulerAngles.y},{t.eulerAngles.z}]";
            var scl = $"[{t.localScale.x},{t.localScale.y},{t.localScale.z}]";

            return JsonHelper.Obj(
                JsonHelper.Num("instanceId", go.GetInstanceID()),
                JsonHelper.Str("name", go.name),
                JsonHelper.Str("tag", go.tag),
                JsonHelper.Num("layer", go.layer),
                JsonHelper.Bool("activeSelf", go.activeSelf),
                $"\"transform\":{{\"position\":{pos},\"rotation\":{rot},\"scale\":{scl}}}",
                $"\"components\":{JsonHelper.Arr(compItems.ToArray())}",
                JsonHelper.Num("childCount", t.childCount));
        }

        private static string HandleUpdate(string paramsJson)
        {
            var p = JsonUtility.FromJson<UpdateParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null)
                return ResponseHelper.Error($"GameObject with instanceId {p.instanceId} not found");

            Undo.RecordObject(go, "MCP Update Object");
            Undo.RecordObject(go.transform, "MCP Update Transform");

            if (!string.IsNullOrEmpty(p.name)) go.name = p.name;
            if (!string.IsNullOrEmpty(p.tag)) go.tag = p.tag;
            if (p.layer >= 0) go.layer = p.layer;
            if (p.isActive >= 0) go.SetActive(p.isActive == 1);

            if (p.position != null && p.position.Length == 3)
                go.transform.position = new Vector3(p.position[0], p.position[1], p.position[2]);
            if (p.rotation != null && p.rotation.Length == 3)
                go.transform.eulerAngles = new Vector3(p.rotation[0], p.rotation[1], p.rotation[2]);
            if (p.scale != null && p.scale.Length == 3)
                go.transform.localScale = new Vector3(p.scale[0], p.scale[1], p.scale[2]);

            EditorUtility.SetDirty(go);
            return ResponseHelper.Ok(
                JsonHelper.Bool("updated", true),
                JsonHelper.Num("instanceId", go.GetInstanceID()));
        }

        // NEW: Duplicate a GameObject
        private static string HandleDuplicate(string paramsJson)
        {
            var p = JsonUtility.FromJson<DuplicateParams>(paramsJson);
            var source = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (source == null)
                return ResponseHelper.Error($"GameObject with instanceId {p.instanceId} not found");

            var clone = UnityEngine.Object.Instantiate(source, source.transform.parent);
            clone.name = !string.IsNullOrEmpty(p.newName) ? p.newName : source.name + " (Copy)";
            Undo.RegisterCreatedObjectUndo(clone, $"MCP Duplicate {source.name}");

            return ResponseHelper.Ok(
                JsonHelper.Num("instanceId", clone.GetInstanceID()),
                JsonHelper.Str("name", clone.name),
                JsonHelper.Num("sourceId", source.GetInstanceID()));
        }

        // NEW: Find by hierarchy path (e.g. "Player/Camera/Main")
        private static string HandleFindByPath(string paramsJson)
        {
            var p = JsonUtility.FromJson<FindParams>(paramsJson);
            if (string.IsNullOrEmpty(p.path))
                return ResponseHelper.Error("path is required (e.g. 'Player/Camera/Main')");

            // Split path and find root first
            var parts = p.path.Split('/');
            var rootName = parts[0];

            GameObject root = null;
            var allRoots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in allRoots)
            {
                if (r.name == rootName) { root = r; break; }
            }
            if (root == null)
                return ResponseHelper.Error($"Root object '{rootName}' not found");

            Transform current = root.transform;
            for (int i = 1; i < parts.Length; i++)
            {
                var child = current.Find(parts[i]);
                if (child == null)
                    return ResponseHelper.Error($"Child '{parts[i]}' not found under '{current.name}'");
                current = child;
            }

            var go = current.gameObject;
            return ResponseHelper.Ok(
                JsonHelper.Num("instanceId", go.GetInstanceID()),
                JsonHelper.Str("name", go.name),
                JsonHelper.Str("path", p.path),
                JsonHelper.Bool("activeSelf", go.activeSelf));
        }

        internal static Type ResolveUnityType(string typeName)
        {
            var type = Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule");
            if (type != null) return type;
            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine.PhysicsModule");
            if (type != null) return type;
            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine.AudioModule");
            if (type != null) return type;
            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine.AnimationModule");
            if (type != null) return type;
            type = Type.GetType($"UnityEngine.{typeName}, UnityEngine.UIModule");
            if (type != null) return type;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName) ?? asm.GetType($"UnityEngine.{typeName}");
                if (type != null) return type;
            }
            return null;
        }
    }
}
#endif
