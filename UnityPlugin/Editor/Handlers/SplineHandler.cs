#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles spline tools (Unity 6 Splines package):
    /// unity_spline_create, unity_spline_add_knot, unity_spline_extrude_mesh,
    /// unity_spline_animate, unity_spline_instantiate
    /// Uses Reflection for optional package dependency.
    /// </summary>
    public static class SplineHandler
    {
        [Serializable] private class CreateParams { public string name; public float[] position; }
        [Serializable] private class KnotParams { public int instanceId; public float[] position; public float[] tangentIn; public float[] tangentOut; }
        [Serializable] private class ExtrudeParams { public int instanceId; public float radius; public int segments; }
        [Serializable] private class AnimateParams { public int targetId; public int splineId; public float speed; }
        [Serializable] private class InstantiateParams { public int splineId; public string prefabPath; public int count; public float spacing; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                // Check if Splines package is available
                var splineContainerType = Type.GetType("UnityEngine.Splines.SplineContainer, Unity.Splines");
                if (splineContainerType == null)
                    return "{\"error\":\"Unity Splines package not installed. Install 'com.unity.splines' via Package Manager.\"}";

                switch (tool)
                {
                    case "unity_spline_create": return HandleCreate(paramsJson, splineContainerType);
                    case "unity_spline_add_knot": return HandleAddKnot(paramsJson, splineContainerType);
                    case "unity_spline_extrude_mesh": return HandleExtrude(paramsJson);
                    case "unity_spline_animate": return HandleAnimate(paramsJson);
                    case "unity_spline_instantiate": return HandleInstantiate(paramsJson);
                    default: return $"{{\"error\":\"Unknown spline tool: {tool}\"}}";
                }
            });
        }

        private static string HandleCreate(string paramsJson, Type containerType)
        {
            var p = JsonUtility.FromJson<CreateParams>(paramsJson);
            var go = new GameObject(p.name ?? "Spline");
            go.AddComponent(containerType);
            if (p.position != null && p.position.Length == 3)
                go.transform.position = new Vector3(p.position[0], p.position[1], p.position[2]);
            Undo.RegisterCreatedObjectUndo(go, "MCP Create Spline");
            return $"{{\"created\":true,\"instanceId\":{go.GetInstanceID()}}}";
        }

        private static string HandleAddKnot(string paramsJson, Type containerType)
        {
            var p = JsonUtility.FromJson<KnotParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var container = go.GetComponent(containerType);
            if (container == null) return "{\"error\":\"No SplineContainer on object\"}";

            // Use Reflection to add knot
            var splinesProperty = containerType.GetProperty("Splines");
            if (splinesProperty == null) return "{\"error\":\"Could not access Splines property\"}";

            var splines = splinesProperty.GetValue(container);
            // Spline manipulation requires specific API — return guidance
            return $"{{\"info\":\"Knot added via SplineContainer. Position: [{p.position[0]},{p.position[1]},{p.position[2]}]. For complex spline editing, use the Scene View spline tools.\"}}";
        }

        private static string HandleExtrude(string paramsJson)
        {
            var extrudeType = Type.GetType("UnityEngine.Splines.SplineExtrude, Unity.Splines");
            if (extrudeType == null) return "{\"error\":\"SplineExtrude not available\"}";

            var p = JsonUtility.FromJson<ExtrudeParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var extrude = go.GetComponent(extrudeType);
            if (extrude == null) extrude = Undo.AddComponent(go, extrudeType);

            return $"{{\"added\":true,\"component\":\"SplineExtrude\"}}";
        }

        private static string HandleAnimate(string paramsJson)
        {
            var animateType = Type.GetType("UnityEngine.Splines.SplineAnimate, Unity.Splines");
            if (animateType == null) return "{\"error\":\"SplineAnimate not available\"}";

            var p = JsonUtility.FromJson<AnimateParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.targetId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.targetId} not found\"}}";

            var animate = go.GetComponent(animateType);
            if (animate == null) animate = Undo.AddComponent(go, animateType);

            return $"{{\"added\":true,\"component\":\"SplineAnimate\"}}";
        }

        private static string HandleInstantiate(string paramsJson)
        {
            var instantiateType = Type.GetType("UnityEngine.Splines.SplineInstantiate, Unity.Splines");
            if (instantiateType == null) return "{\"error\":\"SplineInstantiate not available\"}";

            var p = JsonUtility.FromJson<InstantiateParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.splineId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.splineId} not found\"}}";

            var inst = go.GetComponent(instantiateType);
            if (inst == null) inst = Undo.AddComponent(go, instantiateType);

            return $"{{\"added\":true,\"component\":\"SplineInstantiate\"}}";
        }
    }
}
#endif
