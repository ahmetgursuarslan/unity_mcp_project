using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace UnityMcpRouter;

/// <summary>
/// Manages a persistent, resilient WebSocket connection to the Unity Editor's MCP plugin.
/// Features: auto-reconnect with exponential backoff, heartbeat/ping-pong, circuit breaker,
/// connection lock, WSS support, and graceful degradation during Unity reloads.
/// </summary>
public sealed class UnityWebSocketClient : IDisposable
{
    // ─── Connection State Machine ─────────────────
    private enum ConnectionState { Disconnected, Connecting, Connected, Reconnecting }

    private readonly ILogger<UnityWebSocketClient> _logger;
    private readonly string _uri;
    private readonly int _timeoutSeconds;
    private readonly int _bufferSize;
    private ClientWebSocket? _socket;
    private volatile ConnectionState _state = ConnectionState.Disconnected;

    // ─── Thread Safety ────────────────────────────
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private CancellationTokenSource? _receiveCts;
    private CancellationTokenSource? _heartbeatCts;
    private Task? _receiveTask;
    private Task? _heartbeatTask;
    private long _requestId;

    // ─── Circuit Breaker ──────────────────────────
    private int _consecutiveFailures;
    private DateTime _circuitOpenUntil = DateTime.MinValue;
    private const int CircuitBreakerThreshold = 5;
    private static readonly TimeSpan CircuitBreakerCooldown = TimeSpan.FromSeconds(30);

    // ─── Heartbeat ────────────────────────────────
    private const int HeartbeatIntervalSeconds = 15;
    private const int HeartbeatTimeoutSeconds = 5;
    private DateTime _lastPongReceived = DateTime.UtcNow;

    // ─── Reconnect Config ─────────────────────────
    private const int MaxReconnectAttempts = 10;
    private const int MaxBackoffSeconds = 30;
    private const int ConnectTimeoutSeconds = 8;

    // ─── Max message size (2 MB) ──────────────────
    private const int MaxMessageSize = 2 * 1024 * 1024;

    public bool IsConnected => _state == ConnectionState.Connected && _socket?.State == WebSocketState.Open;

    public UnityWebSocketClient(ILogger<UnityWebSocketClient> logger)
    {
        _logger = logger;
        var port = Environment.GetEnvironmentVariable("UNITY_WS_PORT") ?? "8090";
        var scheme = Environment.GetEnvironmentVariable("UNITY_WS_SCHEME")?.ToLower() ?? "ws";
        if (scheme != "ws" && scheme != "wss") scheme = "ws";
        _uri = $"{scheme}://localhost:{port}/";
        _timeoutSeconds = int.TryParse(
            Environment.GetEnvironmentVariable("UNITY_REQUEST_TIMEOUT"), out var t) ? t : 45;
        _bufferSize = int.TryParse(
            Environment.GetEnvironmentVariable("UNITY_BUFFER_SIZE"), out var b) ? b : 128 * 1024;
    }

    /// <summary>
    /// Ensures the WebSocket connection is established with full resilience:
    /// connection lock, circuit breaker, exponential backoff with jitter.
    /// </summary>
    public async Task EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (IsConnected) return;

        // Circuit breaker: if open, check cooldown
        if (_consecutiveFailures >= CircuitBreakerThreshold)
        {
            if (DateTime.UtcNow < _circuitOpenUntil)
            {
                var remaining = (_circuitOpenUntil - DateTime.UtcNow).TotalSeconds;
                throw new InvalidOperationException(
                    $"Circuit breaker OPEN — Unity connection failed {_consecutiveFailures} consecutive times. " +
                    $"Cooling down for {remaining:F0}s. Unity may be offline or recompiling.");
            }
            _logger.LogInformation("Circuit breaker half-open — attempting single reconnect...");
        }

        // Connection lock: only one connection attempt at a time
        await _connectLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (IsConnected) return;

            for (int attempt = 1; attempt <= MaxReconnectAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    await CleanupSocketAsync();

                    _state = ConnectionState.Connecting;
                    _socket = new ClientWebSocket();

                    // For WSS with self-signed dev certs on localhost
                    if (_uri.StartsWith("wss://"))
                    {
                        _socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                    }

                    _logger.LogInformation("Connecting to Unity at {Uri} (attempt {Attempt}/{Max})",
                        _uri, attempt, MaxReconnectAttempts);

                    using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    connectCts.CancelAfter(TimeSpan.FromSeconds(ConnectTimeoutSeconds));
                    await _socket.ConnectAsync(new Uri(_uri), connectCts.Token);

                    _logger.LogInformation("TCP connected to Unity Editor.");

                    // API Key authentication handshake
                    await PerformAuthHandshakeAsync(ct);

                    // Start background receive loop
                    _receiveCts = new CancellationTokenSource();
                    _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));

                    // Start heartbeat
                    _heartbeatCts = new CancellationTokenSource();
                    _lastPongReceived = DateTime.UtcNow;
                    _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_heartbeatCts.Token));

                    _state = ConnectionState.Connected;
                    Interlocked.Exchange(ref _consecutiveFailures, 0);
                    _logger.LogInformation("WebSocket connection fully established.");
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    // Auth failure is permanent — don't retry
                    _state = ConnectionState.Disconnected;
                    throw;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _state = ConnectionState.Disconnected;
                    throw;
                }
                catch (Exception ex) when (attempt < MaxReconnectAttempts)
                {
                    _state = ConnectionState.Reconnecting;
                    var backoff = CalculateBackoff(attempt);
                    _logger.LogWarning("Connection attempt {Attempt} failed: {Msg}. Retrying in {Backoff}ms...",
                        attempt, ex.Message, backoff);
                    await Task.Delay(backoff, ct);
                }
            }

            // All attempts exhausted
            _state = ConnectionState.Disconnected;
            var failures = Interlocked.Increment(ref _consecutiveFailures);
            if (failures >= CircuitBreakerThreshold)
            {
                _circuitOpenUntil = DateTime.UtcNow + CircuitBreakerCooldown;
                _logger.LogError("Circuit breaker OPENED after {Failures} consecutive failures. Cooling down for {Seconds}s.",
                    failures, CircuitBreakerCooldown.TotalSeconds);
            }

            throw new InvalidOperationException(
                $"Failed to connect to Unity Editor at {_uri} after {MaxReconnectAttempts} attempts. " +
                "Ensure Unity is running with the MCP plugin enabled and the port matches.");
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Performs API key authentication handshake with timeout.
    /// </summary>
    private async Task PerformAuthHandshakeAsync(CancellationToken ct)
    {
        var apiKey = Environment.GetEnvironmentVariable("MCP_API_KEY") ?? "";
        if (string.IsNullOrEmpty(apiKey)) return;

        _logger.LogDebug("Sending API key authentication...");

        using var authCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        authCts.CancelAfter(TimeSpan.FromSeconds(5));

        var authPayload = JsonSerializer.Serialize(new { type = "auth", apiKey });
        var authMsg = Encoding.UTF8.GetBytes(authPayload);
        await _socket!.SendAsync(new ArraySegment<byte>(authMsg),
            WebSocketMessageType.Text, true, authCts.Token);

        var authBuffer = new byte[1024];
        var authResult = await _socket.ReceiveAsync(
            new ArraySegment<byte>(authBuffer), authCts.Token);
        var authResponse = Encoding.UTF8.GetString(authBuffer, 0, authResult.Count);

        try
        {
            using var doc = JsonDocument.Parse(authResponse);
            if (doc.RootElement.TryGetProperty("error", out _))
            {
                _logger.LogError("API key authentication failed: {Response}", authResponse);
                await CleanupSocketAsync();
                throw new UnauthorizedAccessException("MCP API key authentication failed.");
            }
        }
        catch (JsonException)
        {
            // If it's not valid JSON or doesn't have "error", treat as success
        }

        _logger.LogInformation("API key authentication successful.");
    }

    /// <summary>
    /// Sends a tool command to Unity and awaits the response.
    /// Automatically reconnects on connection loss.
    /// </summary>
    public async Task<JsonElement> SendCommandAsync(string toolName, JsonElement? parameters, CancellationToken ct = default)
    {
        // Attempt with one auto-reconnect on failure
        for (int retry = 0; retry < 2; retry++)
        {
            await EnsureConnectedAsync(ct);

            var id = Interlocked.Increment(ref _requestId).ToString();
            var request = new
            {
                id,
                tool = toolName,
                parameters = parameters ?? JsonDocument.Parse("{}").RootElement
            };

            var json = JsonSerializer.Serialize(request);
            var bytes = Encoding.UTF8.GetBytes(json);

            var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[id] = tcs;

            try
            {
                await _sendLock.WaitAsync(ct);
                try
                {
                    if (!IsConnected)
                        throw new InvalidOperationException("Connection lost before send.");

                    await _socket!.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        ct);
                }
                finally
                {
                    _sendLock.Release();
                }

                _logger.LogDebug("Sent command {Tool} (id={Id})", toolName, id);

                // Wait for response with timeout
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

                var responseTask = tcs.Task;
                var completedTask = await Task.WhenAny(responseTask, Task.Delay(-1, timeoutCts.Token));

                if (completedTask == responseTask)
                {
                    return await responseTask;
                }

                _pendingRequests.TryRemove(id, out _);
                throw new TimeoutException(
                    $"Unity did not respond to '{toolName}' within {_timeoutSeconds}s. " +
                    "The operation may still be running in Unity. Try increasing UNITY_REQUEST_TIMEOUT.");
            }
            catch (InvalidOperationException) when (retry == 0)
            {
                // Connection lost during send — clean up and retry once
                _pendingRequests.TryRemove(id, out _);
                _logger.LogWarning("Connection lost during send of {Tool}. Reconnecting and retrying...", toolName);
                await CleanupSocketAsync();
                continue;
            }
            catch (WebSocketException) when (retry == 0)
            {
                _pendingRequests.TryRemove(id, out _);
                _logger.LogWarning("WebSocket error sending {Tool}. Reconnecting and retrying...", toolName);
                await CleanupSocketAsync();
                continue;
            }
            catch
            {
                _pendingRequests.TryRemove(id, out _);
                throw;
            }
        }

        throw new InvalidOperationException($"Failed to send command '{toolName}' after retry.");
    }

    /// <summary>
    /// Background loop that reads WebSocket messages and resolves pending requests.
    /// On exit, triggers graceful socket cleanup so next SendCommand triggers reconnect.
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[_bufferSize];
        var messageBuffer = new MemoryStream();

        try
        {
            while (!ct.IsCancellationRequested && _socket?.State == WebSocketState.Open)
            {
                messageBuffer.SetLength(0);
                WebSocketReceiveResult result;

                do
                {
                    result = await _socket!.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogWarning("Unity closed the WebSocket connection gracefully.");
                        return;
                    }
                    messageBuffer.Write(buffer, 0, result.Count);

                    // Guard against oversized messages
                    if (messageBuffer.Length > MaxMessageSize)
                    {
                        _logger.LogError("Message exceeded {Max}MB limit — dropping.", MaxMessageSize / (1024 * 1024));
                        messageBuffer.SetLength(0);
                        break;
                    }
                } while (!result.EndOfMessage);

                if (messageBuffer.Length == 0) continue;

                var json = Encoding.UTF8.GetString(messageBuffer.GetBuffer(), 0, (int)messageBuffer.Length);
                _logger.LogDebug("Received: {Json}", json.Length > 200 ? json[..200] + "..." : json);

                try
                {
                    using var doc = JsonDocument.Parse(json);

                    // Handle heartbeat pong
                    if (doc.RootElement.TryGetProperty("type", out var typeProp) &&
                        typeProp.GetString() == "pong")
                    {
                        _lastPongReceived = DateTime.UtcNow;
                        continue;
                    }

                    // Handle "reloading" signal from Unity
                    if (doc.RootElement.TryGetProperty("type", out var typeProp2) &&
                        typeProp2.GetString() == "reloading")
                    {
                        _logger.LogInformation("Unity is reloading scripts — connection will reset.");
                        continue;
                    }

                    if (doc.RootElement.TryGetProperty("id", out var idProp))
                    {
                        var id = idProp.GetString();
                        if (id != null && _pendingRequests.TryRemove(id, out var tcs))
                        {
                            tcs.TrySetResult(doc.RootElement.Clone());
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError("Failed to parse Unity response: {Err}", ex.Message);
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex)
        {
            _logger.LogWarning("WebSocket receive error: {Msg}. Connection will be re-established on next command.", ex.Message);
        }
        catch (ObjectDisposedException) { }
        finally
        {
            // Fail all pending requests so callers don't hang
            FailAllPendingRequests("WebSocket connection lost. Reconnect will happen on next command.");

            // Mark as disconnected so next SendCommand triggers reconnect
            _state = ConnectionState.Disconnected;
        }
    }

    /// <summary>
    /// Periodic heartbeat loop: sends ping every 15s, checks for pong within 5s.
    /// Detects silent TCP drops that raw WebSocket doesn't catch.
    /// </summary>
    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), ct);

                if (_socket?.State != WebSocketState.Open) break;

                try
                {
                    var pingMsg = Encoding.UTF8.GetBytes("{\"type\":\"ping\"}");
                    await _sendLock.WaitAsync(ct);
                    try
                    {
                        await _socket!.SendAsync(
                            new ArraySegment<byte>(pingMsg),
                            WebSocketMessageType.Text, true, ct);
                    }
                    finally
                    {
                        _sendLock.Release();
                    }

                    // Wait and check for pong
                    await Task.Delay(TimeSpan.FromSeconds(HeartbeatTimeoutSeconds), ct);

                    if ((DateTime.UtcNow - _lastPongReceived).TotalSeconds > HeartbeatIntervalSeconds + HeartbeatTimeoutSeconds)
                    {
                        _logger.LogWarning("Heartbeat timeout — no pong received in {Sec}s. Marking connection as dead.",
                            HeartbeatIntervalSeconds + HeartbeatTimeoutSeconds);
                        await CleanupSocketAsync();
                        break;
                    }
                }
                catch (WebSocketException)
                {
                    _logger.LogWarning("Heartbeat send failed — connection is dead.");
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Calculate exponential backoff with jitter: min(2^attempt * 1000 + jitter, MaxBackoff)
    /// </summary>
    private static int CalculateBackoff(int attempt)
    {
        var baseDelay = Math.Min((int)Math.Pow(2, attempt) * 1000, MaxBackoffSeconds * 1000);
        var jitter = Random.Shared.Next(0, 500);
        return baseDelay + jitter;
    }

    /// <summary>
    /// Cleanly tear down the current socket, cancel receive/heartbeat tasks.
    /// </summary>
    private async Task CleanupSocketAsync()
    {
        _heartbeatCts?.Cancel();
        _receiveCts?.Cancel();

        // Give receive task a moment to exit
        if (_receiveTask != null)
        {
            try { await _receiveTask.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { /* Expected — task may throw on cancellation */ }
        }
        if (_heartbeatTask != null)
        {
            try { await _heartbeatTask.WaitAsync(TimeSpan.FromSeconds(1)); }
            catch { }
        }

        try { _socket?.Dispose(); } catch { }
        _socket = null;
        _receiveCts?.Dispose();
        _receiveCts = null;
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
        _receiveTask = null;
        _heartbeatTask = null;
        _state = ConnectionState.Disconnected;
    }

    /// <summary>
    /// Fail all pending request TCS so callers get an exception instead of hanging.
    /// </summary>
    private void FailAllPendingRequests(string reason)
    {
        foreach (var kvp in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(kvp.Key, out var tcs))
            {
                tcs.TrySetException(new InvalidOperationException(reason));
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupSocketAsync();
        _sendLock.Dispose();
        _connectLock.Dispose();
    }

    public void Dispose()
    {
        _heartbeatCts?.Cancel();
        _receiveCts?.Cancel();
        try { _socket?.Dispose(); } catch { }
        _sendLock.Dispose();
        _connectLock.Dispose();
        _receiveCts?.Dispose();
        _heartbeatCts?.Dispose();
        FailAllPendingRequests("WebSocket client disposed.");
    }
}
