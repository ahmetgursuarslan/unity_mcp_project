using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using UnityMcpRouter;

// ALL console output MUST go to stderr to keep stdio JSON-RPC channel clean
Console.SetOut(Console.Error);

// Global unhandled exception handlers — prevent silent crashes
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Console.Error.WriteLine($"[FATAL] Unhandled exception: {e.ExceptionObject}");
};
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Console.Error.WriteLine($"[WARNING] Unobserved task exception: {e.Exception?.Message}");
    e.SetObserved();
};

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Configure logging to stderr only
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });
    builder.Logging.SetMinimumLevel(
        Environment.GetEnvironmentVariable("LOG_LEVEL")?.ToLower() switch
        {
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "info" => LogLevel.Information,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Error
        }
    );

    // Register the Unity WebSocket client as a singleton
    builder.Services.AddSingleton<UnityWebSocketClient>();

    // Register MCP server with stdio transport and auto-discover tools
    builder.Services
        .AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "unity-mcp-controller",
                Version = "2.0.0"
            };
        })
        .WithStdioServerTransport()
        .WithToolsFromAssembly()
        .WithResources<UnityResourcesProvider>();

    var app = builder.Build();

    // Graceful shutdown: dispose WebSocket client properly
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() =>
    {
        Console.Error.WriteLine("[MCP] Shutting down gracefully...");
        var wsClient = app.Services.GetService<UnityWebSocketClient>();
        wsClient?.Dispose();
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[FATAL] MCP Router crashed: {ex}");
    Environment.Exit(1);
}
