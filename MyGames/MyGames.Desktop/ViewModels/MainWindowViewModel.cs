using MyGames.Desktop.Helpers;
using MyGames.Desktop.Logs;
using MyGames.Desktop.Models;
using MyGames.Desktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MyGames.Desktop.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        // --- Fields ---
        private string _gameStatus = "Chưa bắt đầu";
        private PlayerColor _currentSide = PlayerColor.White;
        private string _recommendedMove = string.Empty;
        private string _playerColorText = "Chưa chọn";

        // --- Collections ---
        public ObservableCollection<ChessMove> Moves { get; } = new();
        public ObservableCollection<string> MoveHistory { get; } = new();
        public ObservableCollection<string> LogLines { get; } = new();

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
        }

        // --- Methods ---

        public void AddMove(string from, string to, PlayerColor player = PlayerColor.White)
        {
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) return;

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

            StatusMessage = $"Đã đi: {from} → {to}";
            // RecommendedMove = "(chờ AI gợi ý...)";

            RecommendedMove = "(đang tính...)";

            // Gọi engine gợi ý nước tiếp theo
            var movesUci = string.Join(" ", Moves.Select(m => m.MoveNotation));
            _ = AnalyzeBoardAsync(movesUci: movesUci, depth: 12);
        }

        public void AddMoveSan(string san, PlayerColor player = PlayerColor.White)
        {
            if (string.IsNullOrWhiteSpace(san)) return;

            var move = new ChessMove
            {
                MoveNumber = Moves.Count + 1,
                MoveNotation = san,
                Timestamp = DateTime.Now,
                Player = player
            };

            Moves.Add(move);
            MoveHistory.Add($"{move.MoveNumber}. {player}: {san} ({move.Timestamp:T})");

            StatusMessage = $"Đã đi: {san}";
            RecommendedMove = "(chờ SAN→UCI conversion...)";
        }

        /// <summary>
        /// Phân tích bàn cờ bằng Stockfish (nếu chưa khởi động engine, sẽ tự start).
        /// </summary>
        public async Task AnalyzeBoardAsync(string? fen = null, string? movesUci = null, int depth = 12)
        {
            if (_stockfishService == null)
            {
                GameStatus = "Engine chưa sẵn sàng";
                return;
            }

            try
            {
                // 🔥 Lazy start engine nếu chưa chạy
                if (!_stockfishService.IsRunning)
                {
                    _stockfishService.Start(_appSettings.StockfishPath);
                }

                GameStatus = "Đang phân tích...";
                string result;

                if (!string.IsNullOrEmpty(fen))
                    result = await _stockfishService.GetBestMoveAsync(fen, depth);
                else if (!string.IsNullOrEmpty(movesUci))
                    result = await _stockfishService.GetBestMoveAsync(movesUci, depth);
                else
                {
                    GameStatus = "Không có vị trí để phân tích";
                    return;
                }

                RecommendedMove = result ?? "(Không có nước gợi ý)";
                GameStatus = "Đã có gợi ý";
            }
            catch (Exception ex)
            {
                GameStatus = $"Lỗi engine: {ex.Message}";
            }
        }

        private void ResetGame()
        {
            Moves.Clear();
            MoveHistory.Clear();
            GameStatus = "Đã khởi động lại ván mới.";
            RecommendedMove = string.Empty;
            PlayerColorText = "Chưa chọn";
            CurrentSide = PlayerColor.White;
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

        // --- INotifyPropertyChanged ---
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
