#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles LOD, occlusion culling and performance optimization tools:
    /// unity_lod_group_setup, unity_occlusion_bake, unity_static_flags_set,
    /// unity_gpu_instancing_enable, unity_profiler_capture, unity_memory_snapshot
    /// </summary>
    public static class LODPerformanceHandler
    {
        [Serializable] private class LODParams { public int instanceId; public float[] thresholds; }
        [Serializable] private class StaticParams { public int instanceId; public string flags; }
        [Serializable] private class InstancingParams { public string materialPath; public int enable = 1; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_lod_group_setup": return HandleLOD(paramsJson);
                    case "unity_occlusion_bake": return HandleOcclusionBake();
                    case "unity_static_flags_set": return HandleStaticFlags(paramsJson);
                    case "unity_gpu_instancing_enable": return HandleInstancing(paramsJson);
                    case "unity_profiler_capture": return HandleProfiler();
                    case "unity_memory_snapshot": return HandleMemory();
                    default: return $"{{\"error\":\"Unknown LOD/perf tool: {tool}\"}}";
                }
            });
        }

        private static string HandleLOD(string paramsJson)
        {
            var p = JsonUtility.FromJson<LODParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var lodGroup = go.GetComponent<LODGroup>();
            if (lodGroup == null) lodGroup = Undo.AddComponent<LODGroup>(go);

            if (p.thresholds != null && p.thresholds.Length > 0)
            {
                var lods = new LOD[p.thresholds.Length];
                var renderers = go.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < p.thresholds.Length; i++)
                {
                    lods[i] = new LOD(p.thresholds[i], i == 0 ? renderers : new Renderer[0]);
                }
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
            }

            return $"{{\"configured\":true,\"lodCount\":{lodGroup.lodCount}}}";
        }

        private static string HandleOcclusionBake()
        {
            StaticOcclusionCulling.Compute();
            return $"{{\"baking\":true,\"isRunning\":{(StaticOcclusionCulling.isRunning ? "true" : "false")}}}";
        }

        private static string HandleStaticFlags(string paramsJson)
        {
            var p = JsonUtility.FromJson<StaticParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            StaticEditorFlags flags = 0;
            if (!string.IsNullOrEmpty(p.flags))
            {
                var parts = p.flags.Split(',');
                foreach (var part in parts)
                {
                    if (Enum.TryParse<StaticEditorFlags>(part.Trim(), true, out var f))
                        flags |= f;
                }
            }
            else
            {
                flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.OccluderStatic;
            }

            GameObjectUtility.SetStaticEditorFlags(go, flags);
            return $"{{\"set\":true,\"flags\":\"{flags}\"}}";
        }

        private static string HandleInstancing(string paramsJson)
        {
            var p = JsonUtility.FromJson<InstancingParams>(paramsJson);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(p.materialPath);
            if (mat == null) return $"{{\"error\":\"Material not found at {p.materialPath}\"}}";

            mat.enableInstancing = p.enable == 1;
            EditorUtility.SetDirty(mat);
            return $"{{\"instancing\":{(mat.enableInstancing ? "true" : "false")}}}";
        }

        private static string HandleProfiler()
        {
            return $"{{\"totalAllocatedMemory\":{UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()}" +
                   $",\"totalReservedMemory\":{UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong()}" +
                   $",\"monoUsedSize\":{UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()}" +
                   $",\"monoHeapSize\":{UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong()}}}";
        }

        private static string HandleMemory()
        {
            var totalMem = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            var gfxMem = UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver();
            return $"{{\"totalAllocated\":{totalMem},\"graphicsDriver\":{gfxMem}" +
                   $",\"tempAllocator\":{UnityEngine.Profiling.Profiler.GetTempAllocatorSize()}}}";
        }
    }
}
#endif
