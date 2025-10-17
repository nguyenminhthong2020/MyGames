using MyGames.Desktop.Helpers;
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

            // --- 1) Ngăn trùng lặp: nếu move gần nhất giống hệt, bỏ qua ---
            var last = _mainVm.Moves.LastOrDefault();
            if (last is not null && string.Equals(last.MoveNotation, moveSan, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info($"Duplicate move ignored: {moveSan}");
                return;
            }

            // --- 2) Thêm tạm SAN vào danh sách (lưu lịch sử gốc) ---
            var move = new ChessMove
            {
                MoveNumber = _mainVm.Moves.Count + 1,
                MoveNotation = moveSan,
                Timestamp = DateTime.Now,
                Player = ((_mainVm.Moves.Count % 2) == 0) ? PlayerColor.White : PlayerColor.Black
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

            // --- 3) Thử chuyển SAN -> UCI ngay tại đây ---
            // Build current UCI history (strings) from existing moves (some may still be SAN)
            var existingNotations = _mainVm.Moves.Select(m => m.MoveNotation).ToList();

            string uci = string.Empty;
            try
            {
                // SanToUciConverter.ConvertSanToUci nhận (san, moveHistory)
                // moveHistory giúp converter đưa ra dự đoán tốt hơn (nếu nó cần)
                uci = SanToUciConverter.ConvertSanToUci(moveSan, existingNotations, startingFen: "startpos");

                // Nếu convert ra chuỗi có dạng UCI, cập nhật move lưu trong model thành UCI
                if (!string.IsNullOrWhiteSpace(uci) && IsUciMove(uci))
                {
                    move.MoveNotation = uci; // ghi đè SAN -> UCI để BuildUciMovesString hoạt động
                    _logger.Info($"Converted SAN -> UCI: {moveSan} -> {uci}");
                }
                else
                {
                    _logger.Info($"SAN->UCI conversion returned empty/invalid for {moveSan}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error converting SAN to UCI for {moveSan}: {ex.Message}");
            }


            // --- 4) Nếu hiện tại toàn bộ moves đã là UCI, gọi engine; nếu không, báo chờ ---
            // 🔹 Engine suggestion
            //string? movesUci = BuildUciMovesString(_mainVm.Moves, out bool allAreUci);

            //if (!allAreUci)
            //{
            //    _mainVm.RecommendedMove = "(waiting for SAN->UCI conversion)";
            //    _logger.Info("Moves are not all UCI; skipping engine call until SAN->UCI conversion is implemented.");
            //    return;
            //}
            // 🔹 Cố gắng chuyển tất cả SAN sang UCI trước khi gọi engine
            bool allAreUci = SanToUciConverter.TryConvertAllToUci(_mainVm.Moves.Select(x => x).ToList());

            if (!allAreUci)
            {
                _mainVm.RecommendedMove = "(waiting for SAN->UCI conversion)";
                _logger.Info("Một số nước vẫn chưa convert được sang UCI.");
                return;
            }

            // 🔹 Xây lại chuỗi moves UCI sau khi convert
            string movesUci = BuildUciMovesString(_mainVm.Moves, out _);


            // --- 5) Gọi Stockfish có timeout để tránh treo ---
            try
            {
                // gọi async với timeout (ví dụ 5000 ms)
                var suggestion = await Task.Run(() => _stockfish.GetBestMove(movesUci), cancellationToken: CancellationToken.None);
                _mainVm.RecommendedMove = suggestion ?? "(no suggestion)";
            }
            catch (OperationCanceledException)
            {
                _mainVm.RecommendedMove = "(engine timeout)";
                _logger.Error("Stockfish call timed out.");
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
