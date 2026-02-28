#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Phase 2: AI Autonomy Tools
    /// Tools that allow the AI to self-diagnose and fix project errors.
    /// Includes: compiler errors getter, missing reference detector, asset dependency finder.
    /// </summary>
    public static class DeveloperToolsHandler
    {
        [Serializable] private class FindDependenciesParams { public string assetPath; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_dev_get_compile_errors": return HandleGetCompileErrors();
                    case "unity_dev_find_missing_references": return HandleFindMissingReferences();
                    case "unity_dev_find_asset_dependencies": return HandleFindAssetDependencies(paramsJson);
                    default: return ResponseHelper.Error($"Unknown dev tool: {tool}");
                }
            });
        }

        // 1. Get Compilation Errors
        private static string HandleGetCompileErrors()
        {
            if (!EditorUtility.scriptCompilationFailed)
            {
                return ResponseHelper.Ok(
                    JsonHelper.Bool("hasErrors", false),
                    JsonHelper.Arr("errors"));
            }

            // In Unity 2021+, compiler messages can be extracted from the console
            // We use an internal API reflection trick to get the exact file and line number
            var consoleEntries = new List<string>();
            try
            {
                var type = Type.GetType("UnityEditor.LogEntries, UnityEditor");
                if (type != null)
                {
                    var startMethod = type.GetMethod("StartGettingEntries", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var getMethod = type.GetMethod("GetEntryInternal", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var endMethod = type.GetMethod("EndGettingEntries", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var getCountMethod = type.GetMethod("GetCount", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    // We need to create an instance of LogEntry
                    var entryType = Type.GetType("UnityEditor.LogEntry, UnityEditor");
                    var entry = Activator.CreateInstance(entryType);
                    
                    // Mode field of LogEntry (Error = 1, Warning = 2, Log = 3, Assert = 0, Exception = 4, etc)
                    var conditionField = entryType.GetField("condition");
                    var errorNumField = entryType.GetField("errorNum");
                    var fileField = entryType.GetField("file");
                    var lineField = entryType.GetField("line");
                    var modeField = entryType.GetField("mode");

                    int count = (int)getCountMethod.Invoke(null, null);
                    startMethod.Invoke(null, null);

                    for (int i = 0; i < count; i++)
                    {
                        var args = new object[] { i, entry };
                        if ((bool)getMethod.Invoke(null, args))
                        {
                            int mode = (int)modeField.GetValue(entry);
                            // 1 = Error, 4 = Exception, 8 = Fatal
                            if (mode == 1 || mode == 4 || mode == 8)
                            {
                                string file = (string)fileField.GetValue(entry);
                                int line = (int)lineField.GetValue(entry);
                                string condition = (string)conditionField.GetValue(entry);
                                
                                consoleEntries.Add(JsonHelper.Obj(
                                    JsonHelper.Str("file", file ?? ""),
                                    JsonHelper.Num("line", line),
                                    JsonHelper.Str("message", condition ?? "")
                                ));
                            }
                        }
                    }
                    endMethod.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                return ResponseHelper.Error($"Failed to read console logs: {ex.Message}");
            }

            return ResponseHelper.Ok(
                JsonHelper.Bool("hasErrors", true),
                $"\"errors\":{JsonHelper.Arr(consoleEntries.ToArray())}");
        }

        // 2. Find Missing References in Active Scene
        private static string HandleFindMissingReferences()
        {
            var missingList = new List<string>();
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);

            foreach (var go in allObjects)
            {
                var components = go.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    var comp = components[i];
                    
                    // Check for missing script (Component is null)
                    if (comp == null)
                    {
                        missingList.Add(JsonHelper.Obj(
                            JsonHelper.Str("type", "MissingScript"),
                            JsonHelper.Num("instanceId", go.GetInstanceID()),
                            JsonHelper.Str("gameObjectName", go.name),
                            JsonHelper.Str("hierarchyPath", GetHierarchyPath(go.transform))
                        ));
                        continue;
                    }

                    // Check for missing references inside the component using SerializedObject
                    var so = new SerializedObject(comp);
                    var sp = so.GetIterator();
                    while (sp.NextVisible(true))
                    {
                        if (sp.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            if (sp.objectReferenceValue == null && sp.objectReferenceInstanceIDValue != 0)
                            {
                                missingList.Add(JsonHelper.Obj(
                                    JsonHelper.Str("type", "MissingReference"),
                                    JsonHelper.Num("instanceId", go.GetInstanceID()),
                                    JsonHelper.Str("gameObjectName", go.name),
                                    JsonHelper.Str("hierarchyPath", GetHierarchyPath(go.transform)),
                                    JsonHelper.Str("component", comp.GetType().Name),
                                    JsonHelper.Str("property", sp.name)
                                ));
                            }
                        }
                    }
                }
            }

            return ResponseHelper.Ok(
                JsonHelper.Num("count", missingList.Count),
                $"\"missing\":{JsonHelper.Arr(missingList.ToArray())}");
        }

        // 3. Find Asset Dependencies
        private static string HandleFindAssetDependencies(string paramsJson)
        {
            var p = JsonUtility.FromJson<FindDependenciesParams>(paramsJson);
            if (string.IsNullOrEmpty(p.assetPath))
                return ResponseHelper.Error("assetPath is required");

            SecurityGuard.ValidatePath(p.assetPath);

            var dependencies = AssetDatabase.GetDependencies(p.assetPath, false); // Direct dependencies
            var usageList = new List<string>();

            // Find where this asset is used across the project
            // This is an expensive operation, so we limit it to scenes and prefabs
            var guids = AssetDatabase.FindAssets("t:Scene t:Prefab t:Material");
            foreach (var guid in guids)
            {
                var targetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (targetPath == p.assetPath) continue;

                var deps = AssetDatabase.GetDependencies(targetPath, true); // Recursive dependencies
                if (Array.IndexOf(deps, p.assetPath) >= 0)
                {
                    usageList.Add(JsonHelper.Str(targetPath));
                }
            }

            var escapedDeps = new string[dependencies.Length];
            for (int i = 0; i < dependencies.Length; i++)
                escapedDeps[i] = JsonHelper.Str(dependencies[i]);

            return ResponseHelper.Ok(
                $"\"dependencies\":{JsonHelper.Arr(escapedDeps)}",
                $"\"usedBy\":{JsonHelper.Arr(usageList.ToArray())}");
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform.parent == null)
                return transform.name;
            return GetHierarchyPath(transform.parent) + "/" + transform.name;
        }

        // Helper to wrap Str properly without keys for arrays
        private static string JsonStr(string v) => $"\"{JsonHelper.Escape(v)}\"";
    }
}
#endif
