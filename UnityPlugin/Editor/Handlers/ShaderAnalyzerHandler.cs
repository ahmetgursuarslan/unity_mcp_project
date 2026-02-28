#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Phase 2: Shader & Material Analysis
    /// Exposes material and shader properties to the AI so it can 
    /// understand visual states, colors, and tweak graphics parameters.
    /// </summary>
    public static class ShaderAnalyzerHandler
    {
        [Serializable] private class PropertiesParams { public int materialInstanceId; public string materialPath; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_shader_get_properties": return HandleGetProperties(paramsJson);
                    default: return ResponseHelper.Error($"Unknown shader tool: {tool}");
                }
            });
        }

        private static string HandleGetProperties(string paramsJson)
        {
            var p = JsonUtility.FromJson<PropertiesParams>(paramsJson);
            Material mat = null;

            if (p.materialInstanceId != 0)
            {
                mat = EditorUtility.InstanceIDToObject(p.materialInstanceId) as Material;
            }
            else if (!string.IsNullOrEmpty(p.materialPath))
            {
                SecurityGuard.ValidatePath(p.materialPath);
                mat = AssetDatabase.LoadAssetAtPath<Material>(p.materialPath);
            }

            if (mat == null)
                return ResponseHelper.Error("Material not found. Provide materialInstanceId or materialPath.");

            var shader = mat.shader;
            if (shader == null)
                return ResponseHelper.Error("Material operates without a shader.");

            var properties = new List<string>();
            int propCount = ShaderUtil.GetPropertyCount(shader);

            for (int i = 0; i < propCount; i++)
            {
                string propName = ShaderUtil.GetPropertyName(shader, i);
                string propDesc = ShaderUtil.GetPropertyDescription(shader, i);
                var propType = ShaderUtil.GetPropertyType(shader, i);

                string valStr = "null";
                if (mat.HasProperty(propName))
                {
                    switch (propType)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            var c = mat.GetColor(propName);
                            valStr = $"\"rgba({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})\"";
                            break;
                        case ShaderUtil.ShaderPropertyType.Vector:
                            var v = mat.GetVector(propName);
                            valStr = $"\"({v.x:F2},{v.y:F2},{v.z:F2},{v.w:F2})\"";
                            break;
                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            valStr = mat.GetFloat(propName).ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
                            break;
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            var t = mat.GetTexture(propName);
                            valStr = t != null ? $"\"{JsonHelper.Escape(t.name)}\"" : "\"None\"";
                            break;
                    }
                }

                properties.Add(JsonHelper.Obj(
                    JsonHelper.Str("name", propName),
                    JsonHelper.Str("description", propDesc),
                    JsonHelper.Str("type", propType.ToString()),
                    $"\"value\":{valStr}"
                ));
            }

            return ResponseHelper.Ok(
                JsonHelper.Num("instanceId", mat.GetInstanceID()),
                JsonHelper.Str("name", mat.name),
                JsonHelper.Str("shader", shader.name),
                $"\"properties\":{JsonHelper.Arr(properties.ToArray())}");
        }
    }
}
#endif
