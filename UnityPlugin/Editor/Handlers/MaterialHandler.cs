#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles material and shader tools:
    /// unity_material_create, unity_material_assign, unity_material_set_property,
    /// unity_material_get_properties, unity_material_set_texture
    /// </summary>
    public static class MaterialHandler
    {
        [Serializable] private class CreateParams { public string name; public string shaderName; public string savePath; }
        [Serializable] private class AssignParams { public int instanceId; public string materialPath; }
        [Serializable] private class SetPropParams { public string materialPath; public string propertyName; public string propertyType; public string value; }
        [Serializable] private class GetPropsParams { public string materialPath; }
        [Serializable] private class SetTexParams { public string materialPath; public string propertyName; public string texturePath; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_material_create": return HandleCreate(paramsJson);
                    case "unity_material_assign": return HandleAssign(paramsJson);
                    case "unity_material_set_property": return HandleSetProperty(paramsJson);
                    case "unity_material_get_properties": return HandleGetProperties(paramsJson);
                    case "unity_material_set_texture": return HandleSetTexture(paramsJson);
                    default: return $"{{\"error\":\"Unknown material tool: {tool}\"}}";
                }
            });
        }

        private static string HandleCreate(string paramsJson)
        {
            var p = JsonUtility.FromJson<CreateParams>(paramsJson);
            var shader = Shader.Find(p.shaderName ?? "Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            
            var mat = new Material(shader);
            mat.name = p.name ?? "New Material";

            var path = p.savePath ?? $"Assets/Materials/{mat.name}.mat";
            SecurityGuard.ValidatePath(path);
            
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return $"{{\"created\":true,\"path\":\"{path}\",\"shader\":\"{shader.name}\"}}";
        }

        private static string HandleAssign(string paramsJson)
        {
            var p = JsonUtility.FromJson<AssignParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null) return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var mat = AssetDatabase.LoadAssetAtPath<Material>(p.materialPath);
            if (mat == null) return $"{{\"error\":\"Material not found at {p.materialPath}\"}}";

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return "{\"error\":\"No Renderer component on object\"}";

            Undo.RecordObject(renderer, "MCP Assign Material");
            renderer.sharedMaterial = mat;
            EditorUtility.SetDirty(renderer);
            return $"{{\"assigned\":true,\"material\":\"{mat.name}\"}}";
        }

        private static string HandleSetProperty(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetPropParams>(paramsJson);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(p.materialPath);
            if (mat == null) return $"{{\"error\":\"Material not found at {p.materialPath}\"}}";

            Undo.RecordObject(mat, "MCP Set Material Property");
            var propType = (p.propertyType ?? "").ToLower();

            switch (propType)
            {
                case "color":
                    var colorParts = p.value.Trim('[', ']').Split(',');
                    if (colorParts.Length >= 3)
                    {
                        var color = new Color(
                            float.Parse(colorParts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                            float.Parse(colorParts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                            float.Parse(colorParts[2].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                            colorParts.Length > 3 ? float.Parse(colorParts[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 1f);
                        mat.SetColor(p.propertyName, color);
                    }
                    break;
                case "float":
                    mat.SetFloat(p.propertyName, float.Parse(p.value, System.Globalization.CultureInfo.InvariantCulture));
                    break;
                case "int":
                    mat.SetInt(p.propertyName, int.Parse(p.value));
                    break;
                case "vector":
                    var vecParts = p.value.Trim('[', ']').Split(',');
                    mat.SetVector(p.propertyName, new Vector4(
                        float.Parse(vecParts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                        vecParts.Length > 1 ? float.Parse(vecParts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0,
                        vecParts.Length > 2 ? float.Parse(vecParts[2].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0,
                        vecParts.Length > 3 ? float.Parse(vecParts[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 0));
                    break;
                case "keyword_enable":
                    mat.EnableKeyword(p.value);
                    break;
                case "keyword_disable":
                    mat.DisableKeyword(p.value);
                    break;
                default:
                    mat.SetFloat(p.propertyName, float.Parse(p.value, System.Globalization.CultureInfo.InvariantCulture));
                    break;
            }

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return $"{{\"set\":true,\"property\":\"{p.propertyName}\",\"type\":\"{propType}\"}}";
        }

        private static string HandleGetProperties(string paramsJson)
        {
            var p = JsonUtility.FromJson<GetPropsParams>(paramsJson);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(p.materialPath);
            if (mat == null) return $"{{\"error\":\"Material not found at {p.materialPath}\"}}";

            var sb = new StringBuilder();
            sb.Append($"{{\"name\":\"{mat.name}\",\"shader\":\"{mat.shader.name}\",\"properties\":[");

            int propCount = mat.shader.GetPropertyCount();
            for (int i = 0; i < propCount; i++)
            {
                if (i > 0) sb.Append(",");
                var propName = mat.shader.GetPropertyName(i);
                var propType = mat.shader.GetPropertyType(i);
                sb.Append($"{{\"name\":\"{propName}\",\"type\":\"{propType}\"");

                switch (propType)
                {
                    case ShaderPropertyType.Color:
                        var c = mat.GetColor(propName);
                        sb.Append($",\"value\":[{c.r},{c.g},{c.b},{c.a}]");
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        sb.Append($",\"value\":{mat.GetFloat(propName)}");
                        break;
                    case ShaderPropertyType.Vector:
                        var v = mat.GetVector(propName);
                        sb.Append($",\"value\":[{v.x},{v.y},{v.z},{v.w}]");
                        break;
                    case ShaderPropertyType.Texture:
                        var tex = mat.GetTexture(propName);
                        sb.Append($",\"value\":\"{(tex != null ? tex.name : "null")}\"");
                        break;
                }
                sb.Append("}");
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private static string HandleSetTexture(string paramsJson)
        {
            var p = JsonUtility.FromJson<SetTexParams>(paramsJson);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(p.materialPath);
            if (mat == null) return $"{{\"error\":\"Material not found at {p.materialPath}\"}}";

            var tex = AssetDatabase.LoadAssetAtPath<Texture>(p.texturePath);
            if (tex == null) return $"{{\"error\":\"Texture not found at {p.texturePath}\"}}";

            Undo.RecordObject(mat, "MCP Set Texture");
            mat.SetTexture(p.propertyName, tex);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return $"{{\"set\":true,\"property\":\"{p.propertyName}\",\"texture\":\"{tex.name}\"}}";
        }
    }
}
#endif
