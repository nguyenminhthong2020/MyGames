using MyGames.Desktop.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MyGames.Desktop.Controls
{
    public partial class ChessBoardControl : UserControl
    {
        private readonly int _rows = 8;
        private readonly int _cols = 8;

        // Map square string -> Button
        //private readonly Dictionary<string, Button> _cells = new();

        // Selected cell for click-to-move behavior
        private Button? _selectedCell;

        // Board state that this control will render. Set from ViewModel.
        private ChessBoardState? _boardState;

        // Event to notify when a move (from->to) is selected by user (UCI-like squares)
        // === Sự kiện cho phép ViewModel/MainWindow bắt nước đi ===
        public event EventHandler<MoveSelectedEventArgs>? MoveSelected;

        public event EventHandler<string>? SquareClicked;

        private string? _lastFrom;
        private string? _lastTo;


        // Code V3, 4a
        /// <summary>
        /// // Map square string -> Button
        /// </summary>
        private readonly Dictionary<string, Button> _cells = new();
        private Button? _selectedButton;
        private PlayerColor _currentTurn = PlayerColor.White;

        public event Action<string>? SquareSelected;


        private Point _dragStart;
        private bool _isDragging;
        private Button? _dragSourceButton;

        public ChessBoardState BoardState { get; private set; } = new();

        private TextBlock? _dragGhost; // 👈 hiển thị quân cờ bay theo chuột

        public ChessBoardControl()
        {
            InitializeComponent();

            // v1
            //DrawBoard(); // chỉ vẽ sau khi XAML đã load xong

            // v2
            // BuildBoardGrid();

            // v3
            BuildBoard();
        }

        #region V1
        ///// <summary>
        ///// Tạo bàn cờ 8x8 xen kẽ màu trắng và nâu
        ///// </summary>
        //private void DrawBoard()
        //{
        //    BoardGrid.Children.Clear();
        //    BoardGrid.Rows = _rows;
        //    BoardGrid.Columns = _cols;
        //    _cells.Clear();

        //    for (int r = 0; r < _rows; r++)
        //    {
        //        for (int c = 0; c < _cols; c++)
        //        {
        //            var coord = $"{(char)('a' + c)}{8 - r}";
        //            var cell = new Button
        //            {
        //                Tag = coord,
        //                Background = (r + c) % 2 == 0 ? Brushes.Beige : Brushes.SaddleBrown,
        //                BorderThickness = new Thickness(0.5),
        //                Margin = new Thickness(0),
        //                FontSize = 20
        //            };
        //            cell.Click += OnCellClicked;
        //            BoardGrid.Children.Add(cell);
        //            _cells[coord] = cell;
        //        }
        //    }
        //}

        //private void OnCellClicked(object sender, RoutedEventArgs e)
        //{
        //    if (sender is not Button cell) return;

        //    // Nếu chưa chọn ô nào thì chọn ô đầu
        //    if (_selectedCell == null)
        //    {
        //        _selectedCell = cell;
        //        _selectedCell.BorderBrush = Brushes.Red;
        //        _selectedCell.BorderThickness = new Thickness(3);
        //        return;
        //    }

        //    // Nếu đã chọn rồi, đây là click thứ 2
        //    string from = _selectedCell.Tag.ToString()!;
        //    string to = cell.Tag.ToString()!;

        //    // Phát sự kiện MoveSelected
        //    MoveSelected?.Invoke(this, new MoveSelectedEventArgs(from, to));

        //    // Reset highlight
        //    _selectedCell.BorderBrush = null;
        //    _selectedCell.BorderThickness = new Thickness(0.5);
        //    _selectedCell = null;
        //}

        ///// <summary>
        ///// Highlight the last move (both from and to squares). Use RemoveLastHighlight() to clear.
        ///// </summary>
        //public void HighlightLastMove(string from, string to)
        //{
        //    RemoveLastHighlight();

        //    if (_cells.TryGetValue(from, out var bFrom))
        //    {
        //        bFrom.Background = Brushes.Yellow;
        //    }
        //    if (_cells.TryGetValue(to, out var bTo))
        //    {
        //        bTo.Background = Brushes.Yellow;
        //    }
        //}

        ///// <summary>
        ///// Restore original colors (simple approach).
        ///// </summary>
        //public void RemoveLastHighlight()
        //{
        //    foreach (var kv in _cells)
        //    {
        //        var coord = kv.Key;
        //        var btn = kv.Value;
        //        // recompute default color
        //        int file = coord[0] - 'a';
        //        int rank = '8' - coord[1]; // r index
        //        var defaultBrush = ((file + rank) % 2 == 0) ? Brushes.Beige : Brushes.SaddleBrown;
        //        btn.Background = defaultBrush;
        //    }
        //}
        #endregion

        #region Code V2
        ///// <summary>
        ///// Assign board state and refresh UI.
        ///// </summary>
        //public void SetBoardState(ChessBoardState board)
        //{
        //    _boardState = board ?? throw new ArgumentNullException(nameof(board));
        //    RefreshBoard();
        //}

        ///// <summary>
        ///// Build the 9x9 visual grid (ranks at left, files at bottom) and 8x8 board buttons.
        ///// </summary>
        //private void BuildBoardGrid()
        //{
        //    RootGrid.Children.Clear();
        //    RootGrid.RowDefinitions.Clear();
        //    RootGrid.ColumnDefinitions.Clear();
        //    _cells.Clear();

        //    // 9 rows: 8 for board + 1 for files labels at bottom
        //    for (int r = 0; r < 9; r++)
        //        RootGrid.RowDefinitions.Add(new RowDefinition { Height = (r == 8) ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });

        //    // 9 cols: 1 for rank labels at left + 8 for board
        //    for (int c = 0; c < 9; c++)
        //        RootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = (c == 0) ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });

        //    // Add rank labels (left side). Ranks 8..1
        //    for (int r = 0; r < 8; r++)
        //    {
        //        var rank = 8 - r;
        //        var tb = new TextBlock
        //        {
        //            Text = rank.ToString(),
        //            FontWeight = FontWeights.SemiBold,
        //            VerticalAlignment = VerticalAlignment.Center,
        //            HorizontalAlignment = HorizontalAlignment.Center,
        //            Margin = new Thickness(4)
        //        };
        //        Grid.SetRow(tb, r);
        //        Grid.SetColumn(tb, 0);
        //        RootGrid.Children.Add(tb);
        //    }

        //    // Add file labels (bottom). Files a..h
        //    for (int c = 0; c < 8; c++)
        //    {
        //        char file = (char)('a' + c);
        //        var tb = new TextBlock
        //        {
        //            Text = file.ToString(),
        //            FontWeight = FontWeights.SemiBold,
        //            VerticalAlignment = VerticalAlignment.Center,
        //            HorizontalAlignment = HorizontalAlignment.Center,
        //            Margin = new Thickness(8, 4, 8, 4)
        //        };
        //        Grid.SetRow(tb, 8);
        //        Grid.SetColumn(tb, c + 1);
        //        RootGrid.Children.Add(tb);
        //    }

        //    // Create 8x8 buttons for the board squares
        //    for (int r = 0; r < 8; r++)
        //    {
        //        for (int c = 0; c < 8; c++)
        //        {
        //            // Compute square name: files a..h, ranks 8..1
        //            char file = (char)('a' + c);
        //            int rank = 8 - r;
        //            string coord = $"{file}{rank}";

        //            var btn = new Button
        //            {
        //                Tag = coord,
        //                FontSize = 26,
        //                Padding = new Thickness(0),
        //                Margin = new Thickness(0.5),
        //                Background = ((r + c) % 2 == 0) ? Brushes.Beige : Brushes.SaddleBrown,
        //                BorderThickness = new Thickness(1),
        //                BorderBrush = Brushes.Transparent,
        //                Foreground = Brushes.Black
        //            };
        //            btn.Click += OnCellClicked;

        //            Grid.SetRow(btn, r);
        //            Grid.SetColumn(btn, c + 1); // column 0 reserved for rank labels
        //            RootGrid.Children.Add(btn);
        //            _cells[coord] = btn;
        //        }
        //    }
        //}

        ///// <summary>
        ///// Refresh UI from internal ChessBoardState (if set).
        ///// </summary>
        //public void RefreshBoard()
        //{
        //    if (_boardState == null) return;

        //    foreach (var kv in _cells)
        //    {
        //        string coord = kv.Key;
        //        var btn = kv.Value;

        //        var piece = _boardState.GetPieceAt(coord);
        //        if (piece.HasValue)
        //        {
        //            btn.Content = PieceToEmoji(piece.Value);
        //        }
        //        else
        //        {
        //            btn.Content = string.Empty;
        //        }

        //        // restore default background if not highlighted
        //        var file = coord[0] - 'a';
        //        var rank = '8' - coord[1];
        //        btn.Background = ((file + rank) % 2 == 0) ? Brushes.Beige : Brushes.SaddleBrown;
        //        btn.BorderBrush = Brushes.Transparent;
        //    }
        //}

        //private string PieceToEmoji(ChessPiece piece)
        //{
        //    // White pieces (use white glyphs), Black use black glyphs
        //    return (piece.Color, piece.Type) switch
        //    {
        //        (PieceColor.White, PieceType.King) => "♔",
        //        (PieceColor.White, PieceType.Queen) => "♕",
        //        (PieceColor.White, PieceType.Rook) => "♖",
        //        (PieceColor.White, PieceType.Bishop) => "♗",
        //        (PieceColor.White, PieceType.Knight) => "♘",
        //        (PieceColor.White, PieceType.Pawn) => "♙",

        //        (PieceColor.Black, PieceType.King) => "♚",
        //        (PieceColor.Black, PieceType.Queen) => "♛",
        //        (PieceColor.Black, PieceType.Rook) => "♜",
        //        (PieceColor.Black, PieceType.Bishop) => "♝",
        //        (PieceColor.Black, PieceType.Knight) => "♞",
        //        (PieceColor.Black, PieceType.Pawn) => "♟",

        //        _ => "?"
        //    };
        //}

        //private void OnCellClicked(object sender, RoutedEventArgs e)
        //{
        //    if (sender is not Button cell) return;
        //    string coord = cell.Tag as string ?? throw new InvalidOperationException("Cell tag missing");

        //    // If no selected cell yet -> select this one
        //    if (_selectedCell == null)
        //    {
        //        _selectedCell = cell;
        //        _selectedCell.BorderBrush = Brushes.Red;
        //        _selectedCell.BorderThickness = new Thickness(3);
        //        return;
        //    }

        //    // If clicking same cell -> deselect
        //    if (ReferenceEquals(_selectedCell, cell))
        //    {
        //        _selectedCell.BorderBrush = Brushes.Transparent;
        //        _selectedCell.BorderThickness = new Thickness(1);
        //        _selectedCell = null;
        //        return;
        //    }

        //    // Second click: this is the destination
        //    string from = _selectedCell.Tag as string ?? "";
        //    string to = coord;

        //    // raise MoveSelected with UCI-like squares
        //    MoveSelected?.Invoke(this, new MoveSelectedEventArgs(from, to));

        //    // reset previous highlight
        //    _selectedCell.BorderBrush = Brushes.Transparent;
        //    _selectedCell.BorderThickness = new Thickness(1);
        //    _selectedCell = null;
        //}

        ///// <summary>
        ///// Highlight last move on board (both from and to squares).
        ///// </summary>
        //public void HighlightLastMove(string from, string to)
        //{
        //    // first clear default backgrounds
        //    RefreshBoard();

        //    if (_cells.TryGetValue(from, out var bFrom))
        //    {
        //        bFrom.Background = Brushes.Yellow;
        //    }
        //    if (_cells.TryGetValue(to, out var bTo))
        //    {
        //        bTo.Background = Brushes.Yellow;
        //    }
        //}
        #endregion


        #region code V3
        /// <summary>
        /// Gán ChessBoardState và tùy chọn lượt hiện tại (để highlight viền)
        /// </summary>
        public void SetBoardState(ChessBoardState board, PlayerColor currentTurn = PlayerColor.White)
        {
            _boardState = board ?? throw new ArgumentNullException(nameof(board));
            _currentTurn = currentTurn;
            RefreshBoard();
        }

        private void BuildBoard()
        {
            BoardGrid.Children.Clear();
            BoardGrid.RowDefinitions.Clear();
            BoardGrid.ColumnDefinitions.Clear();
            _cells.Clear();

            // 9 rows (0..7 = board rows, 8 = file labels)
            for (int r = 0; r < 9; r++)
            {
                BoardGrid.RowDefinitions.Add(new RowDefinition { Height = (r == 8) ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
            }

            // 9 columns (0 = rank labels, 1..8 = board columns)
            for (int c = 0; c < 9; c++)
            {
                BoardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = (c == 0) ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
            }

            // Rank labels (left side) 8..1 on rows 0..7, column 0
            for (int r = 0; r < 8; r++)
            {
                var rank = 8 - r;
                var tb = new TextBlock
                {
                    Text = rank.ToString(),
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(4)
                };
                Grid.SetRow(tb, r);
                Grid.SetColumn(tb, 0);
                BoardGrid.Children.Add(tb);
            }

            // File labels (bottom) a..h on row 8, columns 1..8
            for (int c = 0; c < 8; c++)
            {
                char file = (char)('a' + c);
                var tb = new TextBlock
                {
                    Text = file.ToString(),
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(8, 4, 8, 4)
                };
                Grid.SetRow(tb, 8);
                Grid.SetColumn(tb, c + 1);
                BoardGrid.Children.Add(tb);
            }

            // Board squares (rows 0..7, cols 1..8)
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    char file = (char)('a' + c);
                    int rank = 8 - r;
                    string coord = $"{file}{rank}";

                    var btn = new Button
                    {
                        Tag = coord,
                        FontSize = 26,
                        Padding = new Thickness(0),
                        Margin = new Thickness(0.5),
                        Background = ((r + c) % 2 == 0) ? Brushes.Beige : Brushes.SaddleBrown,
                        BorderThickness = new Thickness(1),
                        BorderBrush = Brushes.Transparent,
                        Foreground = Brushes.Black,
                        Opacity = 1.0
                    };

                    // hover visual
                    btn.MouseEnter += (s, e) =>
                    {
                        if (btn != _selectedButton)
                            btn.Opacity = 0.9;
                    };
                    btn.MouseLeave += (s, e) =>
                    {
                        if (btn != _selectedButton)
                            btn.Opacity = 1.0;
                    };

                    btn.Click += (s, e) => OnSquareClicked(btn, coord);

                    btn.PreviewMouseLeftButtonDown += OnCellMouseDown;
                    btn.PreviewMouseMove += OnCellMouseMove;
                    btn.PreviewMouseLeftButtonUp += OnCellMouseUp;

                    Grid.SetRow(btn, r);
                    Grid.SetColumn(btn, c + 1);
                    BoardGrid.Children.Add(btn);
                    _cells[coord] = btn;
                }
            }
        }

        private void OnSquareClicked(Button btn, string coord)
        {
            if (_selectedButton == null)
            {
                // chọn ô đầu tiên
                _selectedButton = btn;
                btn.BorderBrush = Brushes.Gold;
                btn.BorderThickness = new Thickness(3);
                return;
            }

            // nếu click lại ô đã chọn -> hủy chọn
            if (ReferenceEquals(_selectedButton, btn))
            {
                _selectedButton.BorderBrush = Brushes.Transparent;
                _selectedButton.BorderThickness = new Thickness(1);
                _selectedButton = null;
                return;
            }

            // ô thứ hai -> raise event MoveSelected
            string from = _selectedButton.Tag as string ?? "";
            string to = coord;

            // clear selection highlight
            _selectedButton.BorderBrush = Brushes.Transparent;
            _selectedButton.BorderThickness = new Thickness(1);
            _selectedButton = null;

            // lưu last move để highlight
            _lastFrom = from;
            _lastTo = to;

            MoveSelected?.Invoke(this, new MoveSelectedEventArgs(from, to));
        }

        /// <summary>
        /// Cập nhật UI từ state (gọi khi board thay đổi)
        /// </summary>
        public void RefreshBoard()
        {
            if (_boardState == null) return;

            foreach (var kv in _cells)
            {
                string coord = kv.Key;
                var btn = kv.Value;

                var piece = _boardState.GetPieceAt(coord);
                if (piece.HasValue)
                {
                    btn.Content = PieceToEmoji(piece.Value);
                    btn.ToolTip = PieceToName(piece.Value);
                }
                else
                {
                    btn.Content = string.Empty;
                    btn.ToolTip = null;
                }

                // restore default bg & border (unless we want last-move highlight later)
                int file = coord[0] - 'a';
                int rank = '8' - coord[1];
                btn.Background = ((file + rank) % 2 == 0) ? Brushes.Beige : Brushes.SaddleBrown;
                btn.BorderBrush = Brushes.Transparent;
                btn.BorderThickness = new Thickness(1);
                btn.Opacity = 1.0;
            }

            // apply last-move highlight if present
            if (!string.IsNullOrEmpty(_lastFrom) && !string.IsNullOrEmpty(_lastTo))
            {
                HighlightLastMove(_lastFrom!, _lastTo!);
            }

            // highlight current turn via border
            HighlightCurrentPlayer();
        }

        private void OnCellClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string square) return;

            if (_selectedButton == null)
            {
                _selectedButton = b;
                b.Background = Brushes.Yellow;
            }
            else
            {
                if (_selectedButton == b)
                {
                    ResetHighlights();
                    _selectedButton = null;
                    return;
                }

                MoveSelected?.Invoke(this, new MoveSelectedEventArgs(
                    _selectedButton.Tag!.ToString()!, square));

                ResetHighlights();
                _selectedButton = null;
            }
        }

        private void OnCellMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button btn)
            {
                _dragStart = e.GetPosition(this);
                _dragSourceButton = btn;
                _isDragging = false;
            }
        }

        private void OnCellMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragSourceButton == null)
                return;

            var pos = e.GetPosition(this);
            var diff = (_dragStart - pos);
            if (!_isDragging && (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                _isDragging = true;

                // 4a
                //// optional visual cue
                //_dragSourceButton.Opacity = 0.5;
                //DragDrop.DoDragDrop(_dragSourceButton, _dragSourceButton.Tag!.ToString(), DragDropEffects.Move);
                //_dragSourceButton.Opacity = 1;

                // 👻 tạo ghost piece (4b)
                if (_dragSourceButton.Content is string content && !string.IsNullOrEmpty(content))
                {
                    _dragGhost = new TextBlock
                    {
                        Text = content,
                        FontSize = 32,
                        Opacity = 0.7,
                        IsHitTestVisible = false
                    };
                    BoardGrid.Children.Add(_dragGhost);
                    Panel.SetZIndex(_dragGhost, 99);
                }
                _dragSourceButton.Opacity = 0.4;


                // ✅ ngăn sự kiện Click bị kích hoạt sau drag
                e.Handled = true;
            }

            if (_isDragging && _dragGhost != null)
            {
                var p = e.GetPosition(BoardGrid);
                Canvas.SetLeft(_dragGhost, p.X - 16);
                Canvas.SetTop(_dragGhost, p.Y - 16);
            }
        }

        private void OnCellMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_dragGhost != null)
            {
                BoardGrid.Children.Remove(_dragGhost);
                _dragGhost = null;
            }

            if (!_isDragging)
            {
                _dragSourceButton = null;
                return;
            }

            _isDragging = false;

            if (sender is Button target && _dragSourceButton != null)
            {
                string from = _dragSourceButton.Tag!.ToString()!;
                string to = target.Tag!.ToString()!;

                // ✅ reset sớm trước khi invoke event
                var src = _dragSourceButton;
                _dragSourceButton = null;

                if (from != to)
                    MoveSelected?.Invoke(this, new MoveSelectedEventArgs(from, to));
            }
            else
            {
                _dragSourceButton = null;
            }
        }


        private void ResetHighlights()
        {
            foreach (var kv in _cells)
            {
                int file = kv.Key[0] - 'a';
                int rank = '8' - kv.Key[1];
                kv.Value.Background = ((file + rank) % 2 == 0) ? Brushes.Beige : Brushes.SaddleBrown;
            }
        }

        public void HighlightLastMove(string from, string to)
        {
            // restore board default first (but do not clobber selection highlight)
            foreach (var kv in _cells)
            {
                var b = kv.Value;
                // if selected, keep selection look
                if (ReferenceEquals(_selectedButton, b)) continue;

                int file = kv.Key[0] - 'a';
                int rank = '8' - kv.Key[1];
                b.Background = ((file + rank) % 2 == 0) ? Brushes.Beige : Brushes.SaddleBrown;
            }

            
            if (_cells.TryGetValue(from, out var bFrom))
            {
                AnimateMovePiece(from, to);
                bFrom.Background = Brushes.Gold;
            }
            if (_cells.TryGetValue(to, out var bTo))
            {
                bTo.Background = Brushes.Gold;
                // animate target cell slightly
                AnimatePulse(bTo);
            }
        }

        private void AnimatePulse(Button btn)
        {
            // V3
            //var anim = new DoubleAnimation
            //{
            //    From = 0.5,
            //    To = 1.0,
            //    Duration = TimeSpan.FromMilliseconds(300),
            //    AutoReverse = true,
            //    EasingFunction = new QuadraticEase()
            //};

            // 4a
            var anim = new DoubleAnimation(1.0, 0.6, TimeSpan.FromMilliseconds(150))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(1)
            };
            btn.BeginAnimation(OpacityProperty, anim);
        }

        private void HighlightCurrentPlayer()
        {
            var color = _currentTurn == PlayerColor.White ? Brushes.AliceBlue : Brushes.LightSlateGray;
            BoardBorder.BorderBrush = color;
            BoardBorder.BorderThickness = new Thickness(3);
        }

        /// <summary>
        /// Flash đỏ ô báo lỗi (ví dụ move invalid).
        /// </summary>
        public void FlashErrorCell(string coord)
        {
            if (!_cells.TryGetValue(coord, out var btn)) return;

            // preserve original brush
            var originalBrush = btn.Background is SolidColorBrush sc ? sc.Color : Colors.Transparent;
            var brush = new SolidColorBrush(Colors.Red);
            btn.Background = brush;

            var animation = new ColorAnimation
            {
                From = Colors.Red,
                To = originalBrush,
                Duration = TimeSpan.FromMilliseconds(400),
                AutoReverse = true,
                EasingFunction = new QuadraticEase()
            };

            brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
        }

        /// <summary>
        /// Hỗ trợ set một piece (dùng từ ViewModel nếu muốn set từng ô).
        /// </summary>
        public void SetPiece(string coord, string emoji)
        {
            if (_cells.TryGetValue(coord, out var btn))
                btn.Content = emoji;
        }

        /// <summary>
        /// Xóa selection highlight (dùng khi VM muốn reset selection)
        /// </summary>
        public void ClearSelectionHighlight()
        {
            if (_selectedButton != null)
            {
                _selectedButton.BorderBrush = Brushes.Transparent;
                _selectedButton.BorderThickness = new Thickness(1);
                _selectedButton.Opacity = 1.0;
                _selectedButton = null;
            }
        }

        private string PieceToEmoji(ChessPiece piece)
        {
            return (piece.Color, piece.Type) switch
            {
                (PieceColor.White, PieceType.King) => "♔",
                (PieceColor.White, PieceType.Queen) => "♕",
                (PieceColor.White, PieceType.Rook) => "♖",
                (PieceColor.White, PieceType.Bishop) => "♗",
                (PieceColor.White, PieceType.Knight) => "♘",
                (PieceColor.White, PieceType.Pawn) => "♙",

                (PieceColor.Black, PieceType.King) => "♚",
                (PieceColor.Black, PieceType.Queen) => "♛",
                (PieceColor.Black, PieceType.Rook) => "♜",
                (PieceColor.Black, PieceType.Bishop) => "♝",
                (PieceColor.Black, PieceType.Knight) => "♞",
                (PieceColor.Black, PieceType.Pawn) => "♟",

                _ => string.Empty
            };
        }

        private string PieceToName(ChessPiece piece)
        {
            string colorName = piece.Color == PieceColor.White ? "Trắng" : "Đen";
            string typeName = piece.Type switch
            {
                PieceType.King => "Vua",
                PieceType.Queen => "Hậu",
                PieceType.Rook => "Xe",
                PieceType.Bishop => "Tượng",
                PieceType.Knight => "Mã",
                PieceType.Pawn => "Tốt",
                _ => "?"
            };
            return $"{colorName} - {typeName}";
        }

        // =====================================================
        // Phase 3f - animate move
        // =====================================================
        private void AnimateMovePiece(string from, string to)
        {
            if (!_cells.TryGetValue(from, out var btnFrom) || !_cells.TryGetValue(to, out var btnTo))
                return;

            var pieceText = (btnFrom.Content as TextBlock)?.Text;
            if (string.IsNullOrEmpty(pieceText))
                return;

            // Lấy vị trí pixel tương đối
            var fromPos = btnFrom.TransformToAncestor(BoardGrid)
                .Transform(new Point(0, 0));
            var toPos = btnTo.TransformToAncestor(BoardGrid)
                .Transform(new Point(0, 0));

            // Tạo TextBlock “bay”
            var flying = new TextBlock
            {
                Text = pieceText,
                FontSize = 40,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            // Thêm vào Grid tạm thời
            BoardGrid.Children.Add(flying);
            Canvas.SetLeft(flying, fromPos.X);
            Canvas.SetTop(flying, fromPos.Y);

            // Tạo animation
            var animX = new DoubleAnimation(fromPos.X, toPos.X, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            var animY = new DoubleAnimation(fromPos.Y, toPos.Y, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            // Khi xong animation → xóa TextBlock và refresh lại board
            animY.Completed += (_, __) =>
            {
                BoardGrid.Children.Remove(flying);
                RefreshBoard();
            };

            flying.BeginAnimation(Canvas.LeftProperty, animX);
            flying.BeginAnimation(Canvas.TopProperty, animY);
        }

        #endregion
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
