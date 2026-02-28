#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles ECS/DOTS tools (optional package):
    /// unity_ecs_create_world, unity_ecs_create_entity, unity_ecs_add_system,
    /// unity_ecs_query, unity_ecs_subscene_create
    /// </summary>
    public static class ECSHandler
    {
        [Serializable] private class SubSceneParams { public string name; public string scenePath; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                var worldType = Type.GetType("Unity.Entities.World, Unity.Entities");
                if (worldType == null)
                    return "{\"error\":\"Entities package not installed. Install 'com.unity.entities' via Package Manager.\"}";

                switch (tool)
                {
                    case "unity_ecs_create_world": return "{\"info\":\"Default World is created automatically. Use SystemBase/ISystem to interact with it.\"}";
                    case "unity_ecs_create_entity": return "{\"info\":\"Entities are created via EntityManager in code. Use script generation to create IComponentData and SystemBase classes.\"}";
                    case "unity_ecs_add_system": return "{\"info\":\"Systems are registered via C# attributes [UpdateInGroup]. Use script generation tools.\"}";
                    case "unity_ecs_query": return "{\"info\":\"EntityQuery runs in SystemBase.OnUpdate(). Use script generation for query creation.\"}";
                    case "unity_ecs_subscene_create": return HandleSubScene(paramsJson);
                    default: return $"{{\"error\":\"Unknown ECS tool: {tool}\"}}";
                }
            });
        }

        private static string HandleSubScene(string paramsJson)
        {
            var p = JsonUtility.FromJson<SubSceneParams>(paramsJson);
            var subSceneType = Type.GetType("Unity.Scenes.SubScene, Unity.Scenes");
            if (subSceneType == null) return "{\"error\":\"SubScene type not found\"}";

            var go = new GameObject(p.name ?? "SubScene");
            go.AddComponent(subSceneType);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create SubScene");
            return $"{{\"created\":true,\"instanceId\":{go.GetInstanceID()}}}";
        }
    }
}
#endif
