#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles asset import settings tools:
    /// unity_texture_import_settings, unity_model_import_settings,
    /// unity_audio_import_settings, unity_asset_postprocessor_add
    /// </summary>
    public static class ImportSettingsHandler
    {
        [Serializable] private class TexImportParams
        {
            public string assetPath;
            public int maxSize = -1;
            public string compression;
            public string filterMode;
            public int generateMipMaps = -1;
            public string textureType;
        }
        [Serializable] private class ModelImportParams
        {
            public string assetPath;
            public float scaleFactor = -1;
            public int importNormals = -1;
            public int importAnimation = -1;
            public string animationType;
        }
        [Serializable] private class AudioImportParams
        {
            public string assetPath;
            public string loadType;
            public string compressionFormat;
            public float quality = -1;
            public int forceToMono = -1;
        }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_texture_import_settings": return HandleTextureImport(paramsJson);
                    case "unity_model_import_settings": return HandleModelImport(paramsJson);
                    case "unity_audio_import_settings": return HandleAudioImport(paramsJson);
                    case "unity_asset_postprocessor_add": return "{\"error\":\"AssetPostprocessor requires C# script generation — use file write tools instead\"}";
                    default: return $"{{\"error\":\"Unknown import tool: {tool}\"}}";
                }
            });
        }

        private static string HandleTextureImport(string paramsJson)
        {
            var p = JsonUtility.FromJson<TexImportParams>(paramsJson);
            var importer = AssetImporter.GetAtPath(p.assetPath) as TextureImporter;
            if (importer == null) return $"{{\"error\":\"No TextureImporter at {p.assetPath}\"}}";

            if (p.maxSize > 0) importer.maxTextureSize = p.maxSize;
            if (!string.IsNullOrEmpty(p.compression))
            {
                if (Enum.TryParse<TextureImporterCompression>(p.compression, true, out var comp))
                    importer.textureCompression = comp;
            }
            if (!string.IsNullOrEmpty(p.filterMode))
            {
                if (Enum.TryParse<FilterMode>(p.filterMode, true, out var fm))
                    importer.filterMode = fm;
            }
            if (p.generateMipMaps >= 0) importer.mipmapEnabled = p.generateMipMaps == 1;
            if (!string.IsNullOrEmpty(p.textureType))
            {
                if (Enum.TryParse<TextureImporterType>(p.textureType, true, out var tt))
                    importer.textureType = tt;
            }

            importer.SaveAndReimport();
            return $"{{\"updated\":true,\"path\":\"{p.assetPath}\"}}";
        }

        private static string HandleModelImport(string paramsJson)
        {
            var p = JsonUtility.FromJson<ModelImportParams>(paramsJson);
            var importer = AssetImporter.GetAtPath(p.assetPath) as ModelImporter;
            if (importer == null) return $"{{\"error\":\"No ModelImporter at {p.assetPath}\"}}";

            if (p.scaleFactor > 0) importer.globalScale = p.scaleFactor;
            if (p.importNormals >= 0)
                importer.importNormals = p.importNormals == 1
                    ? ModelImporterNormals.Import
                    : ModelImporterNormals.None;
            if (p.importAnimation >= 0) importer.importAnimation = p.importAnimation == 1;
            if (!string.IsNullOrEmpty(p.animationType))
            {
                if (Enum.TryParse<ModelImporterAnimationType>(p.animationType, true, out var at))
                    importer.animationType = at;
            }

            importer.SaveAndReimport();
            return $"{{\"updated\":true,\"path\":\"{p.assetPath}\"}}";
        }

        private static string HandleAudioImport(string paramsJson)
        {
            var p = JsonUtility.FromJson<AudioImportParams>(paramsJson);
            var importer = AssetImporter.GetAtPath(p.assetPath) as AudioImporter;
            if (importer == null) return $"{{\"error\":\"No AudioImporter at {p.assetPath}\"}}";

            var settings = importer.defaultSampleSettings;
            if (!string.IsNullOrEmpty(p.loadType))
            {
                if (Enum.TryParse<AudioClipLoadType>(p.loadType, true, out var lt))
                    settings.loadType = lt;
            }
            if (!string.IsNullOrEmpty(p.compressionFormat))
            {
                if (Enum.TryParse<AudioCompressionFormat>(p.compressionFormat, true, out var cf))
                    settings.compressionFormat = cf;
            }
            if (p.quality >= 0) settings.quality = Mathf.Clamp01(p.quality);
            importer.defaultSampleSettings = settings;

            if (p.forceToMono >= 0) importer.forceToMono = p.forceToMono == 1;

            importer.SaveAndReimport();
            return $"{{\"updated\":true,\"path\":\"{p.assetPath}\"}}";
        }
    }
}
#endif
