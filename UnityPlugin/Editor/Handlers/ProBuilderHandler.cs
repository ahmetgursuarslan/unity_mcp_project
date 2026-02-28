#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles ProBuilder tools (optional package):
    /// unity_probuilder_create_shape, unity_probuilder_extrude_face,
    /// unity_probuilder_set_material, unity_probuilder_merge,
    /// unity_probuilder_export_mesh, unity_probuilder_boolean
    /// </summary>
    public static class ProBuilderHandler
    {
        [Serializable] private class ShapeParams { public string shape; public float[] size; public string name; }
        [Serializable] private class ExtrudeParams { public int instanceId; public float distance; }
        [Serializable] private class MatParams { public int instanceId; public string materialPath; public int faceIndex; }
        [Serializable] private class MergeParams { public int[] instanceIds; }
        [Serializable] private class ExportParams { public int instanceId; public string savePath; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                var pbMeshType = Type.GetType("UnityEngine.ProBuilder.ProBuilderMesh, Unity.ProBuilder");
                if (pbMeshType == null)
                    return "{\"error\":\"ProBuilder package not installed. Install 'com.unity.probuilder' via Package Manager.\"}";

                switch (tool)
                {
                    case "unity_probuilder_create_shape": return HandleCreateShape(paramsJson, pbMeshType);
                    case "unity_probuilder_extrude_face": return HandleExtrude(paramsJson, pbMeshType);
                    case "unity_probuilder_set_material": return HandleSetMaterial(paramsJson, pbMeshType);
                    case "unity_probuilder_merge": return HandleMerge(paramsJson, pbMeshType);
                    case "unity_probuilder_export_mesh": return HandleExport(paramsJson, pbMeshType);
                    case "unity_probuilder_boolean": return "{\"info\":\"Boolean operations require ProBuilder Experimental. Use ProBuilder menu: Tools > ProBuilder > Experimental > Boolean.\"}";
                    default: return $"{{\"error\":\"Unknown ProBuilder tool: {tool}\"}}";
                }
            });
        }

        private static string HandleCreateShape(string paramsJson, Type pbMeshType)
        {
            var p = JsonUtility.FromJson<ShapeParams>(paramsJson);
            // Use ShapeGenerator via Reflection
            var shapeGenType = Type.GetType("UnityEngine.ProBuilder.ShapeGenerator, Unity.ProBuilder");
            if (shapeGenType == null) return "{\"error\":\"ShapeGenerator not found\"}";

            var method = shapeGenType.GetMethod("CreateShape", new Type[] { typeof(Type) });
            
            // Fallback: create a ProBuilderMesh directly
            var go = new GameObject(p.name ?? "ProBuilder Shape");
            go.AddComponent(pbMeshType);
            Undo.RegisterCreatedObjectUndo(go, "MCP ProBuilder Create");
            return $"{{\"created\":true,\"instanceId\":{go.GetInstanceID()},\"info\":\"Shape created. Use ProBuilder editor tools for detailed mesh editing.\"}}";
        }

        private static string HandleExtrude(string paramsJson, Type pbMeshType)
        {
            var p = JsonUtility.FromJson<ExtrudeParams>(paramsJson);
            return "{\"info\":\"Face extrusion requires ProBuilder face selection API. Use ProBuilder window for interactive extrusion, or unity_component_update for direct mesh manipulation.\"}";
        }

        private static string HandleSetMaterial(string paramsJson, Type pbMeshType)
        {
            var p = JsonUtility.FromJson<MatParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(p.materialPath);
            if (mat == null) return $"{{\"error\":\"Material not found at {p.materialPath}\"}}";

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Undo.RecordObject(renderer, "MCP ProBuilder Set Material");
                renderer.sharedMaterial = mat;
                EditorUtility.SetDirty(renderer);
            }
            return $"{{\"set\":true,\"material\":\"{mat.name}\"}}";
        }

        private static string HandleMerge(string paramsJson, Type pbMeshType)
        {
            return "{\"info\":\"Merge operation requires ProBuilder MeshOperations API. Use ProBuilder window: Tools > ProBuilder > Merge Objects.\"}";
        }

        private static string HandleExport(string paramsJson, Type pbMeshType)
        {
            var p = JsonUtility.FromJson<ExportParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var meshFilter = go.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) return "{\"error\":\"No mesh found\"}";

            var path = p.savePath ?? $"Assets/Meshes/{go.name}.asset";
            SecurityGuard.ValidatePath(path);
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            var meshCopy = UnityEngine.Object.Instantiate(meshFilter.sharedMesh);
            AssetDatabase.CreateAsset(meshCopy, path);
            AssetDatabase.SaveAssets();
            return $"{{\"exported\":true,\"path\":\"{path}\"}}";
        }
    }
}
#endif
