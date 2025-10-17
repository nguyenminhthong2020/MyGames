using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MyGames.Desktop.Logs;
using System.Text.Json;

namespace MyGames.Desktop.Services
{
    public class Startup
    {
        private readonly AppSettings _appSettings;
        private readonly LoggerService _logger;
        private readonly ChessGameService _gameService;

        public Startup(AppSettings appSettings, LoggerService logger, ChessGameService gameService)
        {
            _appSettings = appSettings;
            _logger = logger;
            _gameService = gameService;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRouting();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            string url = $"{_appSettings.SchemeDomain}:{_appSettings.HttpPort}";
            _logger.Info($"✅ Internal web server listening at {url}");

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                // ========== 1. Health check ==========
                endpoints.MapGet("/health", async ctx =>
                {
                    ctx.Response.ContentType = "text/plain";
                    await ctx.Response.WriteAsync("ok");
                });

                // ========== 2. /game_started ==========
                endpoints.MapPost("/game_started", async ctx =>
                {
                    try
                    {
                        var payload = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
                        string? gameId = payload.TryGetProperty("gameId", out var g) ? g.GetString() : null;
                        string? side = payload.TryGetProperty("side", out var s) ? s.GetString() : null;

                        var doc = new
                        {
                            type = "game_started",
                            gameId = gameId ?? string.Empty,
                            side = side ?? "white"
                        };

                        if (_gameService != null)
                        {
                            await _gameService.HandleNotificationAsync(JsonSerializer.SerializeToElement(doc));
                            _logger?.Info($"[HTTP] /game_started -> forwarded to ChessGameService (gameId={gameId}, side={side})");
                            ctx.Response.StatusCode = 200;
                            await ctx.Response.WriteAsync("ok");
                        }
                        else
                        {
                            _logger?.Warn("[HTTP] /game_started -> ChessGameService not available");
                            ctx.Response.StatusCode = 500;
                            await ctx.Response.WriteAsync("ChessGameService not available");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error in /game_started");
                        ctx.Response.StatusCode = 500;
                        await ctx.Response.WriteAsync("error: " + ex.Message);
                    }
                });

                // ========== 3. /move_san ==========
                endpoints.MapPost("/move_san", async ctx =>
                {
                    try
                    {
                        var payload = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
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

                        if (_gameService != null)
                        {
                            await _gameService.HandleNotificationAsync(JsonSerializer.SerializeToElement(doc));
                            _logger?.Info($"[HTTP] /move_san -> {move}");
                            ctx.Response.StatusCode = 200;
                            await ctx.Response.WriteAsync("ok");
                        }
                        else
                        {
                            _logger?.Warn("[HTTP] /move_san -> ChessGameService not available");
                            ctx.Response.StatusCode = 500;
                            await ctx.Response.WriteAsync("ChessGameService not available");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error in /move_san");
                        ctx.Response.StatusCode = 500;
                        await ctx.Response.WriteAsync("error: " + ex.Message);
                    }
                });

                // generic notify endpoint 
                endpoints.MapPost("/notify", async ctx =>
                {
                    try
                    {
                        var body = await JsonSerializer.DeserializeAsync<JsonElement>(ctx.Request.Body);
                        _logger?.Info("[HTTP] /notify payload received");
                        if (_gameService != null)
                        {
                            await _gameService.HandleNotificationAsync(body);
                            ctx.Response.StatusCode = 200;
                            await ctx.Response.WriteAsync("ok");
                        }
                        else
                        {
                            ctx.Response.StatusCode = 500;
                            await ctx.Response.WriteAsync("ChessGameService not available");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        _logger?.Error("Error processing /notify", ex);
                        ctx.Response.StatusCode = 500;
                        await ctx.Response.WriteAsync("error: " + ex.Message);
                    }
                });
            });
        }
    }
}
