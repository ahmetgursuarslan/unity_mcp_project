#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles 2D system tools:
    /// unity_sprite_create, unity_sprite_atlas_create, unity_tilemap_create,
    /// unity_tilemap_set_tile, unity_tilemap_paint_area, unity_tilemap_clear,
    /// unity_2d_physics_setup, unity_sorting_layer_manage, unity_sprite_shape_create
    /// </summary>
    public static class TwoDHandler
    {
        [Serializable] private class SpriteParams { public string texturePath; public int pixelsPerUnit = 100; }
        [Serializable] private class TilemapCreateParams { public string name; public string gridType; }
        [Serializable] private class TileSetParams { public int tilemapId; public int x; public int y; public string tilePath; }
        [Serializable] private class TilePaintParams { public int tilemapId; public int x1; public int y1; public int x2; public int y2; public string tilePath; }
        [Serializable] private class TilemapClearParams { public int tilemapId; }
        [Serializable] private class Physics2DParams { public int instanceId; public string bodyType; public float mass = -1; public float gravityScale = -1; public string colliderType; }
        [Serializable] private class SortingParams { public string layerName; public int order = -1; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_sprite_create": return HandleSpriteCreate(paramsJson);
                    case "unity_sprite_atlas_create": return "{\"info\":\"Create SpriteAtlas via Assets > Create > 2D > Sprite Atlas, or use unity_execute_menu_item.\"}";
                    case "unity_tilemap_create": return HandleTilemapCreate(paramsJson);
                    case "unity_tilemap_set_tile": return HandleTileSet(paramsJson);
                    case "unity_tilemap_paint_area": return HandleTilePaint(paramsJson);
                    case "unity_tilemap_clear": return HandleTilemapClear(paramsJson);
                    case "unity_2d_physics_setup": return HandlePhysics2D(paramsJson);
                    case "unity_sorting_layer_manage": return HandleSortingLayer(paramsJson);
                    case "unity_sprite_shape_create": return "{\"info\":\"SpriteShape requires the 2D SpriteShape package. Use unity_component_add to add SpriteShapeController.\"}";
                    default: return $"{{\"error\":\"Unknown 2D tool: {tool}\"}}";
                }
            });
        }

        private static string HandleSpriteCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<SpriteParams>(paramsJson);
            var importer = AssetImporter.GetAtPath(p.texturePath) as TextureImporter;
            if (importer == null) return $"{{\"error\":\"No TextureImporter at {p.texturePath}\"}}";

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = p.pixelsPerUnit;
            importer.SaveAndReimport();
            return $"{{\"configured\":true,\"path\":\"{p.texturePath}\",\"pixelsPerUnit\":{p.pixelsPerUnit}}}";
        }

        private static string HandleTilemapCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<TilemapCreateParams>(paramsJson);
            var gridGo = new GameObject(p.name ?? "Tilemap Grid");
            var grid = gridGo.AddComponent<Grid>();

            if (!string.IsNullOrEmpty(p.gridType))
            {
                if (Enum.TryParse<GridLayout.CellLayout>(p.gridType, true, out var layout))
                    grid.cellLayout = layout;
            }

            var tilemapGo = new GameObject("Tilemap");
            tilemapGo.transform.SetParent(gridGo.transform);
            var tilemap = tilemapGo.AddComponent<Tilemap>();
            tilemapGo.AddComponent<TilemapRenderer>();

            Undo.RegisterCreatedObjectUndo(gridGo, "MCP Create Tilemap");
            return $"{{\"gridId\":{gridGo.GetInstanceID()},\"tilemapId\":{tilemapGo.GetInstanceID()}}}";
        }

        private static string HandleTileSet(string paramsJson)
        {
            var p = JsonUtility.FromJson<TileSetParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.tilemapId) as GameObject;
            if (go == null) return $"{{\"error\":\"Tilemap object {p.tilemapId} not found\"}}";
            var tilemap = go.GetComponent<Tilemap>();
            if (tilemap == null) return "{\"error\":\"No Tilemap component on object\"}";

            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(p.tilePath);
            if (tile == null) return $"{{\"error\":\"Tile not found at {p.tilePath}\"}}";

            Undo.RecordObject(tilemap, "MCP Set Tile");
            tilemap.SetTile(new Vector3Int(p.x, p.y, 0), tile);
            return $"{{\"set\":true,\"x\":{p.x},\"y\":{p.y}}}";
        }

        private static string HandleTilePaint(string paramsJson)
        {
            var p = JsonUtility.FromJson<TilePaintParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.tilemapId) as GameObject;
            if (go == null) return $"{{\"error\":\"Tilemap object {p.tilemapId} not found\"}}";
            var tilemap = go.GetComponent<Tilemap>();
            if (tilemap == null) return "{\"error\":\"No Tilemap component\"}";

            var tile = AssetDatabase.LoadAssetAtPath<TileBase>(p.tilePath);
            if (tile == null) return $"{{\"error\":\"Tile not found at {p.tilePath}\"}}";

            Undo.RecordObject(tilemap, "MCP Paint Tiles");
            int count = 0;
            for (int x = Math.Min(p.x1, p.x2); x <= Math.Max(p.x1, p.x2); x++)
                for (int y = Math.Min(p.y1, p.y2); y <= Math.Max(p.y1, p.y2); y++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                    count++;
                }
            return $"{{\"painted\":true,\"tileCount\":{count}}}";
        }

        private static string HandleTilemapClear(string paramsJson)
        {
            var p = JsonUtility.FromJson<TilemapClearParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.tilemapId) as GameObject;
            if (go == null) return $"{{\"error\":\"Tilemap {p.tilemapId} not found\"}}";
            var tilemap = go.GetComponent<Tilemap>();
            if (tilemap == null) return "{\"error\":\"No Tilemap component\"}";

            Undo.RecordObject(tilemap, "MCP Clear Tilemap");
            tilemap.ClearAllTiles();
            return "{\"cleared\":true}";
        }

        private static string HandlePhysics2D(string paramsJson)
        {
            var p = JsonUtility.FromJson<Physics2DParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) rb = Undo.AddComponent<Rigidbody2D>(go);

            Undo.RecordObject(rb, "MCP 2D Physics Setup");
            if (!string.IsNullOrEmpty(p.bodyType) && Enum.TryParse<RigidbodyType2D>(p.bodyType, true, out var bt))
                rb.bodyType = bt;
            if (p.mass > 0) rb.mass = p.mass;
            if (p.gravityScale >= 0) rb.gravityScale = p.gravityScale;

            if (!string.IsNullOrEmpty(p.colliderType))
            {
                switch (p.colliderType.ToLower())
                {
                    case "box": if (!go.GetComponent<BoxCollider2D>()) Undo.AddComponent<BoxCollider2D>(go); break;
                    case "circle": if (!go.GetComponent<CircleCollider2D>()) Undo.AddComponent<CircleCollider2D>(go); break;
                    case "capsule": if (!go.GetComponent<CapsuleCollider2D>()) Undo.AddComponent<CapsuleCollider2D>(go); break;
                    case "polygon": if (!go.GetComponent<PolygonCollider2D>()) Undo.AddComponent<PolygonCollider2D>(go); break;
                }
            }

            EditorUtility.SetDirty(rb);
            return $"{{\"configured\":true,\"bodyType\":\"{rb.bodyType}\"}}";
        }

        private static string HandleSortingLayer(string paramsJson)
        {
            var p = JsonUtility.FromJson<SortingParams>(paramsJson);
            var layers = SortingLayer.layers;
            var sb = new StringBuilder("[");
            for (int i = 0; i < layers.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"name\":\"{layers[i].name}\",\"id\":{layers[i].id},\"value\":{layers[i].value}}}");
            }
            sb.Append("]");
            return $"{{\"layers\":{sb}}}";
        }
    }
}
#endif
