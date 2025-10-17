using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyGames.Desktop.Controls
{
    public partial class ChessBoardControl : UserControl
    {
        private Button? _selectedCell;
        private readonly int _rows = 8;
        private readonly int _cols = 8;
        private readonly Dictionary<string, Button> _cells = new();

        // === Sự kiện cho phép ViewModel/MainWindow bắt nước đi ===
        public event EventHandler<MoveSelectedEventArgs>? MoveSelected;

        public ChessBoardControl()
        {
            InitializeComponent();
            DrawBoard();
        }

        /// <summary>
        /// Tạo bàn cờ 8x8 xen kẽ màu trắng và nâu
        /// </summary>
        private void DrawBoard()
        {
            BoardGrid.Children.Clear();
            BoardGrid.Rows = _rows;
            BoardGrid.Columns = _cols;
            _cells.Clear();

            for (int r = 0; r < _rows; r++)
            {
                for (int c = 0; c < _cols; c++)
                {
                    var coord = $"{(char)('a' + c)}{8 - r}";
                    var cell = new Button
                    {
                        Tag = coord,
                        Background = (r + c) % 2 == 0 ? Brushes.Beige : Brushes.SaddleBrown,
                        BorderThickness = new Thickness(0.5),
                        Margin = new Thickness(0),
                        FontSize = 20
                    };
                    cell.Click += OnCellClicked;
                    BoardGrid.Children.Add(cell);
                    _cells[coord] = cell;
                }
            }
        }

        private void OnCellClicked(object sender, RoutedEventArgs e)
        {
            if (sender is not Button cell) return;

            // Nếu chưa chọn ô nào thì chọn ô đầu
            if (_selectedCell == null)
            {
                _selectedCell = cell;
                _selectedCell.BorderBrush = Brushes.Red;
                _selectedCell.BorderThickness = new Thickness(3);
                return;
            }

            // Nếu đã chọn rồi, đây là click thứ 2
            string from = _selectedCell.Tag.ToString()!;
            string to = cell.Tag.ToString()!;

            // Phát sự kiện MoveSelected
            MoveSelected?.Invoke(this, new MoveSelectedEventArgs(from, to));

            // Reset highlight
            _selectedCell.BorderBrush = null;
            _selectedCell.BorderThickness = new Thickness(0.5);
            _selectedCell = null;
        }

        /// <summary>
        /// Highlight the last move (both from and to squares). Use RemoveLastHighlight() to clear.
        /// </summary>
        public void HighlightLastMove(string from, string to)
        {
            RemoveLastHighlight();

            if (_cells.TryGetValue(from, out var bFrom))
            {
                bFrom.Background = Brushes.Yellow;
            }
            if (_cells.TryGetValue(to, out var bTo))
            {
                bTo.Background = Brushes.Yellow;
            }
        }

        /// <summary>
        /// Restore original colors (simple approach).
        /// </summary>
        public void RemoveLastHighlight()
        {
            foreach (var kv in _cells)
            {
                var coord = kv.Key;
                var btn = kv.Value;
                // recompute default color
                int file = coord[0] - 'a';
                int rank = '8' - coord[1]; // r index
                var defaultBrush = ((file + rank) % 2 == 0) ? Brushes.Beige : Brushes.SaddleBrown;
                btn.Background = defaultBrush;
            }
        }
    }

    /// <summary>
    /// EventArgs chứa thông tin nước đi
    /// </summary>
    public class MoveSelectedEventArgs : EventArgs
    {
        public string From { get; }
        public string To { get; }

        public MoveSelectedEventArgs(string from, string to)
        {
            From = from;
            To = to;
        }
    }
}
