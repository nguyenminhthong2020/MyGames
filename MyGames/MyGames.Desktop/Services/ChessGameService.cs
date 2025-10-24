using Microsoft.Extensions.DependencyInjection;
using MyGames.Desktop.Helpers;
using MyGames.Desktop.Logs;
using MyGames.Desktop.Models;
using MyGames.Desktop.ViewModels;
using System;
using System.Text.Json;
using System.Windows;

//namespace MyGames.Desktop.Services
//{
//    public class ChessGameService
//    {
//        private readonly StockfishService _stockfish;
//        //private readonly ILogger<ChessGameService> _logger;
//        private readonly MainWindowViewModel _mainVm;
//        private readonly LoggerService _logger;
//        private readonly GamePersistenceService _gamePersistence;

//        public string? CurrentGameId { get; private set; }
//        public PlayerColor MyColor { get; private set; } = PlayerColor.White;

//        // Giữ hàng đợi nước đi để đảm bảo thứ tự
//        private readonly Queue<(string move, int index, bool isReplay)> _moveQueue = new();
//        private bool _isProcessingQueue = false;
//        private bool _isReplayingInitialMoves = false;

//        public ChessGameService(
//            StockfishService stockfish,
//            LoggerService logger,
//            GamePersistenceService gamePersistence,
//            MainWindowViewModel mainVm)
//        {
//            _stockfish = stockfish;
//            _logger = logger;
//            _gamePersistence = gamePersistence;
//            _mainVm = mainVm;
//        }

//        /// <summary>
//        /// Nhận các gói thông báo JSON 
//        /// { type: "game_started", gameId, side, currentMoves[] }
//        /// { type: "move_san", gameId, move, moveIndex }
//        /// </summary>
//        public async Task HandleNotificationAsync(JsonElement payload)
//        {
//            if (!payload.TryGetProperty("type", out var typeProp))
//                return;

//            string type = typeProp.GetString() ?? string.Empty;
//            _logger.Information($"Received notification type={type}");

//            //switch (type)
//            //{
//            //    case "game_started":
//            //        var id = payload.GetProperty("gameId").GetString() ?? Guid.NewGuid().ToString();
//            //        var sideStr = payload.GetProperty("side").GetString() ?? "white";
//            //        await StartNewGameAsync(id!, sideStr);
//            //        break;

//            //    case "move_san":
//            //        var moveSan = payload.GetProperty("san").GetString() ?? "";
//            //        await OnMoveReceivedAsync(moveSan);
//            //        break;

//            //    default:
//            //        _logger.Warning("Unknown notification type: {Type}", type);
//            //        break;
//            //}

//            switch (type)
//            {
//                case "game_started":
//                    {
//                        var id = payload.TryGetProperty("gameId", out var g)
//                            ? g.GetString() ?? Guid.NewGuid().ToString()
//                            : Guid.NewGuid().ToString();

//                        var sideStr = payload.TryGetProperty("side", out var s)
//                            ? s.GetString() ?? "white"
//                            : "white";

//                        List<string> moves = new();
//                        if (payload.TryGetProperty("currentMoves", out var arr) && arr.ValueKind == JsonValueKind.Array)
//                        {
//                            foreach (var m in arr.EnumerateArray())
//                            {
//                                var san = m.GetString();
//                                if (!string.IsNullOrWhiteSpace(san))
//                                    moves.Add(san);
//                            }
//                        }

//                        await StartNewGameAsync(id, sideStr, moves);
//                    }
//                    break;

//                case "move_san":
//                    {
//                        string moveSan = payload.TryGetProperty("move", out var m) ? m.GetString() ?? "" : "";
//                        int moveIndex = payload.TryGetProperty("moveIndex", out var idx) && idx.TryGetInt32(out var val)
//                            ? val
//                            : -1;
//                        await EnqueueMoveAsync(moveSan, moveIndex);
//                    }
//                    break;

//                default:
//                    _logger.Warning($"Unknown notification type: {type}");
//                    break;
//            }
//        }

//        private async Task StartNewGameAsync(string gameId, string side, List<string> existingMoves)
//        {
//            var dispatcher = Application.Current?.Dispatcher;
//            if (dispatcher == null)
//            {
//                _logger.Warn("Dispatcher không sẵn sàng, bỏ qua StartNewGameAsync.");
//                return;
//            }

//            CurrentGameId = gameId;
//            MyColor = side.Equals("black", StringComparison.OrdinalIgnoreCase)
//                ? PlayerColor.Black
//                : PlayerColor.White;

//            await dispatcher.InvokeAsync(() =>
//            {
//                _mainVm.ResetGame();
//                _mainVm.Moves.Clear();
//                _mainVm.CurrentSide = MyColor;
//                _mainVm.GameStatus = $"Game {CurrentGameId} started. You are {MyColor}.";
//                _mainVm.PlayerColorProperty = MyColor;
//                _mainVm.IsPlayerWhite = (MyColor == PlayerColor.White);
//                _mainVm.SelectedColorIndex = (MyColor == PlayerColor.White) ? 1 : 2;
//                //_mainVm.IsPlayerTurn = false; // mặc định: vì extension sẽ gửi nước đầu tiên là của đối thủ
//                _mainVm.IsPlayerTurn = (MyColor == PlayerColor.White);
//            });

//            _logger.Info($"New game {gameId} ({side}) initialized with {existingMoves.Count} existing moves.");

//            // Nếu extension gửi sẵn các moves hiện có, thêm vào queue để chạy tuần tự
//            _isReplayingInitialMoves = existingMoves.Count > 0;
//            int idx = 0;
//            foreach (var san in existingMoves)
//            {
//                _moveQueue.Enqueue((san, idx++, true));
//            }

//            // Bắt đầu xử lý queue
//            _ = ProcessMoveQueueAsync();
//        }

//        private async Task EnqueueMoveAsync(string moveSan, int moveIndex)
//        {
//            if (string.IsNullOrWhiteSpace(moveSan))
//                return;

//            // isReplay = false for live moves
//            _moveQueue.Enqueue((moveSan, moveIndex, false));
//            _logger.Info($"Queued move: {moveSan} (idx={moveIndex})");

//            // Bắt đầu xử lý nếu chưa có ai đang xử lý
//            if (!_isProcessingQueue)
//                await ProcessMoveQueueAsync();
//        }

//        private async Task ProcessMoveQueueAsync()
//        {
//            if (_isProcessingQueue) return;
//            _isProcessingQueue = true;

//            while (_moveQueue.Count > 0)
//            {
//                var (move, index, _) = _moveQueue.Dequeue();
//                _logger.Info($"Dequeued move idx={index}: {move}");
//                await OnMoveReceivedAsync(move);
//                await Task.Delay(100); // tránh block UI liên tục
//            }

//            // Sau khi đẩy hết queue, tính lại lượt kế tiếp và cập nhật UI
//            var dispatcher = Application.Current?.Dispatcher;
//            if (dispatcher != null)
//            {
//                await dispatcher.InvokeAsync(() =>
//                {
//                    // nextSideToMove: nếu số moves hiện có chẵn => White; lẻ => Black
//                    var movesCount = _mainVm.Moves.Count;
//                    var nextSide = (movesCount % 2 == 0) ? PlayerColor.White : PlayerColor.Black;

//                    // nếu người chơi màu MyColor thì IsPlayerTurn = (MyColor == nextSide)
//                    _mainVm.IsPlayerTurn = (MyColor == nextSide);
//                    _mainVm.RecommendedMove = _mainVm.IsPlayerTurn ? "(your turn)" : "(opponent's turn)";
//                    _logger.Info($"Replay finished. sideToMove={nextSide}. IsPlayerTurn={_mainVm.IsPlayerTurn}");
//                });
//            }

//            _isProcessingQueue = false;
//        }


//        private async Task OnMoveReceivedAsync(string moveSan, bool isReplay = false)
//        {
//            if (string.IsNullOrWhiteSpace(moveSan))
//                return;

//            if (_mainVm == null || !App.AppIsReady)
//            {
//                _logger.Warn("App chưa sẵn sàng, bỏ qua move.");
//                return;
//            }

//            var dispatcher = Application.Current?.Dispatcher;
//            if (dispatcher == null)
//            {
//                _logger.Warn("Dispatcher không sẵn sàng, bỏ qua move.");
//                return;
//            }

//            _logger.Info($"Move received: {moveSan} (isReplay={isReplay})");

//            // --- 1) Ngăn trùng lặp: nếu move gần nhất giống hệt, bỏ qua ---
//            bool isDuplicate = await dispatcher.InvokeAsync(() =>
//            {
//                var last = _mainVm.Moves.LastOrDefault();
//                return last is not null && string.Equals(last.MoveNotation, moveSan, StringComparison.OrdinalIgnoreCase);
//            }).Task;
//            if (isDuplicate)
//            {
//                _logger.Info($"Duplicate move ignored: {moveSan}");
//                return;
//            }

//            // --- 2) Thêm move vào danh sách ---
//            var move = new ChessMove
//            {
//                MoveNotation = moveSan,
//                Timestamp = DateTime.Now,
//            };

//            await dispatcher.InvokeAsync(() =>
//            {
//                move.MoveNumber = _mainVm.Moves.Count + 1;
//                move.Player = ((_mainVm.Moves.Count % 2) == 0) ? PlayerColor.White : PlayerColor.Black;
//                _mainVm.Moves.Add(move);

//                // Tính row index cho MoveEntries: row = (moveNumber - 1) / 2
//                int rowIndex = (move.MoveNumber - 1) / 2;

//                // Nếu đã có row tương ứng thì cập nhật, nếu chưa thì thêm
//                if (rowIndex < _mainVm.MoveEntries.Count)
//                {
//                    var entry = _mainVm.MoveEntries[rowIndex];
//                    if (move.Player == PlayerColor.White)
//                        entry.White = move.MoveNotation;
//                    else
//                        entry.Black = move.MoveNotation;
//                }
//                else
//                {
//                    // nếu cần bổ sung các hàng trống trước rowIndex (hiếm khi xảy ra), thêm các placeholder
//                    while (_mainVm.MoveEntries.Count < rowIndex)
//                    {
//                        _mainVm.MoveEntries.Add(new MoveEntry { Number = _mainVm.MoveEntries.Count + 1, White = "", Black = "" });
//                    }

//                    // tạo hàng mới
//                    var newEntry = new MoveEntry
//                    {
//                        Number = rowIndex + 1,
//                        White = move.Player == PlayerColor.White ? move.MoveNotation : "",
//                        Black = move.Player == PlayerColor.Black ? move.MoveNotation : ""
//                    };
//                    _mainVm.MoveEntries.Add(newEntry);
//                }
//            });

//            // --- 3) Convert SAN -> UCI (background) ---
//            string uci = string.Empty;
//            try
//            {
//                // snapshot of move notations on UI thread
//                var existingNotations = await dispatcher.InvokeAsync(() => _mainVm.Moves.Select(m => m.MoveNotation).ToList()).Task;

//                uci = SanToUciConverter.ConvertSanToUci(moveSan, existingNotations, startingFen: "startpos");

//                if (string.IsNullOrWhiteSpace(uci) || !IsUciMove(uci))
//                {
//                    _logger.Warn($"SAN->UCI conversion invalid: {moveSan} -> UCI='{uci}'");
//                    return;
//                }

//                // update move notation to UCI on UI thread
//                await dispatcher.InvokeAsync(() => move.MoveNotation = uci);
//                _logger.Info($"Converted SAN -> UCI: {moveSan} -> {uci}");
//            }
//            catch (Exception ex)
//            {
//                _logger.Error($"Error converting SAN to UCI: {ex.Message}");
//                return;
//            }

//            // --- 4) Save AFTER convert (so persistence gets UCI) ---
//            if (!string.IsNullOrEmpty(CurrentGameId))
//            {
//                try
//                {
//                    await _gamePersistence.SaveGameAsync(CurrentGameId, MyColor, _mainVm.Moves);
//                    _logger.Info($"Saved game {CurrentGameId} ({_mainVm.Moves.Count} moves)");
//                }
//                catch (Exception ex)
//                {
//                    _logger.Error($"Error saving game {CurrentGameId}: {ex.Message}");
//                    // do not abort; continue to try to show the move
//                }
//            }

//            // --- 5) Update board (UI thread) ---
//            try
//            {
//                var from = uci.Substring(0, 2);
//                var to = uci.Substring(2, 2);

//                await dispatcher.InvokeAsync(() =>
//                {
//                    if (App.Current?.MainWindow is MainWindow mainWindow)
//                    {
//                        mainWindow.PerformOpponentMove(from, to); // uses Dispatcher internally
//                    }
//                    else
//                    {
//                        _logger.Warn("MainWindow chưa sẵn sàng để perform move.");
//                    }
//                });

//                _logger.Info($"Opponent move performed: {uci}");
//            }
//            catch (Exception ex)
//            {
//                _logger.Error($"Error performing move on UI: {ex.Message}");
//            }

//            // --- 6) If this was a live opponent move (not initial replay), make it player's turn ---
//            if (!isReplay)
//            {
//                await dispatcher.InvokeAsync(() =>
//                {
//                    // If the move we just applied was opponent's, then it's player's turn.
//                    // Determine whether the move we applied belonged to opponent:
//                    bool lastWasOpponent = (move.Player != MyColor);
//                    _mainVm.IsPlayerTurn = lastWasOpponent ? true : _mainVm.IsPlayerTurn;
//                    _mainVm.RecommendedMove = lastWasOpponent ? "Đến lượt bạn." : _mainVm.RecommendedMove;
//                });
//            }
//        }

//        /// <summary>
//        /// Nếu tất cả ChessMove.MoveNotation có dạng UCI (ví dụ 'e2e4' hoặc 'e7e8q'),
//        /// thì nối lại thành chuỗi như \"e2e4 e7e5 g1f3\" trả về true.
//        /// Nếu có SAN (ví dụ 'Nf3', 'e4'), trả về null + allAreUci=false.
//        /// </summary>
//        private string? BuildUciMovesString(System.Collections.ObjectModel.ObservableCollection<ChessMove> moves, out bool allAreUci)
//        {
//            var parts = new List<string>();
//            allAreUci = true;

//            foreach (var m in moves)
//            {
//                var s = (m?.MoveNotation ?? "").Trim();
//                if (string.IsNullOrEmpty(s))
//                {
//                    allAreUci = false;
//                    break;
//                }

//                // UCI move format: from( a-h)(1-8) + to(a-h)(1-8) + optional promotion [qrbn]
//                // Examples: e2e4, e7e8q
//                if (IsUciMove(s))
//                {
//                    parts.Add(s);
//                }
//                else
//                {
//                    allAreUci = false;
//                    break;
//                }
//            }

//            return allAreUci ? string.Join(" ", parts) : null;
//        }

//        private bool IsUciMove(string s)
//        {
//            if (string.IsNullOrEmpty(s)) return false;
//            // length 4: e2e4 ; length 5: e7e8q (promotion)
//            if (s.Length != 4 && s.Length != 5) return false;
//            // from square
//            char f1 = s[0], f2 = s[1], t1 = s[2], t2 = s[3];
//            bool ok = (f1 >= 'a' && f1 <= 'h') && (f2 >= '1' && f2 <= '8')
//                   && (t1 >= 'a' && t1 <= 'h') && (t2 >= '1' && t2 <= '8');
//            if (!ok) return false;
//            if (s.Length == 5)
//            {
//                char promo = s[4];
//                return "qrbn".Contains(char.ToLower(promo));
//            }
//            return true;
//        }
//    }
//}


namespace MyGames.Desktop.Services
{
    public class ChessGameService
    {
        private readonly StockfishService _stockfish;
        private readonly LoggerService _logger;
        private readonly GamePersistenceService _gamePersistence;
        private readonly MainWindowViewModel _mainVm;

        public string? CurrentGameId { get; private set; }
        public PlayerColor MyColor { get; private set; } = PlayerColor.White;

        private readonly Queue<(string move, int index, bool isReplay)> _moveQueue = new();
        private bool _isProcessingQueue = false;
        private bool _isReplayingInitialMoves = false;

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

        public async Task HandleNotificationAsync(JsonElement payload)
        {
            if (!payload.TryGetProperty("type", out var typeProp))
                return;

            string type = typeProp.GetString() ?? string.Empty;
            _logger.Information($"Received notification type={type}");

            switch (type)
            {
                case "game_started":
                    {
                        var id = payload.GetProperty("gameId").GetString() ?? Guid.NewGuid().ToString();
                        var sideStr = payload.GetProperty("side").GetString() ?? "white";

                        var moves = new List<string>();
                        if (payload.TryGetProperty("currentMoves", out var arr) && arr.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var m in arr.EnumerateArray())
                            {
                                var uci = m.GetString();
                                if (!string.IsNullOrWhiteSpace(uci))
                                    moves.Add(uci);
                            }
                        }

                        await StartNewGameAsync(id, sideStr, moves);
                    }
                    break;

                case "move_uci": // 🩵 NEW
                    {
                        string moveUci = payload.GetProperty("move").GetString() ?? "";
                        int moveIndex = payload.TryGetProperty("moveIndex", out var idx) && idx.TryGetInt32(out var val)
                            ? val : -1;
                        await EnqueueMoveAsync(moveUci, moveIndex);
                    }
                    break;

                default:
                    _logger.Warning($"Unknown notification type: {type}");
                    break;
            }
        }

        private async Task StartNewGameAsync(string gameId, string side, List<string> existingMoves)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                _logger.Warn("Dispatcher không sẵn sàng, bỏ qua StartNewGameAsync.");
                return;
            }

            CurrentGameId = gameId;
            MyColor = side.Equals("black", StringComparison.OrdinalIgnoreCase)
                ? PlayerColor.Black
                : PlayerColor.White;

            await dispatcher.InvokeAsync(() =>
            {
                _mainVm.ResetGame();
                _mainVm.Moves.Clear();
                _mainVm.MoveEntries.Clear();

                _mainVm.CurrentSide = MyColor;
                _mainVm.PlayerColorProperty = MyColor;
                _mainVm.IsPlayerWhite = (MyColor == PlayerColor.White);
                _mainVm.SelectedColorIndex = (MyColor == PlayerColor.White) ? 1 : 2;

                _mainVm.GameStatus = $"Game {gameId} started. You are {MyColor}.";
                _mainVm.IsPlayerTurn = (MyColor == PlayerColor.White);
            });

            _logger.Info($"New game {gameId} ({side}) initialized with {existingMoves.Count} existing moves.");

            _isReplayingInitialMoves = existingMoves.Count > 0;
            int idx = 0;
            foreach (var uci in existingMoves)
            {
                _moveQueue.Enqueue((uci, idx++, true));
            }

            _ = ProcessMoveQueueAsync();
        }

        private async Task EnqueueMoveAsync(string moveUci, int moveIndex)
        {
            if (string.IsNullOrWhiteSpace(moveUci)) return;

            _moveQueue.Enqueue((moveUci, moveIndex, false));
            _logger.Info($"Queued move: {moveUci} (idx={moveIndex})");

            if (!_isProcessingQueue)
                await ProcessMoveQueueAsync();
        }

        private async Task ProcessMoveQueueAsync()
        {
            if (_isProcessingQueue) return;
            _isProcessingQueue = true;

            while (_moveQueue.Count > 0)
            {
                var (move, index, isReplay) = _moveQueue.Dequeue();
                await OnMoveReceivedAsync(move, isReplay);
                await Task.Delay(80);
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    var movesCount = _mainVm.Moves.Count;
                    var nextSide = (movesCount % 2 == 0) ? PlayerColor.White : PlayerColor.Black;
                    _mainVm.IsPlayerTurn = (MyColor == nextSide);
                    _mainVm.RecommendedMove = _mainVm.IsPlayerTurn ? "(your turn)" : "(opponent’s turn)";
                });
            }

            _isProcessingQueue = false;
        }

        // 🩵 NEW VERSION – dùng UCI trực tiếp
        private async Task OnMoveReceivedAsync(string moveUci, bool isReplay)
        {
            if (!IsUciMove(moveUci))
            {
                _logger.Warn($"Invalid UCI move: {moveUci}");
                return;
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            var move = new ChessMove
            {
                MoveNotation = moveUci,
                Timestamp = DateTime.Now,
            };

            await dispatcher.InvokeAsync(() =>
            {
                move.MoveNumber = _mainVm.Moves.Count + 1;
                move.Player = ((_mainVm.Moves.Count % 2) == 0) ? PlayerColor.White : PlayerColor.Black;
                _mainVm.Moves.Add(move);

                int rowIndex = (move.MoveNumber - 1) / 2;
                if (rowIndex < _mainVm.MoveEntries.Count)
                {
                    var entry = _mainVm.MoveEntries[rowIndex];
                    if (move.Player == PlayerColor.White)
                        entry.White = move.MoveNotation;
                    else
                        entry.Black = move.MoveNotation;
                }
                else
                {
                    var newEntry = new MoveEntry
                    {
                        Number = rowIndex + 1,
                        White = move.Player == PlayerColor.White ? move.MoveNotation : "",
                        Black = move.Player == PlayerColor.Black ? move.MoveNotation : ""
                    };
                    _mainVm.MoveEntries.Add(newEntry);
                }
            });

            // --- Thực hiện trên bàn cờ ---
            try
            {
                string from = moveUci.Substring(0, 2);
                string to = moveUci.Substring(2, 2);

                await dispatcher.InvokeAsync(() =>
                {
                    if (App.Current?.MainWindow is MainWindow win)
                    {
                        // 🩵 Replay thì cứ perform cả hai bên
                        win.PerformOpponentMove(from, to);
                    }
                });

                _logger.Info($"Board move executed: {moveUci}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error performing move {moveUci}: {ex.Message}");
            }

            // --- Cập nhật lượt đi ---
            if (!isReplay)
            {
                await dispatcher.InvokeAsync(() =>
                {
                    bool lastWasOpponent = (move.Player != MyColor);
                    _mainVm.IsPlayerTurn = lastWasOpponent;
                    _mainVm.RecommendedMove = lastWasOpponent ? "Đến lượt bạn." : _mainVm.RecommendedMove;
                });
            }
        }

        private bool IsUciMove(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Length != 4 && s.Length != 5) return false;
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