using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyGames.Desktop.Helpers;
using MyGames.Desktop.Logs;
using System.Text.Json;

namespace MyGames.Desktop.Services
{
    /// <summary>
    /// Hosted service that starts a local HTTP server on http://127.0.0.1:5000
    /// Provides endpoints:
    ///  - GET  /health         -> "ok"
    ///  - POST /game_started   -> { "gameId": "...", "side": "white" }
    ///  - POST /move_san      -> { "move": "e4" }  (SAN or simple UCI text)
    /// The service forwards payloads to ChessGameService (in DI).
    /// </summary>
    [Obsolete("use Startup.cs instead")]
    public class HttpListenerService : IHostedService
    {
        private IHost? _webHost;
        private readonly LoggerService _logger;
        private readonly AppSettings _appSettings;

        public HttpListenerService(LoggerService logger, AppSettings appSettings)
        {
            _logger = logger;
            _appSettings = appSettings;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            string url = $"{_appSettings.SchemeDomain}:{_appSettings.HttpPort}";
            _logger.Information($"Starting HttpListenerService on {url}");

            _webHost = new HostBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls($"{_appSettings.SchemeDomain}:{Constants.LocalPort}");
                    webBuilder.ConfigureServices(services =>
                    {
                        // no special services required here
                    });
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/health", async ctx =>
                            {
                                ctx.Response.ContentType = "text/plain";
                                await ctx.Response.WriteAsync("ok");
                            });

                            endpoints.MapPost("/game_started", async ctx =>
                            {
                                try
                                {
                                    var payload = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
                                    var gameService = ctx.RequestServices.GetRequiredService<ChessGameService>();

                                    string? gameId = payload.TryGetProperty("gameId", out var g) ? g.GetString() : null;
                                    string? side = payload.TryGetProperty("side", out var s) ? s.GetString() : null;

                                    // Normalize keys we used earlier: ChessGameService.HandleNotification expects type field
                                    var doc = new
                                    {
                                        type = "game_started",
                                        gameId = gameId ?? string.Empty,
                                        side = side ?? "white"
                                    };
                                    // Forward
                                    await gameService.HandleNotificationAsync(JsonSerializer.SerializeToElement(doc));
                                    ctx.Response.StatusCode = 200;
                                    await ctx.Response.WriteAsync("ok");
                                }
                                catch (System.Exception ex)
                                {
                                    ctx.Response.StatusCode = 500;
                                    await ctx.Response.WriteAsync("error: " + ex.Message);
                                }
                            });

                            endpoints.MapPost("/move_san", async ctx =>
                            {
                                try
                                {
                                    var payload = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
                                    var gameService = ctx.RequestServices.GetRequiredService<ChessGameService>();

                                    string? move = payload.TryGetProperty("move", out var m) ? m.GetString() : null;
                                    if (string.IsNullOrWhiteSpace(move))
                                    {
                                        ctx.Response.StatusCode = 400;
                                        await ctx.Response.WriteAsync("missing 'move' field");
                                        return;
                                    }

                                    var doc = new
                                    {
                                        type = "move_san",
                                        san = move
                                    };

                                    await gameService.HandleNotificationAsync(JsonSerializer.SerializeToElement(doc));
                                    ctx.Response.StatusCode = 200;
                                    await ctx.Response.WriteAsync("ok");
                                }
                                catch (System.Exception ex)
                                {
                                    ctx.Response.StatusCode = 500;
                                    await ctx.Response.WriteAsync("error: " + ex.Message);
                                }
                            });
                        });
                    });
                })
                .Build();

            return _webHost.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _webHost?.StopAsync(cancellationToken) ?? Task.CompletedTask;
        }
    }
}
