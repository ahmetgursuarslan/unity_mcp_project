#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles physics tools:
    /// unity_physics_raycast, unity_physics_overlap, unity_physics_settings,
    /// unity_physics_set_collision_matrix
    /// </summary>
    public static class PhysicsHandler
    {
        [Serializable] private class RaycastParams { public float[] origin; public float[] direction; public float maxDistance; public int layerMask = -1; }
        [Serializable] private class OverlapParams { public float[] center; public float radius; public string shape; public int layerMask = -1; }
        [Serializable] private class SettingsParams { public float[] gravity; public int solverIterations = -1; public int solverVelocityIterations = -1; public float bounceThreshold = -1; }
        [Serializable] private class CollisionParams { public int layer1; public int layer2; public int collide = 1; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_physics_raycast": return HandleRaycast(paramsJson);
                    case "unity_physics_overlap": return HandleOverlap(paramsJson);
                    case "unity_physics_settings": return HandleSettings(paramsJson);
                    case "unity_physics_set_collision_matrix": return HandleCollisionMatrix(paramsJson);
                    default: return $"{{\"error\":\"Unknown physics tool: {tool}\"}}";
                }
            });
        }

        private static string HandleRaycast(string paramsJson)
        {
            var p = JsonUtility.FromJson<RaycastParams>(paramsJson);
            var origin = new Vector3(p.origin[0], p.origin[1], p.origin[2]);
            var direction = new Vector3(p.direction[0], p.direction[1], p.direction[2]);
            var maxDist = p.maxDistance > 0 ? p.maxDistance : 1000f;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDist, p.layerMask))
            {
                return $"{{\"hit\":true,\"point\":[{hit.point.x},{hit.point.y},{hit.point.z}]" +
                       $",\"normal\":[{hit.normal.x},{hit.normal.y},{hit.normal.z}]" +
                       $",\"distance\":{hit.distance}" +
                       $",\"objectName\":\"{hit.collider.gameObject.name}\"" +
                       $",\"instanceId\":{hit.collider.gameObject.GetInstanceID()}}}";
            }
            return "{\"hit\":false}";
        }

        private static string HandleOverlap(string paramsJson)
        {
            var p = JsonUtility.FromJson<OverlapParams>(paramsJson);
            var center = new Vector3(p.center[0], p.center[1], p.center[2]);
            var colliders = Physics.OverlapSphere(center, p.radius, p.layerMask);

            var sb = new StringBuilder("[");
            for (int i = 0; i < colliders.Length && i < 50; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"name\":\"{colliders[i].gameObject.name}\",\"instanceId\":{colliders[i].gameObject.GetInstanceID()}}}");
            }
            sb.Append("]");
            return $"{{\"count\":{colliders.Length},\"results\":{sb}}}";
        }

        private static string HandleSettings(string paramsJson)
        {
            var p = JsonUtility.FromJson<SettingsParams>(paramsJson);
            if (p.gravity != null && p.gravity.Length == 3)
                Physics.gravity = new Vector3(p.gravity[0], p.gravity[1], p.gravity[2]);
            if (p.solverIterations > 0)
                Physics.defaultSolverIterations = p.solverIterations;
            if (p.solverVelocityIterations > 0)
                Physics.defaultSolverVelocityIterations = p.solverVelocityIterations;
            if (p.bounceThreshold >= 0)
                Physics.bounceThreshold = p.bounceThreshold;

            return $"{{\"gravity\":[{Physics.gravity.x},{Physics.gravity.y},{Physics.gravity.z}]" +
                   $",\"solverIterations\":{Physics.defaultSolverIterations}" +
                   $",\"bounceThreshold\":{Physics.bounceThreshold}}}";
        }

        private static string HandleCollisionMatrix(string paramsJson)
        {
            var p = JsonUtility.FromJson<CollisionParams>(paramsJson);
            Physics.IgnoreLayerCollision(p.layer1, p.layer2, p.collide == 0);
            return $"{{\"set\":true,\"layer1\":{p.layer1},\"layer2\":{p.layer2},\"collide\":{(p.collide == 1 ? "true" : "false")}}}";
        }
    }
}
#endif
