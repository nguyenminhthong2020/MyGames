using ChessApp.Views;
using MyGames.Desktop.Controls;
using MyGames.Desktop.Helpers;
using MyGames.Desktop.Models;
using MyGames.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using static MyGames.Desktop.Models.ChessPiece;

namespace MyGames.Desktop
{
    public partial class MainWindow : Window
    {
        private MainWindowViewModel? _vm;

        //public MainWindow() 
        //{
        //    InitializeComponent();

        //    // Gắn ViewModel
        //    _vm = ((App)Application.Current).Services.GetRequiredService<MainWindowViewModel>();
        //    DataContext = _vm;

        //    // Lắng nghe sự kiện khi người chơi click 2 ô (1 nước đi)
        //    ChessBoard.MoveSelected += OnMoveSelected;
        //}

        public MainWindow(MainWindowViewModel vm)
        {
            InitializeComponent();

            _vm = vm;
            DataContext = _vm;

            PlayerColorOptions.SelectionChanged += PlayerColorOptions_SelectionChanged;

            ChessBoard.MoveSelected += OnMoveSelected;

            ChessBoard.SetBoardState(_vm.Board, _vm.CurrentSide);
            _vm.Moves.CollectionChanged += (_, __) => ChessBoard.RefreshBoard();
            ChessBoard.PromotionRequired += OnPromotionRequired;

            // Subscribe để auto-scroll MoveListView khi có move mới
            if (_vm.MoveEntries != null)
            {
                _vm.MoveEntries.CollectionChanged += MoveEntries_CollectionChanged;
            }
        }

        // 2️⃣ Xử lý khi người dùng chọn "Trắng đi trước" / "Đen đi sau"
        private void PlayerColorOptions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            if (PlayerColorOptions.SelectedItem == null) return;

            var selected = PlayerColorOptions.SelectedItem.ToString();

            if (selected.Contains("Trắng"))
            {
                _vm.PlayerColorProperty = PlayerColor.White;
                ChessBoard.SetPlayerColorChosen(PlayerColor.White);
                _vm.StatusMessage = "Bạn là Trắng – đi trước.";
            }
            else if (selected.Contains("Đen"))
            {
                _vm.PlayerColorProperty = PlayerColor.Black;
                ChessBoard.SetPlayerColorChosen(PlayerColor.Black);
                _vm.StatusMessage = "Bạn là Đen – đi sau.";
            }
            else
            {
                _vm.PlayerColorProperty = PlayerColor.None;
                ChessBoard.SetPlayerColorChosen(PlayerColor.None);
                _vm.StatusMessage = "WARN::Chưa chọn phe.";
            }

            // Refresh hiển thị để cập nhật đúng quân được phép chọn
            ChessBoard.RefreshBoard();
        }

        private async void OnMoveSelected(object? sender, MoveSelectedEventArgs e)
        {
            if (_vm == null) return;

            // Nếu hiện tại là lượt của người chơi
            if (_vm.IsPlayerTurn)
            {
                // Gọi VM.AddMove và kiểm tra success
                bool ok = _vm.AddMove(e.From, e.To, _vm.PlayerColorProperty);

                if (ok)
                {
                    // Highlight last move on board
                    ChessBoard.HighlightLastMove(e.From, e.To);

                    // ⚠️ Kiểm tra nếu game đã kết thúc thì KHÔNG đổi lượt nữa
                    if (_vm.Board.IsGameOver)
                    {
                        // Đảm bảo hiển thị thông báo kết thúc đúng
                        switch (_vm.Board.GameResult)
                        {
                            case GameResult.WhiteWins:
                                _vm.StatusMessage = "OK::✅ Trắng thắng!";
                                break;
                            case GameResult.BlackWins:
                                _vm.StatusMessage = "OK::✅ Đen thắng!";
                                break;
                            case GameResult.Draw:
                                _vm.StatusMessage = "🤝 Hòa!";
                                break;
                        }

                        // Không đổi lượt, return luôn
                        return;
                    }

                    // Nếu chưa kết thúc, sang lượt đối thủ
                    _vm.IsPlayerTurn = false;
                    _vm.StatusMessage = "Đối thủ đang đi...";
                }
                else
                {
                    // move không hợp lệ/không thành công => chỉ clear selection trên board
                    ChessBoard.ClearSelectionHighlight();
                }
            }
            else
            {
                // Giả lập đối thủ đi (vì chưa có Extension)
                _vm.ProcessOpponentMove(e.From, e.To);

                ChessBoard.RefreshBoard();
                ChessBoard.HighlightLastMove(e.From, e.To);

                // Gọi AI tính và tự đi cho bạn
                if(_vm.IsAutoPlayEnabled)
                {
                    await _vm.AutoPlayBestMoveForPlayer(ChessBoard);
                }
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            
            ChessBoard.MoveSelected -= OnMoveSelected;
            PlayerColorOptions.SelectionChanged -= PlayerColorOptions_SelectionChanged;
        }

        private void MoveEntries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Scroll vào item cuối (chạy trên UI thread)
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MoveListView.Items.Count > 0)
                {
                    var last = MoveListView.Items[MoveListView.Items.Count - 1];
                    MoveListView.ScrollIntoView(last);
                }
            }));
        }
        private async void OnPromotionRequired(object? sender, PromotionEventArgs e)
        {
            var dialog = new PromotionDialog(e.IsWhite);
            dialog.Owner = this;

            if (dialog.ShowDialog() == true)
            {
                char promotionChar = dialog.SelectedPiece;
                _vm.TryMakeMove(e.From, e.To, promotionChar);
            }
        }

    }
}
