using MyGames.Desktop.Controls;
using MyGames.Desktop.ViewModels;
using System.Windows;

namespace MyGames.Desktop
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _vm;

        public MainWindow(MainWindowViewModel vm)
        {
            InitializeComponent();

            // Gắn ViewModel
            _vm = vm ?? throw new System.ArgumentNullException(nameof(vm));
            DataContext = _vm;

            // Lắng nghe sự kiện khi người chơi click 2 ô (1 nước đi)
            ChessBoard.MoveSelected += OnMoveSelected;
        }

        private void OnMoveSelected(object? sender, MoveSelectedEventArgs e)
        {
            // Khi người chơi chọn đủ 2 ô (ví dụ: a2 → a3)
            _vm.AddMove(e.From, e.To);
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
