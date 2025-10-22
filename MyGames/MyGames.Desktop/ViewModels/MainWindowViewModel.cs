using MyGames.Desktop.Controls;
using MyGames.Desktop.Helpers;
using MyGames.Desktop.Logs;
using MyGames.Desktop.Models;
using MyGames.Desktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using static MyGames.Desktop.Models.ChessPiece;

namespace MyGames.Desktop.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        // --- Fields ---
        private string _gameStatus = "Chưa bắt đầu";
        private PlayerColor _currentSide = PlayerColor.White;
        private string _recommendedMove = string.Empty;
        private string _playerColorText = "Chưa chọn";
        private PlayerColor _playerColorProperty = PlayerColor.None;

        // --- Collections ---
        public ObservableCollection<ChessMove> Moves { get; } = new();
        public ObservableCollection<string> MoveHistory { get; } = new();
        public ObservableCollection<string> LogLines { get; } = new();

        // New: MoveEntries for two-column move list (white / black)
        public ObservableCollection<MoveEntry> MoveEntries { get; } = new();

        // Trạng thái bàn cờ
        public ChessBoardState Board { get; }

        private string? _selectedSquare; // lưu ô đầu tiên khi người chơi click

        // --- Properties ---
        public string GameStatus
        {
            get => _gameStatus;
            set { _gameStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusMessage)); }
        }

        public PlayerColor CurrentSide
        {
            get => _currentSide;
            set { _currentSide = value; OnPropertyChanged(); OnPropertyChanged(nameof(PlayerColor)); }
        }

        public string RecommendedMove
        {
            get => _recommendedMove;
            set { _recommendedMove = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => GameStatus;
            set { GameStatus = value; }
        }

        public string PlayerColorText
        {
            get => _playerColorText;
            set { _playerColorText = value; OnPropertyChanged(); }
        }
        public PlayerColor PlayerColorProperty
        {
            get => _playerColorProperty;
            set
            {
                if (_playerColorProperty != value)
                {
                    _playerColorProperty = value;
                    OnPropertyChanged();
                    CurrentSide = value; // đồng bộ
                    OnPropertyChanged(nameof(IsBlackPlayer));

                }
            }

        }

        private bool _isPlayerWhite = true;
        private bool _isPlayerTurn = true;
        public bool IsPlayerWhite
        {
            get => _isPlayerWhite;
            set
            {
                if (_isPlayerWhite != value)
                {
                    _isPlayerWhite = value;
                    PlayerColorProperty = value ? PlayerColor.White : PlayerColor.Black;
                    PlayerColorText = value ? "Trắng (đi trước)" : "Đen (đi sau)";
                    OnPropertyChanged();
                }
            }
        }

        public bool IsPlayerTurn
        {
            get => _isPlayerTurn;
            set { _isPlayerTurn = value; OnPropertyChanged(); }
        }

        // Bổ sung danh sách chọn màu ở ComboBox - giờ gồm mục "Chưa chọn" ở index 0
        public List<string> PlayerColorOptions { get; } = new()
        {
            "Chưa chọn",            // index 0 = chưa chọn
            "Trắng (đi trước)",     // index 1 = White
            "Đen (đi sau)"         // index 2 = Black
        };

        private int _selectedColorIndex = 0; // default: chưa chọn
        public int SelectedColorIndex
        {
            get => _selectedColorIndex;
            set
            {
                if (_selectedColorIndex == value) return; // không làm gì nếu không thay đổi
                _selectedColorIndex = value;
                OnPropertyChanged();

                // Map index -> trạng thái
                switch (value)
                {
                    case 0:
                        // Chưa chọn
                        PlayerColorProperty = PlayerColor.None;   // cần enum có None
                        PlayerColorText = "Chưa chọn";
                        // không thay đổi IsPlayerWhite, giữ mặc định; nhưng lượt chưa bắt đầu
                        IsPlayerTurn = false;

                        break;

                    case 1:
                        // Người chơi chọn Trắng (đi trước)
                        IsPlayerWhite = true;
                        PlayerColorProperty = PlayerColor.White;
                        PlayerColorText = "Trắng (đi trước)";
                        IsPlayerTurn = true; // nếu bạn là trắng -> đi trước

                        break;

                    case 2:
                        // Người chơi chọn Đen (đi sau)
                        IsPlayerWhite = false;
                        PlayerColorProperty = PlayerColor.Black;
                        PlayerColorText = "Đen (đi sau)";
                        IsPlayerTurn = false; // nếu bạn là đen -> không đi trước

                        break;

                    default:
                        // trong trường hợp giá trị bất thường, fallback về Chưa chọn
                        PlayerColorProperty = PlayerColor.None;
                        PlayerColorText = "Chưa chọn";
                        IsPlayerTurn = false;

                        break;
                }
            }
        }

        public bool IsBlackPlayer => PlayerColorProperty == PlayerColor.Black;

        /// <summary>
        /// Xử lý khi đối thủ đi (Extension gửi thông tin, hoặc người dùng tự kéo)
        /// </summary>
        public void ProcessOpponentMove(string from, string to)
        {
            _logger.Info($"Đối thủ đi {from} → {to}");

            // nếu ván đã kết thúc thì ignore
            if (Board.IsGameOver)
            {
                _logger.Warn("ProcessOpponentMove: Ván cờ đã kết thúc, bỏ qua move từ opponent.");
                return;
            }

            // dùng TryMakeMove để kiểm tra hợp lệ theo luật và lượt đi
            var moved = Board.TryMakeMove(from, to, isOpponent: true);
            if (!moved)
            {
                _logger.Warn($"Nước đi đối thủ không hợp lệ theo luật: {from} → {to}");
                StatusMessage = $"WARN::Đối thủ đi sai luật: {from} → {to}";
                return;
            }

            // Tạo và thêm ChessMove cho đối thủ
            var opponentColor = IsPlayerWhite ? PlayerColor.Black : PlayerColor.White;
            var move = new ChessMove
            {
                MoveNumber = Moves.Count + 1,
                MoveNotation = $"{from}{to}",
                Timestamp = DateTime.Now,
                Player = opponentColor
            };

            Moves.Add(move);
            MoveHistory.Add($"{move.MoveNumber}. {(opponentColor == PlayerColor.White ? "Trắng" : "Đen")}: {move.MoveNotation}");

            // update pair list for UI
            UpdateMoveEntries(move.MoveNotation, opponentColor);

            if (moved)
            {
                PlayMoveSound();
            }

            // ✅ Kiểm tra kết thúc ván
            // Nếu sau nước đi đối thủ ván kết thúc => xử lý kết quả và dừng ở đây
            if (Board.IsGameOver)
            {
                switch (Board.GameResult)
                {
                    case GameResult.WhiteWins:
                        StatusMessage = "✅ Trắng thắng (Đối thủ thua).";
                        break;
                    case GameResult.BlackWins:
                        StatusMessage = "✅ Đen thắng (Đối thủ thua).";
                        break;
                    case GameResult.Draw:
                        StatusMessage = "🤝 Hòa cờ.";
                        break;
                    default:
                        StatusMessage = "Ván cờ kết thúc.";
                        break;
                }
                _logger.Info($"Ván cờ kết thúc sau move opponent: {Board.GameResult}");
                // đảm bảo UI cập nhật bàn/hiệu ứng
                //Application.Current.Dispatcher.Invoke(() =>
                //{
                //    if (Application.Current.MainWindow is MainWindow win)
                //    {
                //        win.ChessBoard.RefreshBoard();
                //        if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                //            win.ChessBoard.HighlightLastMove(from, to);
                //    }
                //});

                return;
            }

            StatusMessage = $"Đối thủ vừa đi: {from} → {to}";
            _logger.Info($"Đã cập nhật (opponent) move #{move.MoveNumber}: {move.MoveNotation}");

            // Bây giờ đến lượt người chơi
            IsPlayerTurn = true;

            // refresh UI bàn — giữ hành vi cũ: highlight & refresh
            //Application.Current.Dispatcher.Invoke(() =>
            //{
            //    if (Application.Current.MainWindow is MainWindow win)
            //    {
            //        win.ChessBoard.RefreshBoard();
            //        win.ChessBoard.HighlightLastMove(from, to);
            //    }
            //});
        }

        public void PlayMoveSound()
        {
            if (!_isSoundEnabled) return;

            try
            {
                // Giải pháp 1: nếu bạn có file WAV (ví dụ "move.wav" nằm cạnh .exe)
                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "move.wav");
                if (System.IO.File.Exists(path))
                {
                    using var player = new System.Media.SoundPlayer(path);
                    player.Play();
                }
                else
                {
                    // Giải pháp 2: fallback, tạo beep nhẹ
                    Console.Beep(700, 100);
                }
            }
            catch { /* tránh crash nếu lỗi */ }
        }

        /// <summary>
        /// Gọi Stockfish và cho app tự đi nước cờ tốt nhất cho người chơi
        /// </summary>
        public async Task AutoPlayBestMoveForPlayer(ChessBoardControl board)
        {
            try
            {
                if (Board.IsGameOver)
                {
                    _logger.Warn("AutoPlayBestMoveForPlayer: Ván cờ đã kết thúc, AI không thể đi.");
                    return;
                }

                var movesUci = string.Join(" ", Moves.Select(m => m.MoveNotation));
                // Lấy output (dòng chứa "bestmove ...")
                string stockfishOutput = await _stockfishService.GetBestMoveAsync(movesUci, depth: 12);

                // Parse ra UCI move (ví dụ "e2e4" hoặc "e7e8q")
                string bestMoveUci = ParseBestMoveFromStockfishOutput(stockfishOutput);
                if (string.IsNullOrEmpty(bestMoveUci) || bestMoveUci.Length < 4)
                {
                    StatusMessage = "ERROR::Không có nước gợi ý hợp lệ.";
                    _logger.Warn("Stockfish không trả về bestmove hợp lệ.");
                    return;
                }

                string from = bestMoveUci.Substring(0, 2);
                string to = bestMoveUci.Substring(2, 2);

                // chỉ thực hiện nếu hợp lệ theo luật
                if (!Board.IsMoveLegal(from, to, _isPlayerTurn))
                {
                    _logger.Warn($"AI move {bestMoveUci} không hợp lệ theo luật hiện tại.");
                    StatusMessage = $"AI không thể đi {bestMoveUci}.";
                    return;
                }

                bool moved = Board.TryMakeMove(from, to);
                if (!moved)
                {
                    _logger.Warn($"AI move bị từ chối bởi TryMakeMove: {bestMoveUci}");
                    StatusMessage = $"AI đi {bestMoveUci} nhưng không thể áp dụng lên bàn.";
                    return;
                }
                await Task.Delay(1000); // delay nhỏ để người dùng thấy chuyển động

                // Thêm vào Moves / MoveHistory
                var aiMove = new ChessMove
                {
                    MoveNumber = Moves.Count + 1,
                    MoveNotation = bestMoveUci,
                    Timestamp = DateTime.Now,
                    Player = PlayerColorProperty
                };
                Moves.Add(aiMove);
                MoveHistory.Add($"{aiMove.MoveNumber}. {PlayerColorProperty}: {aiMove.MoveNotation}");

                UpdateMoveEntries(bestMoveUci, PlayerColorProperty);

                RecommendedMove = bestMoveUci;
                _logger.Info($"AI (as {PlayerColorProperty}) đi {bestMoveUci} (auto-play).");

                // Cập nhật UI: refresh + highlight (chạy trên UI thread)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    board.RefreshBoard();
                    board.HighlightLastMove(from, to);
                });


                // ✅ Kiểm tra kết thúc ván
                if (Board.IsGameOver)
                {
                    switch (Board.GameResult)
                    {
                        case GameResult.WhiteWins:
                            StatusMessage = "✅ Trắng thắng!";
                            break;
                        case GameResult.BlackWins:
                            StatusMessage = "✅ Đen thắng!";
                            break;
                        case GameResult.Draw:
                            StatusMessage = "🤝 Hòa cờ!";
                            break;
                        default:
                            StatusMessage = "Ván cờ kết thúc.";
                            break;
                    }
                    _logger.Info($"Ván cờ kết thúc sau AI move: {Board.GameResult}");
                    return;
                }


                // Sau AI di chuyển, chuyển lượt sang đối thủ
                IsPlayerTurn = false;
                StatusMessage = "Đối thủ đang đi...";
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Lỗi khi auto-play: {ex.Message}");
                StatusMessage = "Lỗi khi auto-play.";
            }
        }

        // wrapper to call stockfish GetBestMoveAsync safely (we keep implementation call unchanged)
        private async Task<string> _stockfish_service_guard(string movesUci)
        {
            // reuse existing _stockfishService logic:
            return await _stockfishService.GetBestMoveAsync(movesUci, depth: 12);
        }

        /// <summary>
        /// Parse dòng output của Stockfish để lấy UCI bestmove.
        /// Hỗ trợ các dạng:
        /// - "bestmove e2e4"
        /// - "bestmove e2e4 ponder e7e5"
        /// - hoặc nếu Stockfish trả cả blocks nhiều dòng thì tìm dòng bắt đầu bằng "bestmove".
        /// </summary>
        private string ParseBestMoveFromStockfishOutput(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return string.Empty;

            // Nếu output có nhiều dòng, xét từng dòng
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var ln in lines)
            {
                var trimmed = ln.Trim();
                if (trimmed.StartsWith("bestmove", StringComparison.OrdinalIgnoreCase))
                {
                    // ex: "bestmove e2e4 ponder e7e5"
                    var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                        return parts[1].Trim();
                }
            }

            // fallback: nếu input chính là một UCI move
            var single = output.Trim();
            if (single.Length >= 4 && char.IsLetter(single[0]) && char.IsDigit(single[1]) && char.IsLetter(single[2]) && char.IsDigit(single[3]))
                return single.Substring(0, Math.Min(5, single.Length)); // may be promotion

            return string.Empty;
        }

        private bool _isSoundEnabled = true;
        public bool IsSoundEnabled
        {
            get => _isSoundEnabled;
            set { _isSoundEnabled = value; OnPropertyChanged(); }
        }

        private bool _isAutoPlayEnabled = false;
        public bool IsAutoPlayEnabled
        {
            get => _isAutoPlayEnabled;
            set { _isAutoPlayEnabled = value; OnPropertyChanged(); }
        }

        // --- Commands ---
        public ICommand ResetGameCommand { get; }
        public ICommand AnalyzeCommand { get; }

        // --- Services (DI injected) ---
        private readonly StockfishService _stockfishService;
        private readonly LoggerService _logger;
        private readonly AppSettings _appSettings;

        // --- Constructor (DI) ---
        public MainWindowViewModel(StockfishService stockfishService, LoggerService logger, AppSettings appSettings)
        {
            _stockfishService = stockfishService;
            _logger = logger;

            Board = new ChessBoardState();

            ResetGameCommand = new RelayCommand(_ => ResetGame());
            AnalyzeCommand = new RelayCommand(async _ => await OnAnalyzeRequested());

            _logger.LogAppended += line =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    LogLines.Add(line);
                    if (LogLines.Count > 300) // tránh tràn bộ nhớ
                        LogLines.RemoveAt(0);
                });
            };

            this._appSettings = appSettings;

            // Đã có dùng Lazy start trong AnalyzeBoardAsync
            //try
            //{
            //    _stockfishService.Start(_appSettings.StockfishPath);
            //}
            //catch (Exception ex)
            //{
            //    _logger.Error(ex, "Không thể start Stockfish tự động.");
            //}
        }

        // --- Methods ---

        /// <summary>
        /// Xử lý việc người chơi thực hiện nước đi.
        /// Trả về true nếu nước đi được thực hiện thành công (hợp lệ theo ChessBoardState).
        /// </summary>
        public bool AddMove(string from, string to, PlayerColor player = PlayerColor.White)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                return false;

            _logger.Info($"Người chơi chọn {from} → {to}");

            // Nếu người chơi chưa chọn phe trên UI (PlayerColor.None) -> reject
            if (PlayerColorProperty == PlayerColor.None)
            {
                _logger.Warn("Chưa chọn bên, không thể đi.");
                StatusMessage = "WARN::Chưa chọn bên, không thể đi.";
                return false;
            }

            if (Board.IsGameOver)
            {
                _logger.Warn("AddMove: Ván cờ đã kết thúc, không thể đi thêm.");
                return false;
            }

            // Kiểm tra lượt đi đúng: board giữ CurrentTurn (PieceColor)
            // Quy ước: nếu người dùng chọn IsPlayerWhite -> họ điều khiển màu trắng; ngược lại là đen.
            var expectedPieceColor = IsPlayerWhite ? PieceColor.White : PieceColor.Black;
            if (Board.CurrentTurn != expectedPieceColor)
            {
                // Lưu ý: MainWindow đã set IsPlayerTurn; nhưng ở đây ta double check với board để an toàn.
                _logger.Warn("Chưa đến lượt của bạn.");
                StatusMessage = "WARN::Chưa đến lượt của bạn.";
                return false;
            }

            // Kiểm tra hợp lệ nước đi sử dụng ChessBoardState
            if (!Board.IsMoveLegal(from, to, _isPlayerTurn))
            {
                StatusMessage = $"WARN::Nước đi không hợp lệ: {from} → {to}";
                _logger.Warn($"Move bị từ chối: {from}→{to} không hợp lệ.");
                return false;
            }

            // Thực hiện di chuyển (TryMakeMove sẽ đổi lượt trên Board)
            bool moved = Board.TryMakeMove(from, to);
            if (!moved)
            {
                _logger.Warn($"Không thể di chuyển quân từ {from} đến {to} (TryMakeMove returned false).");
                return false;
            }

            // Nếu tới đây => move thành công, ghi vào history
            var notation = $"{from}{to}";
            var move = new ChessMove
            {
                MoveNumber = Moves.Count + 1,
                MoveNotation = notation,
                Timestamp = DateTime.Now,
                Player = player
            };

            Moves.Add(move);
            MoveHistory.Add($"{move.MoveNumber}. {player}: {notation} ({move.Timestamp:T})");

            UpdateMoveEntries(notation, player);

            if (moved)
            {
                PlayMoveSound();
            }

            // ✅ Kiểm tra kết thúc ván
            if (Board.IsGameOver)
            {
                switch (Board.GameResult)
                {
                    case GameResult.WhiteWins:
                        StatusMessage = "✅ Trắng thắng!";
                        break;
                    case GameResult.BlackWins:
                        StatusMessage = "✅ Đen thắng!";
                        break;
                    case GameResult.Draw:
                        StatusMessage = "🤝 Hòa!";
                        break;
                    default:
                        StatusMessage = "Ván cờ kết thúc.";
                        break;
                }
                _logger.Info($"Ván cờ kết thúc sau người chơi move: {Board.GameResult}");
                return true;
            }


            StatusMessage = $"Đã đi: {from} → {to}";
            RecommendedMove = "(đang tính...)";

            _logger.Info($"Đã di chuyển: {notation}");
            _logger.Info("Trạng thái bàn hiện tại:\n" + Board.ToString());

            // Sau người chơi đi, để logic ở MainWindow đặt IsPlayerTurn=false
            // và VM gọi engine phân tích tiếp theo:
            var movesUci = string.Join(" ", Moves.Select(m => m.MoveNotation));
            _ = AnalyzeBoardAsync(movesUci: movesUci, depth: 12);

            return true;
        }


        /// <summary>
        /// Dùng cho sự kiện click lần đầu (chọn ô)
        /// (nếu người chơi chọn 2 ô, hệ thống sẽ gọi AddMove có kiểm tra hợp lệ.)
        /// </summary>
        public void SelectSquare(string square)
        {
            if (_selectedSquare == null)
            {
                _selectedSquare = square;
                _logger.Info($"Chọn ô đầu tiên: {_selectedSquare}");
            }
            else
            {
                AddMove(_selectedSquare, square);
                _selectedSquare = null;
            }
        }

        public void AddMoveSan(string san, PlayerColor player = PlayerColor.White)
        {
            if (string.IsNullOrWhiteSpace(san)) return;

            // Ngăn trùng lặp
            if (Moves.Any(m => m.MoveNotation == san && m.Player == player))
                return;

            var move = new ChessMove
            {
                MoveNumber = Moves.Count + 1,
                MoveNotation = san,
                Timestamp = DateTime.Now,
                Player = player
            };

            Moves.Add(move);
            MoveHistory.Add($"{move.MoveNumber}. {player}: {san} ({move.Timestamp:T})");

            // update pair list
            UpdateMoveEntries(san, player);

            StatusMessage = $"Đã đi: {san}";

            // RecommendedMove = "(chờ SAN→UCI conversion...)";
            string uci = SanToUciConverter.ConvertSanToUci(san, new List<string>());
            RecommendedMove = uci; // ví dụ: g1f3
        }

        /// <summary>
        /// Phân tích bàn cờ bằng Stockfish (nếu chưa khởi động engine, sẽ tự start).
        /// </summary>
        public async Task AnalyzeBoardAsync(string? fen = null, string? movesUci = null, int depth = 12)
        {
            //if (_stockfishService == null)
            //{
            //    GameStatus = "Engine chưa sẵn sàng";
            //    return;
            //}

            try
            {
                _logger.Info("🧠 Bắt đầu AnalyzeBoardAsync()...");

                // 🔥 Lazy start engine nếu chưa chạy
                if (!_stockfish_service_check())
                {
                    _logger.Warn("⚙ Stockfish chưa chạy — tiến hành khởi động...");
                    _stockfishService.Start(_appSettings.StockfishPath);
                    await Task.Delay(300); // Cho Stockfish vài trăm ms để init
                }

                if (!_stockfishService.IsRunning)
                {
                    GameStatus = "❌ Không thể khởi động Stockfish.";
                    _logger.Error("❌ AnalyzeBoardAsync dừng — Stockfish vẫn chưa chạy sau khi Start().");
                    return;
                }

                // 🧩 Chuẩn bị FEN hoặc danh sách moves hiện tại
                _logger.Info($"📄 FEN hiện tại: {fen}");
                _logger.Info($"♟ Moves hiện tại: {movesUci}");
                _logger.Info($"🔍 Bắt đầu phân tích (depth={depth})...");

                string inputForEngine;
                if (!string.IsNullOrEmpty(fen))
                    inputForEngine = fen;
                else if (!string.IsNullOrEmpty(movesUci))
                    inputForEngine = movesUci;
                else
                {
                    _logger.Warn("⚠ Không có dữ liệu FEN hoặc moves để phân tích.");
                    GameStatus = "Không có vị trí để phân tích.";
                    return;
                }

                // 🧠 Gọi Stockfish
                string result = await _stockfishService.GetBestMoveAsync(
                    inputForEngine, depth, timeoutMs: 7000, null);

                // ⚙️ Xử lý kết quả
                if (string.IsNullOrWhiteSpace(result))
                {
                    _logger.Warn("⚠ Stockfish không phản hồi hoặc trả về rỗng.");
                    RecommendedMove = "(Chưa có gợi ý — engine im lặng)";
                    GameStatus = "Engine không phản hồi.";
                    OnPropertyChanged(nameof(RecommendedMove));
                    return;
                }

                // 🧩 Parse bestmove
                var bestMove = ParseBestMoveFromStockfishOutput(result);
                if (string.IsNullOrWhiteSpace(bestMove))
                {
                    // fallback: tự split thủ công (dự phòng)
                    bestMove = result.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                                     .SkipWhile(x => x != "bestmove")
                                     .Skip(1)
                                     .FirstOrDefault();
                }

                if (!string.IsNullOrWhiteSpace(bestMove))
                {
                    RecommendedMove = $"💡 Nước gợi ý: {bestMove}";
                    _logger.Info($"✅ Stockfish đề xuất: {bestMove}");
                }
                else
                {
                    RecommendedMove = "(Không xác định được nước gợi ý)";
                    _logger.Warn($"⚠ Không tìm thấy 'bestmove' trong output:\n{result}");
                }

                GameStatus = "Đã có gợi ý từ Stockfish.";
                OnPropertyChanged(nameof(RecommendedMove));
            }
            catch (Exception ex)
            {
                _logger.Error($"❌ Lỗi trong AnalyzeBoardAsync: {ex.Message}");
                GameStatus = "Phân tích thất bại (xem log để biết chi tiết).";
                RecommendedMove = "Lỗi khi phân tích bàn cờ";
                OnPropertyChanged(nameof(RecommendedMove));
            }
        }

        // small wrappers for clarity / keep original logic names
        private bool _stockfish_service_check()
        {
            if (_stockfishService == null)
            {
                _logger.Error("⚠ _stockfishService == null (chưa được inject hoặc chưa khởi tạo).");
                return false;
            }

            if (!_stockfishService.IsRunning)
            {
                _logger.Warn("⚠ StockfishService tồn tại nhưng engine chưa chạy.");
                return false;
            }

            return true;
        }


        /// <summary>
        /// Update MoveEntries collection (pair list) when a new move is added.
        /// This keeps MoveEntries in sync with Moves and MoveHistory.
        /// </summary>
        private void UpdateMoveEntries(string moveNotation, PlayerColor player)
        {
            // compute move number: (Moves.Count + 1) / 2  works as shared pair number
            int number = (Moves.Count + 1) / 2;

            if (player == PlayerColor.White)
            {
                // create a new entry with white move
                var entry = new MoveEntry
                {
                    Number = number,
                    White = moveNotation,
                    Black = string.Empty
                };
                MoveEntries.Add(entry);
            }
            else
            {
                // player is Black -> fill last entry's Black if possible, otherwise create
                if (MoveEntries.Count == 0)
                {
                    var entry = new MoveEntry
                    {
                        Number = number,
                        White = string.Empty,
                        Black = moveNotation
                    };
                    MoveEntries.Add(entry);
                }
                else
                {
                    var last = MoveEntries.Last();
                    if (string.IsNullOrEmpty(last.Black))
                    {
                        last.Black = moveNotation;
                        // notify change
                        var idx = MoveEntries.Count - 1;
                        MoveEntries[idx] = last;
                    }
                    else
                    {
                        var entry = new MoveEntry
                        {
                            Number = number,
                            White = string.Empty,
                            Black = moveNotation
                        };
                        MoveEntries.Add(entry);
                    }
                }
            }
        }

        private void ResetGame()
        {
            // 🧩 Reset lựa chọn màu người chơi trong ComboBox
            _selectedColorIndex = 0;
            SelectedColorIndex = 0;
            IsPlayerWhite = true;
            PlayerColorProperty = PlayerColor.None;
            PlayerColorText = "Chưa chọn";
            OnPropertyChanged(nameof(SelectedColorIndex));

            Moves.Clear();
            MoveHistory.Clear();
            MoveEntries.Clear();
            RecommendedMove = string.Empty;

            // 🧩 Đặt trạng thái ván chơi
            CurrentSide = PlayerColor.White;
            GameStatus = "Ván mới đã khởi động. Hãy chọn màu quân cờ.";
            IsPlayerTurn = false; // ⚠️ chưa chọn màu thì chưa đến lượt người chơi

            // 🧩 Reset trạng thái bàn cờ
            Board.Reset();
            OnPropertyChanged(nameof(Board)); // đảm bảo UI cập nhật lại bàn cờ

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is MainWindow win)
                    win.ChessBoard.ResetBoardDisplay();
            });
        }


        private async Task OnAnalyzeRequested()
        {
            // Build UCI moves string if Moves contain UCI moves
            var movesUci = BuildUciMovesStringForViewModel(out bool allUci);
            if (!allUci)
            {
                GameStatus = "Phân tích tạm hoãn — có SAN chưa convert (cần converter)";
                return;
            }

            await AnalyzeBoardAsync(fen: null, movesUci: movesUci, depth: 12);
        }

        private string? BuildUciMovesStringForViewModel(out bool allAreUci)
        {
            allAreUci = true;
            var parts = new List<string>();
            foreach (var m in Moves)
            {
                var s = (m?.MoveNotation ?? "").Trim();
                if (string.IsNullOrEmpty(s) || !IsUciMove(s))
                {
                    allAreUci = false;
                    break;
                }
                parts.Add(s);
            }
            return allAreUci ? string.Join(" ", parts) : null;
        }

        // reuse IsUciMove method (paste IsUciMove from earlier)
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

        public bool TryMakeMove(string from, string to, char? promotion = null)
        {
            if (Board.IsGameOver)
                return false;

            //bool success = Board.TryMakeMove(from, to, promotion ?? 'q');

            //if (success)
            //{
            //    if (Board.IsGameOver)
            //    {
            //        StatusMessage = Board.GameResult switch
            //        {
            //            GameResult.WhiteWins => "✅ Trắng thắng!",
            //            GameResult.BlackWins => "✅ Đen thắng!",
            //            _ => "🤝 Hòa!"
            //        };
            //    }
            //}

            //return success;


            char promo = promotion ?? 'q';

            bool success = Board.TryMakeMove(from, to, promo);
            if (!success) return false;

            // Build UCI move notation (with promotion if provided)
            string uci = from + to + (promotion.HasValue ? promotion.Value.ToString().ToLower() : "");

            var move = new ChessMove
            {
                MoveNumber = Moves.Count + 1,
                MoveNotation = uci,
                Timestamp = DateTime.Now,
                Player = PlayerColorProperty // or determine from Board state / previous side
            };
            Moves.Add(move);
            MoveHistory.Add($"{move.MoveNumber}. {PlayerColorProperty}: {move.MoveNotation}");

            UpdateMoveEntries(uci, move.Player);

            PlayMoveSound();

            // Refresh UI board & highlight last move
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Application.Current.MainWindow is MainWindow win)
                {
                    win.ChessBoard.RefreshBoard();
                    win.ChessBoard.HighlightLastMove(from, to);
                }
            });

            // Check game over
            if (Board.IsGameOver)
            {
                StatusMessage = Board.GameResult switch
                {
                    GameResult.WhiteWins => "✅ Trắng thắng!",
                    GameResult.BlackWins => "✅ Đen thắng!",
                    _ => "🤝 Hòa!"
                };

                // Optionally disable further moves UI etc.
            }
            else
            {
                // continue game: if auto analysis / autoplay etc., trigger
                IsPlayerTurn = (GetPieceColor(PlayerColorProperty) == Board.CurrentTurn);
            }

            return true;
        }
        private PieceColor GetPieceColor(PlayerColor player)
        {
            return player == PlayerColor.White ? PieceColor.White : PieceColor.Black;
        }


        // --- INotifyPropertyChanged ---
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>
    /// MoveEntry represents a single row in the move list with White/Black columns.
    /// </summary>
    public record struct MoveEntry(int Number, string White = "", string Black = "");
}
