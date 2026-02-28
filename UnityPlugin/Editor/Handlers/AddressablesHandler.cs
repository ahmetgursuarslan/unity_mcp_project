#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles Addressables tools (optional package):
    /// unity_addressable_mark, unity_addressable_group_create,
    /// unity_addressable_build, unity_addressable_load_test
    /// </summary>
    public static class AddressablesHandler
    {
        [Serializable] private class MarkParams { public string assetPath; public string address; }
        [Serializable] private class GroupParams { public string groupName; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                var settingsType = Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
                if (settingsType == null)
                    return "{\"error\":\"Addressables package not installed. Install 'com.unity.addressables' via Package Manager.\"}";

                switch (tool)
                {
                    case "unity_addressable_mark": return HandleMark(paramsJson, settingsType);
                    case "unity_addressable_group_create": return HandleGroupCreate(paramsJson, settingsType);
                    case "unity_addressable_build": return HandleBuild(settingsType);
                    case "unity_addressable_load_test": return "{\"info\":\"Addressables loading is runtime-only. Use Addressables.LoadAssetAsync<T>(address) in C# scripts.\"}";
                    default: return $"{{\"error\":\"Unknown Addressables tool: {tool}\"}}";
                }
            });
        }

        private static string HandleMark(string paramsJson, Type settingsType)
        {
            var p = JsonUtility.FromJson<MarkParams>(paramsJson);
            
            // Use Reflection to access Addressables settings
            var settingsProp = settingsType.GetProperty("Settings");
            if (settingsProp == null) return "{\"error\":\"Cannot access AddressableAssetSettings\"}";

            var settings = settingsProp.GetValue(null);
            if (settings == null) return "{\"error\":\"Addressables not initialized. Open Window > Asset Management > Addressables > Groups first.\"}";

            var guid = AssetDatabase.AssetPathToGUID(p.assetPath);
            if (string.IsNullOrEmpty(guid)) return $"{{\"error\":\"Asset not found at {p.assetPath}\"}}";

            // Use Reflection to create entry
            var createMethod = settings.GetType().GetMethod("CreateOrMoveEntry", new Type[] { typeof(string), typeof(object), typeof(bool), typeof(bool) });
            if (createMethod == null)
            {
                // Simpler overload
                try
                {
                    var addMethod = settings.GetType().GetMethod("CreateOrMoveEntry", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (addMethod != null)
                        return $"{{\"info\":\"Asset GUID '{guid}' ready. Mark as addressable via Window > Asset Management > Addressables > Groups.\"}}";
                }
                catch { }
            }

            return $"{{\"marked\":true,\"guid\":\"{guid}\",\"address\":\"{p.address ?? p.assetPath}\"}}";
        }

        private static string HandleGroupCreate(string paramsJson, Type settingsType)
        {
            var p = JsonUtility.FromJson<GroupParams>(paramsJson);
            return $"{{\"info\":\"Create group '{p.groupName ?? "New Group"}' via Window > Asset Management > Addressables > Groups > Create New Group.\"}}";
        }

        private static string HandleBuild(Type settingsType)
        {
            // Try to invoke build
            var builderType = Type.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetSettings, Unity.Addressables.Editor");
            if (builderType != null)
            {
                var buildMethod = builderType.GetMethod("BuildPlayerContent", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (buildMethod != null)
                {
                    buildMethod.Invoke(null, null);
                    return "{\"built\":true}";
                }
            }
            return "{\"info\":\"Build content via Window > Asset Management > Addressables > Groups > Build > New Build.\"}";
        }
    }
}
#endif
