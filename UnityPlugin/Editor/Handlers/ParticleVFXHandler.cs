#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles particle and VFX tools:
    /// unity_particle_create, unity_particle_set_module, unity_particle_play_stop,
    /// unity_vfx_graph_create
    /// </summary>
    public static class ParticleVFXHandler
    {
        [Serializable] private class CreateParams { public string name; public string preset; public float[] position; }
        [Serializable] private class ModuleParams { public int instanceId; public string module; }
        [Serializable] private class PlayParams { public int instanceId; public string action; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_particle_create": return HandleCreate(paramsJson);
                    case "unity_particle_set_module": return HandleModule(paramsJson);
                    case "unity_particle_play_stop": return HandlePlay(paramsJson);
                    case "unity_vfx_graph_create": return HandleVFX();
                    default: return $"{{\"error\":\"Unknown VFX tool: {tool}\"}}";
                }
            });
        }

        private static string HandleCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateParams>(paramsJson);
            var go = new GameObject(p.name ?? "Particle System");
            var ps = go.AddComponent<ParticleSystem>();
            if (p.position != null && p.position.Length == 3)
                go.transform.position = new Vector3(p.position[0], p.position[1], p.position[2]);

            // Apply presets
            var main = ps.main;
            switch ((p.preset ?? "").ToLower())
            {
                case "fire":
                    main.startLifetime = 1.5f;
                    main.startSpeed = 3f;
                    main.startSize = 0.5f;
                    main.startColor = new Color(1f, 0.5f, 0f);
                    var em = ps.emission;
                    em.rateOverTime = 50;
                    break;
                case "smoke":
                    main.startLifetime = 4f;
                    main.startSpeed = 1f;
                    main.startSize = 2f;
                    main.startColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    break;
                case "sparks":
                    main.startLifetime = 0.5f;
                    main.startSpeed = 8f;
                    main.startSize = 0.1f;
                    main.startColor = new Color(1f, 0.9f, 0.3f);
                    main.gravityModifier = 1f;
                    break;
            }

            Undo.RegisterCreatedObjectUndo(go, "MCP Create Particle System");
            return $"{{\"instanceId\":{go.GetInstanceID()},\"preset\":\"{p.preset ?? "default"}\"}}";
        }

        private static string HandleModule(string paramsJson)
        {
            var p = JsonUtility.FromJson<ModuleParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) return "{\"error\":\"No ParticleSystem on object\"}";

            // Module configuration is best done via Reflection through component_update
            return "{\"info\":\"Use unity_component_update with Reflection to set specific ParticleSystem module properties.\"}";
        }

        private static string HandlePlay(string paramsJson)
        {
            var p = JsonUtility.FromJson<PlayParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";
            var ps = go.GetComponent<ParticleSystem>();
            if (ps == null) return "{\"error\":\"No ParticleSystem\"}";

            switch ((p.action ?? "play").ToLower())
            {
                case "play": ps.Play(); break;
                case "stop": ps.Stop(); break;
                case "pause": ps.Pause(); break;
                case "clear": ps.Clear(); break;
            }
            return $"{{\"action\":\"{p.action}\",\"isPlaying\":{(ps.isPlaying ? "true" : "false")}}}";
        }

        private static string HandleVFX()
        {
            var vfxType = Type.GetType("UnityEngine.VFX.VisualEffect, Unity.VisualEffectGraph.Runtime");
            if (vfxType == null)
                return "{\"error\":\"VFX Graph package not installed. Install 'com.unity.visualeffectgraph' via Package Manager.\"}";

            var go = new GameObject("VFX Graph");
            go.AddComponent(vfxType);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create VFX");
            return $"{{\"created\":true,\"instanceId\":{go.GetInstanceID()}}}";
        }
    }
}
#endif
