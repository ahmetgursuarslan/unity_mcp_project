#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles component tools:
    /// unity_component_add, unity_component_remove, unity_component_update
    /// Uses .NET Reflection for dynamic field/property access.
    /// </summary>
    public static class ComponentHandler
    {
        [Serializable] private class AddParams { public int instanceId; public string componentType; }
        [Serializable] private class RemoveParams { public int instanceId; public string componentType; }
        [Serializable] private class UpdateParams 
        { 
            public int instanceId; 
            public string componentType; 
        }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_component_add": return HandleAdd(paramsJson);
                    case "unity_component_remove": return HandleRemove(paramsJson);
                    case "unity_component_update": return HandleUpdate(paramsJson);
                    default: return $"{{\"error\": \"Unknown component tool: {tool}\"}}";
                }
            });
        }

        private static string HandleAdd(string paramsJson)
        {
            var p = JsonUtility.FromJson<AddParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null)
                return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var type = GameObjectHandler.ResolveUnityType(p.componentType);
            if (type == null)
                return $"{{\"error\":\"Component type '{p.componentType}' not found\"}}";

            var comp = Undo.AddComponent(go, type);
            return $"{{\"added\":true,\"componentType\":\"{type.Name}\",\"instanceId\":{comp.GetInstanceID()}}}";
        }

        private static string HandleRemove(string paramsJson)
        {
            var p = JsonUtility.FromJson<RemoveParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null)
                return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var type = GameObjectHandler.ResolveUnityType(p.componentType);
            if (type == null)
                return $"{{\"error\":\"Component type '{p.componentType}' not found\"}}";

            var comp = go.GetComponent(type);
            if (comp == null)
                return $"{{\"error\":\"Component '{p.componentType}' not found on {go.name}\"}}";

            Undo.DestroyObjectImmediate(comp);
            return "{\"removed\":true}";
        }

        private static string HandleUpdate(string paramsJson)
        {
            // Parse the basic envelope
            var p = JsonUtility.FromJson<UpdateParams>(paramsJson);
            var go = EditorUtility.InstanceIDToObject(p.instanceId) as GameObject;
            if (go == null)
                return $"{{\"error\":\"GameObject {p.instanceId} not found\"}}";

            var type = GameObjectHandler.ResolveUnityType(p.componentType);
            if (type == null)
                return $"{{\"error\":\"Component type '{p.componentType}' not found\"}}";

            var comp = go.GetComponent(type);
            if (comp == null)
                return $"{{\"error\":\"Component '{p.componentType}' not found on {go.name}\"}}";

            Undo.RecordObject(comp, "MCP Update Component");

            // Extract the "fields" object from raw JSON
            var fieldsJson = ExtractFieldsJson(paramsJson);
            if (string.IsNullOrEmpty(fieldsJson) || fieldsJson == "{}")
                return "{\"error\":\"No fields provided\"}";

            var updatedFields = new List<string>();
            var errors = new List<string>();

            // Simple JSON key-value parsing for the fields object
            var pairs = ParseSimpleJsonObject(fieldsJson);
            foreach (var kvp in pairs)
            {
                try
                {
                    if (SetMemberValue(comp, type, kvp.Key, kvp.Value))
                        updatedFields.Add(kvp.Key);
                    else
                        errors.Add($"Field '{kvp.Key}' not found on {type.Name}");
                }
                catch (Exception ex)
                {
                    errors.Add($"Error setting '{kvp.Key}': {ex.Message}");
                }
            }

            EditorUtility.SetDirty(comp);

            var sb = new StringBuilder("{\"updated\":[");
            for (int i = 0; i < updatedFields.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"\"{updatedFields[i]}\"");
            }
            sb.Append("]");
            if (errors.Count > 0)
            {
                sb.Append(",\"errors\":[");
                for (int i = 0; i < errors.Count; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append($"\"{CommandDispatcher.CreateErrorResponse("", errors[i]).Replace("\"", "'")}\"");
                }
                sb.Append("]");
            }
            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Uses Reflection to set a field or property value on a component.
        /// Handles basic type conversions (float, int, bool, string, Vector3, Color).
        /// </summary>
        private static bool SetMemberValue(Component comp, Type type, string memberName, string valueStr)
        {
            // Try field first
            var field = type.GetField(memberName, 
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                var value = ConvertValue(valueStr, field.FieldType);
                field.SetValue(comp, value);
                return true;
            }

            // Try property
            var prop = type.GetProperty(memberName,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanWrite)
            {
                var value = ConvertValue(valueStr, prop.PropertyType);
                prop.SetValue(comp, value);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Converts a JSON string value to the appropriate .NET/Unity type.
        /// </summary>
        private static object ConvertValue(string valueStr, Type targetType)
        {
            valueStr = valueStr.Trim();

            if (targetType == typeof(float))
                return float.Parse(valueStr, System.Globalization.CultureInfo.InvariantCulture);
            if (targetType == typeof(int))
                return int.Parse(valueStr);
            if (targetType == typeof(bool))
                return valueStr.ToLower() == "true" || valueStr == "1";
            if (targetType == typeof(string))
                return valueStr.Trim('"');
            if (targetType == typeof(double))
                return double.Parse(valueStr, System.Globalization.CultureInfo.InvariantCulture);
            
            if (targetType == typeof(Vector3))
            {
                var parts = valueStr.Trim('[', ']').Split(',');
                return new Vector3(
                    float.Parse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[2].Trim(), System.Globalization.CultureInfo.InvariantCulture));
            }
            
            if (targetType == typeof(Vector2))
            {
                var parts = valueStr.Trim('[', ']').Split(',');
                return new Vector2(
                    float.Parse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture));
            }
            
            if (targetType == typeof(Color))
            {
                var parts = valueStr.Trim('[', ']').Split(',');
                return new Color(
                    float.Parse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[2].Trim(), System.Globalization.CultureInfo.InvariantCulture),
                    parts.Length > 3 ? float.Parse(parts[3].Trim(), System.Globalization.CultureInfo.InvariantCulture) : 1f);
            }

            if (targetType.IsEnum)
                return Enum.Parse(targetType, valueStr.Trim('"'), true);

            return Convert.ChangeType(valueStr, targetType, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string ExtractFieldsJson(string json)
        {
            var key = "\"fields\"";
            var idx = json.IndexOf(key, StringComparison.Ordinal);
            if (idx == -1) return "{}";

            idx += key.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t'))
                idx++;

            if (idx >= json.Length || json[idx] != '{') return "{}";

            int depth = 0;
            int start = idx;
            bool inString = false;
            for (int i = idx; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                    inString = !inString;
                if (!inString)
                {
                    if (c == '{') depth++;
                    else if (c == '}') { depth--; if (depth == 0) return json.Substring(start, i - start + 1); }
                }
            }
            return "{}";
        }

        /// <summary>
        /// Parses a simple flat JSON object into key-value string pairs.
        /// Handles basic JSON values (strings, numbers, booleans, arrays).
        /// </summary>
        private static Dictionary<string, string> ParseSimpleJsonObject(string json)
        {
            var result = new Dictionary<string, string>();
            json = json.Trim('{', '}').Trim();
            if (string.IsNullOrEmpty(json)) return result;

            int i = 0;
            while (i < json.Length)
            {
                // Find key
                int keyStart = json.IndexOf('"', i);
                if (keyStart == -1) break;
                int keyEnd = json.IndexOf('"', keyStart + 1);
                if (keyEnd == -1) break;
                var key = json.Substring(keyStart + 1, keyEnd - keyStart - 1);

                // Find colon
                i = json.IndexOf(':', keyEnd + 1);
                if (i == -1) break;
                i++;

                // Skip whitespace
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;

                // Read value
                string value;
                if (json[i] == '"')
                {
                    // String value
                    int valEnd = json.IndexOf('"', i + 1);
                    while (valEnd > 0 && json[valEnd - 1] == '\\')
                        valEnd = json.IndexOf('"', valEnd + 1);
                    value = json.Substring(i + 1, valEnd - i - 1);
                    i = valEnd + 1;
                }
                else if (json[i] == '[')
                {
                    // Array value — find matching bracket
                    int depth = 0;
                    int start = i;
                    for (; i < json.Length; i++)
                    {
                        if (json[i] == '[') depth++;
                        else if (json[i] == ']') { depth--; if (depth == 0) { i++; break; } }
                    }
                    value = json.Substring(start, i - start);
                }
                else
                {
                    // Number/bool/null
                    int start = i;
                    while (i < json.Length && json[i] != ',' && json[i] != '}' && json[i] != ']')
                        i++;
                    value = json.Substring(start, i - start).Trim();
                }

                result[key] = value;

                // Skip comma
                while (i < json.Length && (json[i] == ',' || char.IsWhiteSpace(json[i]))) i++;
            }

            return result;
        }
    }
}
#endif
