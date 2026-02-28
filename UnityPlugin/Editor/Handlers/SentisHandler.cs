#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles Unity Sentis ML inference tools (optional package):
    /// unity_sentis_load_model, unity_sentis_run_inference,
    /// unity_sentis_get_output, unity_sentis_set_backend
    /// </summary>
    public static class SentisHandler
    {
        [Serializable] private class LoadParams { public string modelPath; public int instanceId; }
        [Serializable] private class BackendParams { public string backend; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                var sentisType = Type.GetType("Unity.Sentis.ModelLoader, Unity.Sentis");
                if (sentisType == null)
                    return "{\"error\":\"Unity Sentis not installed. Install 'com.unity.sentis' via Package Manager.\"}";

                switch (tool)
                {
                    case "unity_sentis_load_model": return HandleLoad(paramsJson);
                    case "unity_sentis_run_inference": return "{\"info\":\"Inference is a runtime operation. Create a C# script using Worker.Schedule() and Worker.PeekOutput().\"}";
                    case "unity_sentis_get_output": return "{\"info\":\"Output reading is runtime-only. Use tensor.ToReadOnlyArray() in C# scripts.\"}";
                    case "unity_sentis_set_backend": return HandleBackend(paramsJson);
                    default: return $"{{\"error\":\"Unknown Sentis tool: {tool}\"}}";
                }
            });
        }

        private static string HandleLoad(string paramsJson)
        {
            var p = JsonUtility.FromJson<LoadParams>(paramsJson);
            if (string.IsNullOrEmpty(p.modelPath))
                return "{\"error\":\"modelPath is required (ONNX file path)\"}";

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p.modelPath);
            if (asset == null) return $"{{\"error\":\"Model not found at {p.modelPath}\"}}";

            return $"{{\"loaded\":true,\"path\":\"{p.modelPath}\",\"type\":\"{asset.GetType().Name}\"" +
                   $",\"info\":\"Model asset loaded. Create a C# script with ModelLoader.Load() and Worker for inference.\"}}";
        }

        private static string HandleBackend(string paramsJson)
        {
            var p = JsonUtility.FromJson<BackendParams>(paramsJson);
            return $"{{\"info\":\"Backend '{p.backend ?? "GPUCompute"}' is set in code: new Worker(model, BackendType.{p.backend ?? "GPUCompute"}). Available: CPU, GPUCompute, GPUPixel.\"}}";
        }
    }
}
#endif
