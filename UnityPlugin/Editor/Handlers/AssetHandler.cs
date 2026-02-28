#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles asset management tools:
    /// unity_asset_import, unity_asset_move, unity_asset_delete, unity_asset_find,
    /// unity_asset_get_dependencies, unity_asset_set_labels, unity_scriptable_object_create
    /// </summary>
    public static class AssetHandler
    {
        [Serializable] private class ImportParams { public string sourcePath; public string destinationPath; }
        [Serializable] private class MoveParams { public string oldPath; public string newPath; }
        [Serializable] private class DeleteParams { public string assetPath; }
        [Serializable] private class FindParams { public string filter; public string type; public string searchFolder; }
        [Serializable] private class DepsParams { public string assetPath; }
        [Serializable] private class LabelsParams { public string assetPath; public string[] labels; }
        [Serializable] private class SOParams { public string typeName; public string savePath; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_asset_import": return HandleImport(paramsJson);
                    case "unity_asset_move": return HandleMove(paramsJson);
                    case "unity_asset_delete": return HandleDelete(paramsJson);
                    case "unity_asset_find": return HandleFind(paramsJson);
                    case "unity_asset_get_dependencies": return HandleDeps(paramsJson);
                    case "unity_asset_set_labels": return HandleLabels(paramsJson);
                    case "unity_scriptable_object_create": return HandleSOCreate(paramsJson);
                    default: return $"{{\"error\":\"Unknown asset tool: {tool}\"}}";
                }
            });
        }

        private static string HandleImport(string paramsJson)
        {
            var p = JsonUtility.FromJson<ImportParams>(paramsJson);
            SecurityGuard.ValidatePath(p.destinationPath);

            var dir = System.IO.Path.GetDirectoryName(p.destinationPath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            System.IO.File.Copy(p.sourcePath, p.destinationPath, true);
            AssetDatabase.ImportAsset(p.destinationPath);
            return $"{{\"imported\":true,\"path\":\"{p.destinationPath}\"}}";
        }

        private static string HandleMove(string paramsJson)
        {
            var p = JsonUtility.FromJson<MoveParams>(paramsJson);
            SecurityGuard.ValidatePath(p.newPath);
            var result = AssetDatabase.MoveAsset(p.oldPath, p.newPath);
            return string.IsNullOrEmpty(result)
                ? $"{{\"moved\":true,\"newPath\":\"{p.newPath}\"}}"
                : $"{{\"error\":\"{result}\"}}";
        }

        private static string HandleDelete(string paramsJson)
        {
            var p = JsonUtility.FromJson<DeleteParams>(paramsJson);
            SecurityGuard.ValidatePath(p.assetPath);
            var deleted = AssetDatabase.DeleteAsset(p.assetPath);
            return $"{{\"deleted\":{(deleted ? "true" : "false")}}}";
        }

        private static string HandleFind(string paramsJson)
        {
            var p = JsonUtility.FromJson<FindParams>(paramsJson);
            var filter = p.filter ?? "";
            if (!string.IsNullOrEmpty(p.type))
                filter = $"t:{p.type} {filter}".Trim();

            var folders = !string.IsNullOrEmpty(p.searchFolder)
                ? new[] { p.searchFolder }
                : new[] { "Assets" };

            var guids = AssetDatabase.FindAssets(filter, folders);
            var sb = new StringBuilder("[");
            var count = Math.Min(guids.Length, 50);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) sb.Append(",");
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                sb.Append($"{{\"guid\":\"{guids[i]}\",\"path\":\"{path}\",\"type\":\"{assetType?.Name ?? "Unknown"}\"}}");
            }
            sb.Append("]");
            return $"{{\"count\":{guids.Length},\"results\":{sb}}}";
        }

        private static string HandleDeps(string paramsJson)
        {
            var p = JsonUtility.FromJson<DepsParams>(paramsJson);
            var deps = AssetDatabase.GetDependencies(p.assetPath, true);
            var sb = new StringBuilder("[");
            for (int i = 0; i < deps.Length; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"\"{deps[i]}\"");
            }
            sb.Append("]");
            return $"{{\"assetPath\":\"{p.assetPath}\",\"dependencies\":{sb}}}";
        }

        private static string HandleLabels(string paramsJson)
        {
            var p = JsonUtility.FromJson<LabelsParams>(paramsJson);
            var asset = AssetDatabase.LoadMainAssetAtPath(p.assetPath);
            if (asset == null) return $"{{\"error\":\"Asset not found at {p.assetPath}\"}}";

            AssetDatabase.SetLabels(asset, p.labels ?? new string[0]);
            return $"{{\"labelsSet\":true,\"count\":{(p.labels?.Length ?? 0)}}}";
        }

        private static string HandleSOCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<SOParams>(paramsJson);
            SecurityGuard.ValidatePath(p.savePath);

            var type = GameObjectHandler.ResolveUnityType(p.typeName);
            if (type == null || !typeof(ScriptableObject).IsAssignableFrom(type))
                return $"{{\"error\":\"Type '{p.typeName}' is not a ScriptableObject\"}}";

            var so = ScriptableObject.CreateInstance(type);
            var dir = System.IO.Path.GetDirectoryName(p.savePath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(so, p.savePath);
            AssetDatabase.SaveAssets();
            return $"{{\"created\":true,\"path\":\"{p.savePath}\",\"type\":\"{type.Name}\"}}";
        }
    }
}
#endif
