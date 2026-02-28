#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles multiplayer/netcode tools (optional package):
    /// unity_netcode_setup, unity_network_object_create, unity_network_variable_add,
    /// unity_network_rpc_define, unity_multiplayer_test
    /// </summary>
    public static class NetcodeHandler
    {
        [Serializable] private class SetupParams { public int tickRate = 30; }
        [Serializable] private class NetObjParams { public int instanceId; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                var nmType = Type.GetType("Unity.Netcode.NetworkManager, Unity.Netcode.Runtime");
                if (nmType == null)
                    return "{\"error\":\"Netcode for GameObjects not installed. Install 'com.unity.netcode.gameobjects' via Package Manager.\"}";

                switch (tool)
                {
                    case "unity_netcode_setup": return HandleSetup(paramsJson, nmType);
                    case "unity_network_object_create": return HandleNetObject(paramsJson);
                    case "unity_network_variable_add": return "{\"info\":\"NetworkVariables are defined in C# scripts. Use script generation to create NetworkBehaviour classes.\"}";
                    case "unity_network_rpc_define": return "{\"info\":\"RPCs are defined as C# methods with [ServerRpc]/[ClientRpc] attributes. Use script generation tools.\"}";
                    case "unity_multiplayer_test": return "{\"info\":\"Use ParrelSync or Multiplayer Play Mode for testing. Install via Package Manager.\"}";
                    default: return $"{{\"error\":\"Unknown netcode tool: {tool}\"}}";
                }
            });
        }

        private static string HandleSetup(string paramsJson, Type nmType)
        {
            var p = JsonUtility.FromJson<SetupParams>(paramsJson);
            var go = GameObject.Find("NetworkManager");
            if (go == null)
            {
                go = new GameObject("NetworkManager");
                Undo.RegisterCreatedObjectUndo(go, "MCP Create NetworkManager");
            }
            
            var nm = go.GetComponent(nmType);
            if (nm == null) nm = Undo.AddComponent(go, nmType);

            return $"{{\"created\":true,\"instanceId\":{go.GetInstanceID()}}}";
        }

        private static string HandleNetObject(string paramsJson)
        {
            var p = JsonUtility.FromJson<NetObjParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var noType = Type.GetType("Unity.Netcode.NetworkObject, Unity.Netcode.Runtime");
            if (noType == null) return "{\"error\":\"Netcode not installed\"}";

            var comp = go.GetComponent(noType);
            if (comp == null) comp = Undo.AddComponent(go, noType);

            return $"{{\"added\":true,\"instanceId\":{go.GetInstanceID()}}}";
        }
    }
}
#endif
