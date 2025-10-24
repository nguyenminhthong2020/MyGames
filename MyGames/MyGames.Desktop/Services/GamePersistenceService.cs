using MyGames.Desktop.Logs;
using MyGames.Desktop.Models;
using System.IO;
using System.Text.Json;

namespace MyGames.Desktop.Services
{
    public class GamePersistenceService
    {
        private readonly LoggerService _logger;
        private readonly string _saveDir;

        public GamePersistenceService(LoggerService logger)
        {
            _logger = logger;
            _saveDir = Path.Combine(AppContext.BaseDirectory, "SavedGames");
            Directory.CreateDirectory(_saveDir);
        }

        public async Task SaveGameAsync(string gameId, PlayerColor side, IList<ChessMove> moves)
        {
            try
            {
                var data = new
                {
                    gameId,
                    side = side.ToString(),
                    moves,
                    savedAt = DateTime.UtcNow
                };
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                string path = Path.Combine(_saveDir, $"{gameId}.json");
                await File.WriteAllTextAsync(path, json);
                _logger.Information($"Game {gameId} saved to {path}");
            }
            catch (Exception ex)
            {
                _logger.Error($"{ex}: Failed to save game {gameId}");
            }
        }

        public async Task<(PlayerColor side, List<ChessMove> moves)?> LoadGameAsync(string gameId)
        {
            string path = Path.Combine(_saveDir, $"{gameId}.json");
            if (!File.Exists(path))
            {
                _logger.Warning($"Save file not found for {gameId}");
                return null;
            }

            try
            {
                string json = await File.ReadAllTextAsync(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var sideStr = root.GetProperty("side").GetString() ?? "White";
                var side = Enum.TryParse<PlayerColor>(sideStr, true, out var color) ? color : PlayerColor.White;

                var moves = new List<ChessMove>();
                foreach (var move in root.GetProperty("moves").EnumerateArray())
                {
                    moves.Add(new ChessMove
                    {
                        MoveNumber = move.GetProperty("MoveNumber").GetInt32(),
                        MoveNotation = move.GetProperty("MoveNotation").GetString() ?? "",
                        Timestamp = move.GetProperty("Timestamp").GetDateTime()
                    });
                }

                _logger.Information($"Game {gameId} loaded from {path}");
                return (side, moves);
            }
            catch (Exception ex)
            {
                _logger.Error($"{ex}: Failed to load game {gameId}");
                return null;
            }
        }
    }
}
