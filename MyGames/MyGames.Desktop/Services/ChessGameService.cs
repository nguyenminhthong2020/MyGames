using MyGames.Desktop.Logs;
using MyGames.Desktop.Models;
using MyGames.Desktop.ViewModels;
using System.Text.Json;

namespace MyGames.Desktop.Services
{
    public class ChessGameService
    {
        private readonly StockfishService _stockfish;
        //private readonly ILogger<ChessGameService> _logger;
        private readonly MainWindowViewModel _mainVm;
        private readonly LoggerService _logger;
        private readonly GamePersistenceService _gamePersistence;

        public string? CurrentGameId { get; private set; }
        public PlayerColor MyColor { get; private set; } = PlayerColor.White;

        public ChessGameService(
            StockfishService stockfish,
            LoggerService logger,
            GamePersistenceService gamePersistence,
            MainWindowViewModel mainVm)
        {
            _stockfish = stockfish;
            _logger = logger;
            _gamePersistence = gamePersistence;
            _mainVm = mainVm;
        }

        /// <summary>
        /// Nhận các gói thông báo JSON từ HttpListenerService.
        /// Có thể là {type: "game_started", ...} hoặc {type: "move_san", ...}.
        /// </summary>
        public async Task HandleNotificationAsync(JsonElement payload)
        {
            if (!payload.TryGetProperty("type", out var typeProp))
                return;

            string type = typeProp.GetString() ?? string.Empty;
            _logger.Information("Received notification type={Type}", type);

            switch (type)
            {
                case "game_started":
                    var id = payload.GetProperty("gameId").GetString() ?? Guid.NewGuid().ToString();
                    var sideStr = payload.GetProperty("side").GetString() ?? "white";
                    await StartNewGameAsync(id!, sideStr);
                    break;

                case "move_san":
                    var moveSan = payload.GetProperty("san").GetString() ?? "";
                    await OnMoveReceivedAsync(moveSan);
                    break;

                default:
                    _logger.Warning("Unknown notification type: {Type}", type);
                    break;
            }
        }

        private async Task StartNewGameAsync(string gameId, string side)
        {
            CurrentGameId = gameId;
            MyColor = side.Equals("black", StringComparison.OrdinalIgnoreCase)
                ? PlayerColor.Black : PlayerColor.White;

            _mainVm.Moves.Clear();
            _mainVm.CurrentSide = MyColor;
            _mainVm.GameStatus = $"Game {CurrentGameId} started. You are {MyColor}.";

            // 🔹 Load lại moves nếu có file cũ
            try
            {
                var loaded = await _gamePersistence.LoadGameAsync(gameId);
                if (loaded is { } result)
                {
                    var (savedSide, moves) = result;
                    foreach (var move in moves)
                        _mainVm.Moves.Add(move);

                    _logger.Info($"Restored {moves.Count} moves for game {gameId} (side: {savedSide})");
                }
                else
                {
                    _logger.Info($"Starting new game {gameId} ({side})");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error loading saved game {gameId}: {ex.Message}");
            }
        }

        private async Task OnMoveReceivedAsync(string moveSan)
        {
            if (string.IsNullOrWhiteSpace(moveSan))
                return;

            _logger.Info($"Move received: {moveSan}");

            var move = new ChessMove
            {
                MoveNumber = _mainVm.Moves.Count + 1,
                MoveNotation = moveSan,
                Timestamp = DateTime.Now
            };
            _mainVm.Moves.Add(move);

            // 🔹 Lưu lại mỗi khi có nước đi
            if (!string.IsNullOrEmpty(CurrentGameId))
            {
                try
                {
                    await _gamePersistence.SaveGameAsync(CurrentGameId, MyColor, _mainVm.Moves);
                    _logger.Info($"Saved game {CurrentGameId} ({_mainVm.Moves.Count} moves)");
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error saving game {CurrentGameId}: {ex.Message}");
                }
            }

            // 🔹 Engine suggestion
            string? movesUci = BuildUciMovesString(_mainVm.Moves, out bool allAreUci);

            if (!allAreUci)
            {
                _mainVm.RecommendedMove = "(waiting for SAN->UCI conversion)";
                _logger.Info("Moves are not all UCI; skipping engine call until SAN->UCI conversion is implemented.");
                return;
            }

            try
            {
                var suggestion = _stockfish.GetBestMove(movesUci);
                _mainVm.RecommendedMove = suggestion ?? "(no suggestion)";
            }
            catch (Exception ex)
            {
                _logger.Error($"Error calling Stockfish: {ex.Message}");
                _mainVm.RecommendedMove = "(engine error)";
            }
        }

        /// <summary>
        /// Nếu tất cả ChessMove.MoveNotation có dạng UCI (ví dụ 'e2e4' hoặc 'e7e8q'),
        /// thì nối lại thành chuỗi như \"e2e4 e7e5 g1f3\" trả về true.
        /// Nếu có SAN (ví dụ 'Nf3', 'e4'), trả về null + allAreUci=false.
        /// </summary>
        private string? BuildUciMovesString(System.Collections.ObjectModel.ObservableCollection<ChessMove> moves, out bool allAreUci)
        {
            var parts = new List<string>();
            allAreUci = true;

            foreach (var m in moves)
            {
                var s = (m?.MoveNotation ?? "").Trim();
                if (string.IsNullOrEmpty(s))
                {
                    allAreUci = false;
                    break;
                }

                // UCI move format: from( a-h)(1-8) + to(a-h)(1-8) + optional promotion [qrbn]
                // Examples: e2e4, e7e8q
                if (IsUciMove(s))
                {
                    parts.Add(s);
                }
                else
                {
                    allAreUci = false;
                    break;
                }
            }

            return allAreUci ? string.Join(" ", parts) : null;
        }

        private bool IsUciMove(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            // length 4: e2e4 ; length 5: e7e8q (promotion)
            if (s.Length != 4 && s.Length != 5) return false;
            // from square
            char f1 = s[0], f2 = s[1], t1 = s[2], t2 = s[3];
            bool ok = (f1 >= 'a' && f1 <= 'h') && (f2 >= '1' && f2 <= '8')
                   && (t1 >= 'a' && t1 <= 'h') && (t2 >= '1' && t2 <= '8');
            if (!ok) return false;
            if (s.Length == 5)
            {
                char promo = s[4];
                return "qrbn".Contains(char.ToLower(promo));
            }
            return true;
        }
    }
}
