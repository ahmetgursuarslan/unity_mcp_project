#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles terrain tools:
    /// unity_terrain_create, unity_terrain_set_heightmap, unity_terrain_paint_texture,
    /// unity_terrain_place_trees, unity_terrain_place_details, unity_terrain_set_settings
    /// </summary>
    public static class TerrainHandler
    {
        [Serializable] private class CreateParams { public string name; public float width = 500; public float height = 600; public float length = 500; public int heightmapRes = 513; }
        [Serializable] private class HeightParams { public int instanceId; public int x; public int y; public int width; public int height; public float[] heights; }
        [Serializable] private class PaintParams { public int instanceId; public int layerIndex; public int x; public int y; public int width; public int height; public float opacity; }
        [Serializable] private class TreeParams { public int instanceId; public string prefabPath; public float[] position; public float widthScale = 1; public float heightScale = 1; }
        [Serializable] private class SettingsParams { public int instanceId; public float detailDistance = -1; public float treeDistance = -1; public int pixelError = -1; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_terrain_create": return HandleCreate(paramsJson);
                    case "unity_terrain_set_heightmap": return HandleHeightmap(paramsJson);
                    case "unity_terrain_paint_texture": return HandlePaint(paramsJson);
                    case "unity_terrain_place_trees": return HandleTrees(paramsJson);
                    case "unity_terrain_place_details": return "{\"info\":\"Detail placement requires TerrainData.SetDetailLayer with density maps.\"}";
                    case "unity_terrain_set_settings": return HandleSettings(paramsJson);
                    default: return $"{{\"error\":\"Unknown terrain tool: {tool}\"}}";
                }
            });
        }

        private static string HandleCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateParams>(paramsJson);
            var terrainData = new TerrainData();
            terrainData.heightmapResolution = p.heightmapRes;
            terrainData.size = new Vector3(p.width, p.height, p.length);

            var go = Terrain.CreateTerrainGameObject(terrainData);
            go.name = p.name ?? "Terrain";

            var path = $"Assets/Terrains/{go.name}_Data.asset";
            SecurityGuard.ValidatePath(path);
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(terrainData, path);
            AssetDatabase.SaveAssets();
            Undo.RegisterCreatedObjectUndo(go, "MCP Create Terrain");

            return $"{{\"instanceId\":{go.GetInstanceID()},\"dataPath\":\"{path}\",\"size\":[{p.width},{p.height},{p.length}]}}";
        }

        private static string HandleHeightmap(string paramsJson)
        {
            var p = JsonUtility.FromJson<HeightParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"Terrain {p.instanceId} not found\"}}";
            var terrain = go.GetComponent<Terrain>();
            if (terrain == null) return "{\"error\":\"No Terrain component\"}";

            if (p.heights != null)
            {
                var h = p.height > 0 ? p.height : 1;
                var w = p.width > 0 ? p.width : 1;
                var heights = new float[h, w];
                for (int iy = 0; iy < h && iy * w < p.heights.Length; iy++)
                    for (int ix = 0; ix < w && iy * w + ix < p.heights.Length; ix++)
                        heights[iy, ix] = p.heights[iy * w + ix];

                Undo.RecordObject(terrain.terrainData, "MCP Set Heightmap");
                terrain.terrainData.SetHeights(p.x, p.y, heights);
            }
            return "{\"set\":true}";
        }

        private static string HandlePaint(string paramsJson)
        {
            var p = JsonUtility.FromJson<PaintParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"Terrain {p.instanceId} not found\"}}";
            var terrain = go.GetComponent<Terrain>();
            if (terrain == null) return "{\"error\":\"No Terrain component\"}";

            var w = p.width > 0 ? p.width : 1;
            var h = p.height > 0 ? p.height : 1;
            var layerCount = terrain.terrainData.alphamapLayers;
            if (p.layerIndex >= layerCount) return $"{{\"error\":\"Layer index {p.layerIndex} out of range (max {layerCount - 1})\"}}";

            var alphas = terrain.terrainData.GetAlphamaps(p.x, p.y, w, h);
            var opacity = p.opacity > 0 ? Mathf.Clamp01(p.opacity) : 1f;

            for (int iy = 0; iy < h; iy++)
                for (int ix = 0; ix < w; ix++)
                {
                    for (int l = 0; l < layerCount; l++)
                        alphas[iy, ix, l] = l == p.layerIndex ? opacity : (1f - opacity) / (layerCount - 1);
                }

            Undo.RecordObject(terrain.terrainData, "MCP Paint Terrain");
            terrain.terrainData.SetAlphamaps(p.x, p.y, alphas);
            return $"{{\"painted\":true,\"layer\":{p.layerIndex}}}";
        }

        private static string HandleTrees(string paramsJson)
        {
            var p = JsonUtility.FromJson<TreeParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"Terrain {p.instanceId} not found\"}}";
            var terrain = go.GetComponent<Terrain>();
            if (terrain == null) return "{\"error\":\"No Terrain component\"}";

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.prefabPath);
            if (prefab == null) return $"{{\"error\":\"Tree prefab not found at {p.prefabPath}\"}}";

            // Add tree prototype if not exists
            var protos = terrain.terrainData.treePrototypes;
            int protoIdx = -1;
            for (int i = 0; i < protos.Length; i++)
            {
                if (protos[i].prefab == prefab) { protoIdx = i; break; }
            }
            if (protoIdx < 0)
            {
                var newProtos = new TreePrototype[protos.Length + 1];
                protos.CopyTo(newProtos, 0);
                newProtos[protos.Length] = new TreePrototype { prefab = prefab };
                terrain.terrainData.treePrototypes = newProtos;
                protoIdx = protos.Length;
            }

            // Place tree
            var size = terrain.terrainData.size;
            var treeInstance = new TreeInstance
            {
                prototypeIndex = protoIdx,
                position = new Vector3(p.position[0] / size.x, 0, p.position[2] / size.z),
                widthScale = p.widthScale,
                heightScale = p.heightScale,
                color = Color.white,
                lightmapColor = Color.white
            };

            Undo.RecordObject(terrain.terrainData, "MCP Place Tree");
            terrain.AddTreeInstance(treeInstance);
            terrain.Flush();
            return $"{{\"placed\":true,\"prototypeIndex\":{protoIdx}}}";
        }

        private static string HandleSettings(string paramsJson)
        {
            var p = JsonUtility.FromJson<SettingsParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"Terrain {p.instanceId} not found\"}}";
            var terrain = go.GetComponent<Terrain>();
            if (terrain == null) return "{\"error\":\"No Terrain component\"}";

            Undo.RecordObject(terrain, "MCP Terrain Settings");
            if (p.detailDistance > 0) terrain.detailObjectDistance = p.detailDistance;
            if (p.treeDistance > 0) terrain.treeDistance = p.treeDistance;
            if (p.pixelError > 0) terrain.heightmapPixelError = p.pixelError;

            return $"{{\"updated\":true,\"detailDistance\":{terrain.detailObjectDistance},\"treeDistance\":{terrain.treeDistance}}}";
        }
    }
}
#endif
