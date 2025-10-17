using Microsoft.Extensions.DependencyInjection;
using MyGames.Desktop.Controls;
using MyGames.Desktop.ViewModels;
using System.Windows;

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

            ChessBoard.MoveSelected += OnMoveSelected;

            ChessBoard.SetBoardState(_vm.Board, _vm.CurrentSide);
            _vm.Moves.CollectionChanged += (_, __) => ChessBoard.RefreshBoard();
        }

        private void OnMoveSelected(object? sender, MoveSelectedEventArgs e)
        {
            // Khi người chơi chọn đủ 2 ô (ví dụ: a2 → a3)
            _vm.AddMove(e.From, e.To);

            ChessBoard.RefreshBoard();

            // Highlight last move on board            
            ChessBoard.HighlightLastMove(e.From, e.To);
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            
            ChessBoard.MoveSelected -= OnMoveSelected;
        }
    }
}
