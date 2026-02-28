#if UNITY_EDITOR
using System.Collections.Generic;

namespace Antigravity.MCP.Editor
{
    /// <summary>
    /// Safe JSON string building utilities.
    /// Prevents broken JSON from special characters in Unity object names, paths, etc.
    /// </summary>
    public static class JsonHelper
    {
        /// <summary>Escape a string for safe JSON embedding.</summary>
        public static string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t")
                    .Replace("\b", "\\b")
                    .Replace("\f", "\\f");
        }

        /// <summary>Wrap a value as a JSON string field: "key":"escaped_value"</summary>
        public static string Str(string key, string value) =>
            $"\"{key}\":\"{Escape(value)}\"";

        /// <summary>Wrap a value as a JSON number field: "key":123</summary>
        public static string Num(string key, object value) =>
            $"\"{key}\":{value}";

        /// <summary>Wrap a value as a JSON bool field: "key":true</summary>
        public static string Bool(string key, bool value) =>
            $"\"{key}\":{(value ? "true" : "false")}";

        /// <summary>Build a JSON object from field strings: {"f1":"v1","f2":2}</summary>
        public static string Obj(params string[] fields) =>
            "{" + string.Join(",", fields) + "}";

        /// <summary>Build a JSON array from items: [item1,item2]</summary>
        public static string Arr(params string[] items) =>
            "[" + string.Join(",", items) + "]";

        /// <summary>
        /// Lightweight JSON parser that extracts top-level string key-value pairs from a flat JSON object.
        /// Used for auth handshake parsing without external JSON library dependency (Unity has no System.Text.Json).
        /// Returns null if parsing fails.
        /// </summary>
        public static Dictionary<string, string> ParseToDict(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            var dict = new Dictionary<string, string>();
            json = json.Trim();
            if (!json.StartsWith("{") || !json.EndsWith("}")) return null;

            // Remove outer braces
            var inner = json.Substring(1, json.Length - 2).Trim();
            if (string.IsNullOrEmpty(inner)) return dict;

            // Simple state machine to extract "key":"value" pairs
            int i = 0;
            while (i < inner.Length)
            {
                // Skip whitespace and commas
                while (i < inner.Length && (inner[i] == ' ' || inner[i] == ',' || inner[i] == '\n' || inner[i] == '\r' || inner[i] == '\t'))
                    i++;
                if (i >= inner.Length) break;

                // Expect opening quote for key
                if (inner[i] != '"') break;
                i++;
                int keyStart = i;
                while (i < inner.Length && inner[i] != '"') i++;
                if (i >= inner.Length) break;
                string key = inner.Substring(keyStart, i - keyStart);
                i++; // skip closing quote

                // Skip colon and whitespace
                while (i < inner.Length && (inner[i] == ' ' || inner[i] == ':')) i++;
                if (i >= inner.Length) break;

                // Read value (string only for our purposes)
                if (inner[i] == '"')
                {
                    i++;
                    int valStart = i;
                    while (i < inner.Length && inner[i] != '"')
                    {
                        if (inner[i] == '\\') i++; // skip escaped char
                        i++;
                    }
                    if (i >= inner.Length) break;
                    string val = inner.Substring(valStart, i - valStart);
                    dict[key] = val;
                    i++; // skip closing quote
                }
                else
                {
                    // Skip non-string values (numbers, bools, etc.)
                    int valStart = i;
                    while (i < inner.Length && inner[i] != ',' && inner[i] != '}') i++;
                    dict[key] = inner.Substring(valStart, i - valStart).Trim();
                }
            }

            return dict;
        }
    }

    /// <summary>
    /// Standardized MCP response builder.
    /// </summary>
    public static class ResponseHelper
    {
        public static string Error(string message) =>
            JsonHelper.Obj(JsonHelper.Str("error", message));

        public static string Ok(params string[] fields) =>
            JsonHelper.Obj(fields);
    }
}
#endif
