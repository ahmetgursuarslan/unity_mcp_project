#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Antigravity.MCP.Editor
{
    /// <summary>
    /// Resilient WebSocket server running inside the Unity Editor.
    /// Supports: multiple concurrent clients, heartbeat/pong, secure auth (timing-safe),
    /// message size limits, HTTPS fallback, auto-restart on failure, and thread-safe counters.
    /// </summary>
    [InitializeOnLoad]
    public static class McpEditorServer
    {
        private static HttpListener _httpListener;
        private static CancellationTokenSource _cts;
        private static readonly List<WebSocket> _activeClients = new List<WebSocket>();
        private static readonly object _clientLock = new object();

        // ─── Public State (Thread-Safe) ──────────
        public static bool IsRunning { get; private set; }
        public static int Port { get; private set; } = 8090;
        public static int ConnectedClientCount
        {
            get { lock (_clientLock) return _activeClients.Count; }
        }
        public static DateTime? LastMessageTime => _lastMessageTime;
        public static long TotalMessagesProcessed => Interlocked.Read(ref _totalMessagesProcessed);
        public static string LastError { get; private set; }

        private static DateTime? _lastMessageTime;
        private static long _totalMessagesProcessed;
        private static DateTime? _startTime;
        private static int _autoRestartCount;

        // ─── Cached Thread-Unsafe State ──────────
        private static string _unityVersionCached = "Unknown";
        private static string _productNameCached = "Unknown";
        private static string _apiKeyCached = "";
        private static volatile bool _isCompiling = false;
        private static volatile bool _isReloading = false;

        // ─── Config ──────────────────────────────
        private const int MaxMessageSize = 2 * 1024 * 1024; // 2 MB
        private const int AuthTimeoutSeconds = 5;
        private const int ClientIdleTimeoutSeconds = 300; // 5 min no activity = drop
        private const int MaxAutoRestarts = 5;
        private const string PORT_PREF = "MCP_SERVER_PORT";
        private const string HTTPS_PREF = "MCP_USE_HTTPS";

        // ─── Events for UI ───────────────────────
        public static event Action OnStateChanged;

        static McpEditorServer()
        {
            EditorApplication.update += OnEditorUpdate;
            Port = EditorPrefs.GetInt(PORT_PREF, 8090);
            Start();
            EditorApplication.quitting += Stop;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
        }

        private static void OnEditorUpdate()
        {
            _isCompiling = EditorApplication.isCompiling;
        }

        private static void OnAfterReload()
        {
            _isReloading = false;
            Start();
        }

        private static void OnBeforeReload()
        {
            _isReloading = true;
            // Notify connected clients that Unity is reloading
            NotifyClients("{\"type\":\"reloading\"}");
            Stop();
        }

        public static void Start()
        {
            if (IsRunning) return;

            try
            {
                // Cache thread-unsafe fields on main thread
                _unityVersionCached = Application.unityVersion;
                _productNameCached = Application.productName;
                _apiKeyCached = EditorPrefs.GetString("MCP_API_KEY", "");
                McpToolRegistry.Initialize();

                Port = EditorPrefs.GetInt(PORT_PREF, 8090);
                _cts = new CancellationTokenSource();
                _httpListener = new HttpListener();

                var useHttps = EditorPrefs.GetBool(HTTPS_PREF, false);
                var scheme = useHttps ? "https" : "http";

                try
                {
                    _httpListener.Prefixes.Add($"{scheme}://localhost:{Port}/");
                    _httpListener.Start();
                }
                catch (Exception ex) when (useHttps)
                {
                    // HTTPS failed (cert not bound) — fallback to HTTP
                    Debug.LogWarning($"[MCP] HTTPS failed ({ex.Message}), falling back to HTTP.");
                    _httpListener.Close();
                    _httpListener = new HttpListener();
                    _httpListener.Prefixes.Add($"http://localhost:{Port}/");
                    _httpListener.Start();
                }

                MainThreadDispatcher.Initialize();

                IsRunning = true;
                _startTime = DateTime.Now;
                _autoRestartCount = 0;
                LastError = null;
                Debug.Log($"[MCP] Server listening on port {Port} ({scheme})");
                OnStateChanged?.Invoke();

                Task.Run(() => AcceptConnectionsAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                IsRunning = false;
                Debug.LogError($"[MCP] Failed to start server: {ex.Message}");
                OnStateChanged?.Invoke();
            }
        }

        public static void Stop()
        {
            if (!IsRunning && _httpListener == null) return;

            try
            {
                _cts?.Cancel();

                lock (_clientLock)
                {
                    foreach (var socket in _activeClients)
                    {
                        if (socket.State == WebSocketState.Open)
                        {
                            try
                            {
                                socket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                    "Unity shutting down", CancellationToken.None).Wait(1000);
                            }
                            catch { }
                        }
                    }
                    _activeClients.Clear();
                }

                _httpListener?.Stop();
                _httpListener?.Close();
                _httpListener = null;

                IsRunning = false;
                Debug.Log("[MCP] Server stopped.");
                OnStateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP] Error during shutdown: {ex.Message}");
            }
        }

        public static void Restart()
        {
            Stop();
            Start();
        }

        public static void SetPort(int port)
        {
            if (port < 1 || port > 65535)
            {
                Debug.LogError("[MCP] Invalid port. Must be 1-65535.");
                return;
            }
            EditorPrefs.SetInt(PORT_PREF, port);
            if (IsRunning) Restart();
            else Port = port;
        }

        /// <summary>
        /// Notify all connected clients with a message (e.g., reloading signal).
        /// </summary>
        private static void NotifyClients(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            lock (_clientLock)
            {
                foreach (var socket in _activeClients)
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        try
                        {
                            socket.SendAsync(new ArraySegment<byte>(bytes),
                                WebSocketMessageType.Text, true, CancellationToken.None).Wait(500);
                        }
                        catch { }
                    }
                }
            }
        }

        private static async Task AcceptConnectionsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();

                    // Health check endpoint
                    if (!context.Request.IsWebSocketRequest)
                    {
                        if (context.Request.Url.AbsolutePath == "/health")
                        {
                            if (_isCompiling || _isReloading)
                            {
                                var errorResp = Encoding.UTF8.GetBytes("{\"status\":\"unavailable\",\"reason\":\"compiling_or_reloading\"}");
                                context.Response.ContentType = "application/json";
                                context.Response.StatusCode = 503;
                                context.Response.OutputStream.Write(errorResp, 0, errorResp.Length);
                                context.Response.Close();
                                continue;
                            }

                            var uptime = IsRunning && _startTime.HasValue
                                ? (DateTime.Now - _startTime.Value).ToString(@"hh\:mm\:ss") : "00:00:00";
                            var health = Encoding.UTF8.GetBytes(
                                $"{{\"status\":\"ok\"," +
                                $"\"version\":\"2.0.0\"," +
                                $"\"unityVersion\":\"{_unityVersionCached}\"," +
                                $"\"projectName\":\"{JsonHelper.Escape(_productNameCached)}\"," +
                                $"\"port\":{Port}," +
                                $"\"clients\":{ConnectedClientCount}," +
                                $"\"tools\":{{\"enabled\":{McpToolRegistry.EnabledToolCount},\"total\":{McpToolRegistry.TotalToolCount}}}," +
                                $"\"pending\":{MainThreadDispatcher.PendingCount}," +
                                $"\"messagesProcessed\":{TotalMessagesProcessed}," +
                                $"\"uptime\":\"{uptime}\"}}");
                            context.Response.ContentType = "application/json";
                            context.Response.StatusCode = 200;
                            context.Response.OutputStream.Write(health, 0, health.Length);
                            context.Response.Close();
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                            context.Response.Close();
                        }
                        continue;
                    }

                    var wsContext = await context.AcceptWebSocketAsync(null);
                    Debug.Log("[MCP] Client connected.");

                    lock (_clientLock) _activeClients.Add(wsContext.WebSocket);
                    OnStateChanged?.Invoke();

                    // Handle each client in its own task (multi-client support)
                    _ = Task.Run(() => HandleClientAsync(wsContext.WebSocket, ct));
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException) { break; }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    LastError = ex.Message;
                    Debug.LogWarning($"[MCP] Connection error: {ex.Message}");

                    // Auto-restart on unexpected listener death
                    if (_autoRestartCount < MaxAutoRestarts)
                    {
                        _autoRestartCount++;
                        Debug.LogWarning($"[MCP] Auto-restart attempt {_autoRestartCount}/{MaxAutoRestarts}...");
                        await Task.Delay(1000 * _autoRestartCount, ct);

                        try
                        {
                            _httpListener?.Stop();
                            _httpListener?.Close();
                        }
                        catch { }

                        try
                        {
                            _httpListener = new HttpListener();
                            _httpListener.Prefixes.Add($"http://localhost:{Port}/");
                            _httpListener.Start();
                            Debug.Log("[MCP] Server auto-restarted successfully.");
                        }
                        catch (Exception restartEx)
                        {
                            Debug.LogError($"[MCP] Auto-restart failed: {restartEx.Message}");
                            break;
                        }
                    }
                    else
                    {
                        Debug.LogError("[MCP] Max auto-restarts reached. Server stopped.");
                        break;
                    }
                }
            }
        }

        private static async Task HandleClientAsync(WebSocket socket, CancellationToken ct)
        {
            var buffer = new byte[128 * 1024];
            var messageBuffer = new System.IO.MemoryStream();

            try
            {
                if (_isCompiling || _isReloading)
                {
                    var reject = Encoding.UTF8.GetBytes("{\"error\":\"Unity is compiling or reloading. Try again later.\"}");
                    await socket.SendAsync(new ArraySegment<byte>(reject),
                        WebSocketMessageType.Text, true, CancellationToken.None);
                    await socket.CloseAsync(WebSocketCloseStatus.TryAgainLater,
                        "Compiling", CancellationToken.None);
                    return;
                }

                // ─── Secure API Key Authentication ───
                var apiKey = _apiKeyCached;
                if (!string.IsNullOrEmpty(apiKey))
                {
                    using var authTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(AuthTimeoutSeconds));
                    using var authCts = CancellationTokenSource.CreateLinkedTokenSource(ct, authTimeout.Token);

                    try
                    {
                        var authResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), authCts.Token);
                        if (authResult.MessageType == WebSocketMessageType.Close) return;

                        var authJson = Encoding.UTF8.GetString(buffer, 0, authResult.Count);

                        // Proper JSON parse + timing-safe key comparison
                        bool authPassed = false;
                        try
                        {
                            var authDoc = JsonHelper.ParseToDict(authJson);
                            if (authDoc != null && authDoc.ContainsKey("apiKey"))
                            {
                                var clientKey = authDoc["apiKey"];
                                authPassed = TimingSafeEquals(apiKey, clientKey);
                            }
                        }
                        catch
                        {
                            authPassed = false;
                        }

                        if (!authPassed)
                        {
                            var reject = Encoding.UTF8.GetBytes("{\"error\":\"Authentication failed\"}");
                            await socket.SendAsync(new ArraySegment<byte>(reject),
                                WebSocketMessageType.Text, true, ct);
                            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation,
                                "Auth failed", CancellationToken.None);
                            Debug.LogWarning("[MCP] Client auth failed — connection rejected.");
                            return;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.LogWarning("[MCP] Client auth timed out — connection rejected.");
                        try
                        {
                            await socket.CloseAsync(WebSocketCloseStatus.PolicyViolation,
                                "Auth timeout", CancellationToken.None);
                        }
                        catch { }
                        return;
                    }

                    var ok = Encoding.UTF8.GetBytes("{\"auth\":\"ok\"}");
                    await socket.SendAsync(new ArraySegment<byte>(ok),
                        WebSocketMessageType.Text, true, ct);
                }

                // ─── Main Message Loop ───────────────
                while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    messageBuffer.SetLength(0);
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            Debug.Log("[MCP] Client disconnected.");
                            return;
                        }
                        messageBuffer.Write(buffer, 0, result.Count);

                        // Guard against oversized messages
                        if (messageBuffer.Length > MaxMessageSize)
                        {
                            Debug.LogWarning($"[MCP] Message exceeded {MaxMessageSize / (1024 * 1024)}MB limit — dropping.");
                            messageBuffer.SetLength(0);
                            var errBytes = Encoding.UTF8.GetBytes(
                                "{\"id\":\"0\",\"error\":\"Message too large\",\"isError\":true}");
                            await socket.SendAsync(new ArraySegment<byte>(errBytes),
                                WebSocketMessageType.Text, true, ct);
                            break;
                        }
                    } while (!result.EndOfMessage);

                    if (messageBuffer.Length == 0) continue;

                    var json = Encoding.UTF8.GetString(
                        messageBuffer.GetBuffer(), 0, (int)messageBuffer.Length);

                    _lastMessageTime = DateTime.Now;
                    Interlocked.Increment(ref _totalMessagesProcessed);

                    // Handle heartbeat ping → respond with pong
                    if (json.Contains("\"type\":\"ping\""))
                    {
                        var pong = Encoding.UTF8.GetBytes("{\"type\":\"pong\"}");
                        await socket.SendAsync(new ArraySegment<byte>(pong),
                            WebSocketMessageType.Text, true, ct);
                        continue;
                    }

                    string response;
                    try
                    {
                        if (_isCompiling || _isReloading)
                        {
                            response = CommandDispatcher.CreateErrorResponse("0", "Unity is compiling or reloading. Command rejected.");
                        }
                        else
                        {
                            response = await CommandDispatcher.DispatchAsync(json);
                        }
                    }
                    catch (Exception ex)
                    {
                        response = CommandDispatcher.CreateErrorResponse("0",
                            $"Dispatch error: {ex.Message}");
                    }

                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await socket.SendAsync(
                        new ArraySegment<byte>(responseBytes),
                        WebSocketMessageType.Text,
                        true,
                        ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException ex)
            {
                Debug.LogWarning($"[MCP] WebSocket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP] Unexpected client error: {ex.Message}");
            }
            finally
            {
                lock (_clientLock)
                {
                    _activeClients.Remove(socket);
                }
                OnStateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Constant-time string comparison to prevent timing attacks on API key.
        /// </summary>
        private static bool TimingSafeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
    }
}
#endif
