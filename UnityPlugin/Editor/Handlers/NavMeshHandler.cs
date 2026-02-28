#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.AI;
using UnityEngine;
using UnityEngine.AI;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles navigation tools:
    /// unity_navmesh_bake, unity_navmesh_agent_setup, unity_navmesh_obstacle_add,
    /// unity_navmesh_set_area, unity_navmesh_find_path
    /// </summary>
    public static class NavMeshHandler
    {
        [Serializable] private class AgentParams { public int instanceId; public float speed = -1; public float radius = -1; public float height = -1; public float stoppingDistance = -1; }
        [Serializable] private class ObstacleParams { public int instanceId; public int carve = 1; public float[] size; }
        [Serializable] private class AreaParams { public int areaIndex; public string areaName; public float cost = -1; }
        [Serializable] private class PathParams { public float[] start; public float[] end; public int areaMask = -1; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_navmesh_bake": return HandleBake();
                    case "unity_navmesh_agent_setup": return HandleAgent(paramsJson);
                    case "unity_navmesh_obstacle_add": return HandleObstacle(paramsJson);
                    case "unity_navmesh_set_area": return HandleArea(paramsJson);
                    case "unity_navmesh_find_path": return HandleFindPath(paramsJson);
                    default: return $"{{\"error\":\"Unknown navmesh tool: {tool}\"}}";
                }
            });
        }

        private static string HandleBake()
        {
            NavMeshBuilder.BuildNavMesh();
            return "{\"baked\":true}";
        }

        private static string HandleAgent(string paramsJson)
        {
            var p = JsonUtility.FromJson<AgentParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = Undo.AddComponent<NavMeshAgent>(go);

            Undo.RecordObject(agent, "MCP NavMeshAgent Setup");
            if (p.speed > 0) agent.speed = p.speed;
            if (p.radius > 0) agent.radius = p.radius;
            if (p.height > 0) agent.height = p.height;
            if (p.stoppingDistance >= 0) agent.stoppingDistance = p.stoppingDistance;

            EditorUtility.SetDirty(agent);
            return $"{{\"configured\":true,\"speed\":{agent.speed},\"radius\":{agent.radius}}}";
        }

        private static string HandleObstacle(string paramsJson)
        {
            var p = JsonUtility.FromJson<ObstacleParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var obstacle = go.GetComponent<NavMeshObstacle>();
            if (obstacle == null) obstacle = Undo.AddComponent<NavMeshObstacle>(go);

            Undo.RecordObject(obstacle, "MCP NavMeshObstacle");
            obstacle.carving = p.carve == 1;
            if (p.size != null && p.size.Length == 3)
                obstacle.size = new Vector3(p.size[0], p.size[1], p.size[2]);

            EditorUtility.SetDirty(obstacle);
            return $"{{\"added\":true,\"carving\":{(obstacle.carving ? "true" : "false")}}}";
        }

        private static string HandleArea(string paramsJson)
        {
            var p = JsonUtility.FromJson<AreaParams>(paramsJson);
            // NavMesh area names are set via serialized editor settings
            var areas = GameObjectUtility.GetNavMeshAreaNames();
            var sb = new StringBuilder("[");
            for (int i = 0; i < areas.Length; i++)
            {
                if (i > 0) sb.Append(",");
                var cost = NavMesh.GetAreaCost(i);
                sb.Append($"{{\"index\":{i},\"name\":\"{areas[i]}\",\"cost\":{cost}}}");
            }
            sb.Append("]");

            if (p.cost >= 0 && p.areaIndex >= 0)
                NavMesh.SetAreaCost(p.areaIndex, p.cost);

            return $"{{\"areas\":{sb}}}";
        }

        private static string HandleFindPath(string paramsJson)
        {
            var p = JsonUtility.FromJson<PathParams>(paramsJson);
            var start = new Vector3(p.start[0], p.start[1], p.start[2]);
            var end = new Vector3(p.end[0], p.end[1], p.end[2]);

            var path = new NavMeshPath();
            var found = NavMesh.CalculatePath(start, end, p.areaMask > 0 ? p.areaMask : NavMesh.AllAreas, path);

            var sb = new StringBuilder("[");
            for (int i = 0; i < path.corners.Length; i++)
            {
                if (i > 0) sb.Append(",");
                var c = path.corners[i];
                sb.Append($"[{c.x},{c.y},{c.z}]");
            }
            sb.Append("]");

            return $"{{\"found\":{(found ? "true" : "false")},\"status\":\"{path.status}\",\"corners\":{sb}}}";
        }
    }
}
#endif
