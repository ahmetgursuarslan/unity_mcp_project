#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Antigravity.MCP.Editor.Handlers
{
    /// <summary>
    /// Handles Unity Package Manager tools:
    /// unity_package_list, unity_package_add, unity_package_remove, unity_package_search
    /// </summary>
    public static class PackageHandler
    {
        [Serializable] private class PkgParams { public string packageId; }
        [Serializable] private class SearchParams { public string query; }

        public static Task<string> Handle(string tool, string paramsJson)
        {
            return MainThreadDispatcher.EnqueueAsync(() =>
            {
                switch (tool)
                {
                    case "unity_package_list": return HandleList();
                    case "unity_package_add": return HandleAdd(paramsJson);
                    case "unity_package_remove": return HandleRemove(paramsJson);
                    case "unity_package_search": return HandleSearch(paramsJson);
                    default: return $"{{\"error\":\"Unknown package tool: {tool}\"}}";
                }
            });
        }

        private static string HandleList()
        {
            var request = Client.List(true);
            while (!request.IsCompleted) System.Threading.Thread.Sleep(50);

            if (request.Status == StatusCode.Failure)
                return $"{{\"error\":\"{request.Error.message}\"}}";

            var sb = new System.Text.StringBuilder("[");
            bool first = true;
            foreach (var pkg in request.Result)
            {
                if (!first) sb.Append(",");
                first = false;
                sb.Append($"{{\"name\":\"{pkg.name}\",\"version\":\"{pkg.version}\",\"source\":\"{pkg.source}\"}}");
            }
            sb.Append("]");
            return $"{{\"packages\":{sb}}}";
        }

        private static string HandleAdd(string paramsJson)
        {
            var p = JsonUtility.FromJson<PkgParams>(paramsJson);
            if (string.IsNullOrEmpty(p.packageId)) return "{\"error\":\"packageId required (e.g. com.unity.cinemachine)\"}";

            var request = Client.Add(p.packageId);
            while (!request.IsCompleted) System.Threading.Thread.Sleep(50);

            if (request.Status == StatusCode.Failure)
                return $"{{\"error\":\"{request.Error.message}\"}}";

            return $"{{\"added\":true,\"name\":\"{request.Result.name}\",\"version\":\"{request.Result.version}\"}}";
        }

        private static string HandleRemove(string paramsJson)
        {
            var p = JsonUtility.FromJson<PkgParams>(paramsJson);
            if (string.IsNullOrEmpty(p.packageId)) return "{\"error\":\"packageId required\"}";

            var request = Client.Remove(p.packageId);
            while (!request.IsCompleted) System.Threading.Thread.Sleep(50);

            if (request.Status == StatusCode.Failure)
                return $"{{\"error\":\"{request.Error.message}\"}}";

            return $"{{\"removed\":true,\"packageId\":\"{p.packageId}\"}}";
        }

        private static string HandleSearch(string paramsJson)
        {
            var p = JsonUtility.FromJson<SearchParams>(paramsJson);
            var request = Client.SearchAll(p.query);
            while (!request.IsCompleted) System.Threading.Thread.Sleep(50);

            if (request.Status == StatusCode.Failure)
                return $"{{\"error\":\"{request.Error.message}\"}}";

            var sb = new System.Text.StringBuilder("[");
            bool first = true;
            int count = 0;
            foreach (var pkg in request.Result)
            {
                if (count++ >= 20) break;
                if (!first) sb.Append(",");
                first = false;
                sb.Append($"{{\"name\":\"{pkg.name}\",\"version\":\"{pkg.versions.latest}\",\"description\":\"{EscapeJson(pkg.description)}\"}}");
            }
            sb.Append("]");
            return $"{{\"results\":{sb}}}";
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "");
        }
    }
}
#endif
