using Microsoft.Extensions.DependencyInjection;
using MyGames.Desktop.Logs;
using MyGames.Desktop.Models;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace MyGames.Desktop.Controls
{
    public partial class ChessBoardControl : UserControl
    {
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

        /// <summary>
        /// // Map square string -> Button
        /// </summary>
        private readonly Dictionary<string, Button> _cells = new();
        private Button? _selectedButton;

        //  Trạng thái lượt hiện tại
        private PlayerColor _currentTurn = PlayerColor.White;
        // Cho phép trạng thái "chưa chọn bên"
        private bool _isPlayerColorChosen = false;

        public event Action<string>? SquareSelected;


        private Point _dragStart;
        private bool _isDragging;
        private Button? _dragSourceButton;

        public ChessBoardState BoardState { get; private set; } = new();

        private TextBlock? _dragGhost; // 👈 hiển thị quân cờ bay theo chuột

        private readonly SolidColorBrush _lightSquareBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EEEED2"));
        private readonly SolidColorBrush _darkSquareBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#769656"));

        //btn.FontFamily = new FontFamily("Segoe UI Symbol"); 
        //btn.FontFamily = new FontFamily("Chess Merida Unicode");
        private FontFamily _fontFamily = new FontFamily(new Uri("pack://application:,,,/MyGames.Desktop;component/"), "/Resources/Fonts/#Chess Merida Unicode");

        private readonly LoggerService _logger = App.ServiceProvider.GetRequiredService<LoggerService>();

        private readonly Dictionary<string, SvgViewbox> _pieceCache = new();
        private readonly Dictionary<string, ImageSource> _pieceBitmapCache = new();

        private HashSet<string> _legalTargets = new HashSet<string>();

        private double _pieceSize = 42; // mặc định

        public static readonly DependencyProperty IsBlackPlayerProperty =
    DependencyProperty.Register(nameof(IsBlackPlayer), typeof(bool), typeof(ChessBoardControl),
        new PropertyMetadata(false, OnIsBlackPlayerChanged));

        public event EventHandler<PromotionEventArgs>? PromotionRequired;

        public bool IsBlackPlayer
        {
            get => (bool)GetValue(IsBlackPlayerProperty);
            set => SetValue(IsBlackPlayerProperty, value);
        }

        public ChessBoardControl()
        {
            InitializeComponent();
            BuildBoard();
            LoadPieceBitmaps();
        }

        private static void OnIsBlackPlayerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ChessBoardControl)d;
            control.RefreshBoard();
        }

        private void ApplyBoardOrientation()
        {
            if (BoardGrid == null || _cells.Count == 0)
                return;

            // Mỗi ô có key = "a1".."h8".
            // BuildBoard ban đầu đặt Buttons tại Grid.Row = r (0..7), Grid.Column = c+1 (1..8).
            // Ta tính vị trí hiển thị từ coord:
            //
            // originalGridRow = 8 - rank  (với rank là số từ 1..8)
            // originalGridCol = file + 1  (với file 0..7 => col 1..8)
            //
            // Nếu IsBlackPlayer==false -> hiển thị ở vị trí original.
            // Nếu IsBlackPlayer==true  -> hiển thị ở vị trí (7 - originalGridRow, 9 - originalGridCol)
            //
            // Tương đương:
            // displayRow = IsBlackPlayer ? (7 - originalGridRow) : originalGridRow
            // displayCol = IsBlackPlayer ? (9 - originalGridCol) : originalGridCol

            foreach (var kv in _cells)
            {
                string coord = kv.Key; // e.g. "a1"
                var btn = kv.Value;

                int file = coord[0] - 'a';       // 0..7
                int rankNum = coord[1] - '0';    // 1..8

                int originalGridRow = 8 - rankNum;      // 0..7
                int originalGridCol = file + 1;         // 1..8

                int displayRow = IsBlackPlayer ? (7 - originalGridRow) : originalGridRow;
                int displayCol = IsBlackPlayer ? (9 - originalGridCol) : originalGridCol;

                Grid.SetRow(btn, displayRow);
                Grid.SetColumn(btn, displayCol);
            }

            // cập nhật nhãn rank (cột 0) và file (hàng 8) cho phù hợp với hướng hiển thị
            foreach (var child in BoardGrid.Children)
            {
                if (child is TextBlock tb)
                {
                    int col = Grid.GetColumn(tb);
                    int row = Grid.GetRow(tb);

                    // Rank label (cột 0, hàng 0..7): hiển thị số tương ứng với hàng hiển thị
                    if (col == 0 && row >= 0 && row < 8)
                    {
                        // visual row = row
                        tb.Text = IsBlackPlayer ? (row + 1).ToString() : (8 - row).ToString();
                    }
                    // File label (hàng 8, cột 1..8): hiển thị chữ tương ứng
                    else if (row == 8 && col > 0 && col <= 8)
                    {
                        int fileIdx = col - 1; // 0..7
                        tb.Text = IsBlackPlayer
                                ? ((char)('h' - fileIdx)).ToString()
                                : ((char)('a' + fileIdx)).ToString();

                    }
                }
            }
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double w = e.NewSize.Width;
            double h = e.NewSize.Height;

            double newSize = 42;

            if (w <= 460 || h <= 460)
                newSize = 30;

            if (w <= 360 || h <= 360)
                newSize = 24;

            if (w <= 290 || h <= 290)
                newSize = 20;

            if (Math.Abs(newSize - _pieceSize) > 0.1)
            {
                _pieceSize = newSize;
                RefreshBoard();
            }
        }

        /// <summary>
        /// 🟩 Kiểm tra xem quân ở ô này có phải của bên đang tới lượt không.
        /// </summary>
        private bool IsPieceOfCurrentTurn(string coord)
        {
            if (_boardState == null) return false;
            var piece = _boardState.GetPieceAt(coord);
            if (!piece.HasValue) return false;
            return (piece.Value.Color == PieceColor.White && _currentTurn == PlayerColor.White)
                || (piece.Value.Color == PieceColor.Black && _currentTurn == PlayerColor.Black);
        }
        /// <summary>
        /// 🟩 Trả về đối thủ (dùng để đổi lượt)
        /// </summary>
        private PlayerColor OpponentColor(PlayerColor color)
        {
            return color == PlayerColor.White ? PlayerColor.Black :
                   color == PlayerColor.Black ? PlayerColor.White :
                   PlayerColor.None;
        }
        /// <summary>
        /// 🟩 Cho phép ViewModel báo rằng người chơi đã chọn màu (Trắng/Đen).
        /// </summary>
        public void SetPlayerColorChosen(PlayerColor color)
        {
            _currentTurn = color;
            _isPlayerColorChosen = true;
            _logger.Info($"Player color chosen: {_currentTurn}");
        }
        /// <summary>
        /// 🟩 Reset trạng thái lượt đi (khi chưa chọn bên hoặc khởi động lại)
        /// </summary>
        public void ResetTurnState()
        {
            _isPlayerColorChosen = false;
            _currentTurn = PlayerColor.None;
        }


        private void LoadPieceBitmaps()
        {
            var settings = new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true };
            var converter = new FileSvgReader(settings);

            string baseDir = "pack://application:,,,/MyGames.Desktop;component/Resources/Pieces/";

            string[] colors = { "w", "b" };
            string[] pieces = { "pawn", "rook", "knight", "bishop", "queen", "king" };

            foreach (string color in colors)
            {
                foreach (string piece in pieces)
                {
                    string key = $"{color}_{piece}";
                    var uri = new Uri($"{baseDir}{key}.svg");

                    // SharpVectors không đọc trực tiếp pack://, cần stream:
                    var streamInfo = Application.GetResourceStream(uri);
                    if (streamInfo == null) continue;

                    var drawing = converter.Read(streamInfo.Stream);
                    var image = new DrawingImage(drawing);
                    image.Freeze(); // cực quan trọng: giúp dùng đa thread + tăng hiệu năng

                    _pieceBitmapCache[key] = image;
                }
            }
        }

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
                        Background = ((r + c) % 2 == 0) ? _lightSquareBrush : _darkSquareBrush,
                        BorderThickness = new Thickness(1),
                        BorderBrush = Brushes.Transparent,
                        Foreground = Brushes.Black,
                        Opacity = 1.0
                    };

                    btn.SetValue(Grid.RowProperty, r);
                    btn.SetValue(Grid.ColumnProperty, c + 1);
                    btn.HorizontalContentAlignment = HorizontalAlignment.Center;
                    btn.VerticalContentAlignment = VerticalAlignment.Center;
                    btn.FontSize = 42;
                    //btn.FontFamily = _family;

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
            // Nếu chưa có board state thì thôi
            if (_boardState == null) return;

            // Nếu chưa chọn ô nào -> chọn ô nguồn
            if (_selectedButton == null)
            {
                var piece = _boardState.GetPieceAt(coord);
                if (!piece.HasValue)
                {
                    // không có quân để chọn -> ignore
                    return;
                }

                // Nếu quân trên ô không thuộc side đang có lượt -> ignore (chỉ hiển thị targets cho side đang được phép đi)
                if (piece.Value.Color != _boardState.CurrentTurn)
                {
                    // Có thể flash nhẹ nhưng theo yêu cầu ta không tô đỏ, chỉ bỏ qua.
                    return;
                }

                // chấp nhận chọn ô, hiển thị selection
                _selectedButton = btn;
                btn.BorderBrush = Brushes.Gold;
                btn.BorderThickness = new Thickness(3);

                // Tính và hiển thị các ô hợp lệ cho quân này
                bool isOpponent = (piece?.Color != BoardState.CurrentTurn);
                ShowLegalMoves(coord, isOpponent);
                return;
            }

            // Nếu click lại ô đã chọn -> hủy chọn
            if (ReferenceEquals(_selectedButton, btn))
            {
                ClearSelectionHighlight();
                return;
            }

            // Nếu đã có _selectedButton -> đây là ô đích
            string from = _selectedButton.Tag as string ?? "";
            string to = coord;

            // Nếu to nằm trong danh sách legal targets -> raise event
            if (_legalTargets.Contains(to))
            {
                var piece = _boardState.GetPieceAt(from);
                if (piece.HasValue && piece.Value.Type == PieceType.Pawn)
                {
                    int toRank = to[1] - '0';

                    // ✅ kiểm tra phong tốt
                    if ((piece.Value.Color == PieceColor.White && toRank == 8) ||
                        (piece.Value.Color == PieceColor.Black && toRank == 1))
                    {
                        // Gọi event PromotionRequired
                        PromotionRequired?.Invoke(this,
                            new PromotionEventArgs(piece.Value.Color == PieceColor.White, from, to));

                        // Không gọi MoveSelected ngay, chờ dialog xử lý
                        ClearSelectionHighlight();
                        return;
                    }
                }

                // Clear selection visuals trước khi raise
                ClearSelectionHighlight();
                _lastFrom = from;
                _lastTo = to;

                MoveSelected?.Invoke(this, new MoveSelectedEventArgs(from, to));
            }
            else
            {
                // Click vào ô không hợp lệ -> bỏ chọn, không làm gì cả
                ClearSelectionHighlight();
            }
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
                    string prefix = piece.Value.Color == PieceColor.White ? "w" : "b";
                    string pieceName = piece.Value.Type.ToString().ToLower(); // pawn, rook, etc.
                    string key = $"{prefix}_{pieceName}";

                    var imgSrc = _pieceBitmapCache[key];
                    btn.Content = new Image
                    {
                        Source = imgSrc,
                        Width = _pieceSize,
                        Height = _pieceSize,
                        Stretch = Stretch.Uniform
                    }; //btn.Content = PieceToEmoji(piece.Value);

                    btn.Foreground = (piece.Value.Color == PieceColor.White)
                                ? Brushes.White
                                : Brushes.Black;

                    btn.ToolTip = PieceToName(piece.Value);

                    // 👇 thêm chút shadow cho quân trắng, giúp nổi bật
                    if (piece.Value.Color == PieceColor.White)
                    {
                        btn.Effect = new DropShadowEffect
                        {
                            BlurRadius = 2,
                            ShadowDepth = 0,
                            Opacity = 0.6,
                            Color = Colors.Black
                        };
                    }
                    else
                    {
                        btn.Effect = null;
                    }
                }
                else
                {
                    btn.Content = string.Empty;
                    btn.ToolTip = null;
                    btn.Foreground = Brushes.Transparent;
                    btn.Effect = null;
                }


                // Xác định màu ô dựa trên tọa độ ô (không phụ thuộc hướng hiển thị)
                int file = coord[0] - 'a';         // 0..7
                int rank = coord[1] - '1';         // 0..7, '1' -> 0

                //btn.Background = ((file + rank) % 2 == 0) ? Brushes.Beige : Brushes.SaddleBrown;
                bool isLight = (file + rank) % 2 == 0;
                btn.Background = isLight ? _lightSquareBrush : _darkSquareBrush;
                // Nếu có quân thì hiện pointer khi hover
                btn.Cursor = piece.HasValue ? Cursors.Hand : Cursors.Arrow;

                btn.BorderBrush = Brushes.Transparent;
                btn.BorderThickness = new Thickness(1);
                btn.Opacity = 1.0;

                //btn.SetValue(Grid.RowProperty, r);
                //btn.SetValue(Grid.ColumnProperty, c + 1);
                btn.HorizontalContentAlignment = HorizontalAlignment.Center;
                btn.VerticalContentAlignment = VerticalAlignment.Center;
                btn.FontSize = 42;

                // Thử bản thường
                btn.FontFamily = _fontFamily;
            }

            // cập nhật lại layout hiển thị theo IsBlackPlayer
            ApplyBoardOrientation();

            // apply last-move highlight if present
            if (!string.IsNullOrEmpty(_lastFrom) && !string.IsNullOrEmpty(_lastTo))
            {
                HighlightLastMove(_lastFrom!, _lastTo!);
            }
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

                // 6b
                if (_dragSourceButton.Content is string content && !string.IsNullOrEmpty(content))
                {
                    _dragGhost = new TextBlock
                    {
                        Text = content,
                        FontSize = 36,               // bạn có thể điều chỉnh kích thước ghost
                        Opacity = 0.85,
                        IsHitTestVisible = false
                    };

                    // thêm vào overlay canvas (không làm thay đổi layout)
                    OverlayCanvas.Children.Add(_dragGhost);
                    Panel.SetZIndex(_dragGhost, 9999);

                    // khởi tạo vị trí ngay (đặt tại từ vị trí button)
                    var fromPos = _dragSourceButton.TransformToAncestor(this)
                                     .Transform(new Point(0, 0));
                    // convert to OverlayCanvas coords (overlay is same visual root so coordinates ok)
                    Canvas.SetLeft(_dragGhost, fromPos.X);
                    Canvas.SetTop(_dragGhost, fromPos.Y);
                }
                _dragSourceButton.Opacity = 0.4;

                // ✅ ngăn sự kiện Click bị kích hoạt sau drag
                e.Handled = true;
            }

            // 6b
            if (_isDragging && _dragGhost != null)
            {
                // lấy vị trí tương đối so với OverlayCanvas
                var p = e.GetPosition(OverlayCanvas);
                Canvas.SetLeft(_dragGhost, p.X - (_dragGhost.ActualWidth / 2));
                Canvas.SetTop(_dragGhost, p.Y - (_dragGhost.ActualHeight / 2));
            }
        }

        private void OnCellMouseUp(object sender, MouseButtonEventArgs e)
        {
            // 6b
            if (_dragGhost != null)
            {
                OverlayCanvas.Children.Remove(_dragGhost);
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

                // kiểm tra lượt
                if (!IsPieceOfCurrentTurn(from))
                {
                    FlashErrorCell(from);
                    _dragSourceButton = null;
                    return;
                }

                // ✅ reset sớm trước khi invoke event
                var src = _dragSourceButton;
                _dragSourceButton = null;

                if (from != to)
                    MoveSelected?.Invoke(this, new MoveSelectedEventArgs(from, to));

                // đổi lượt
                // _currentTurn = OpponentColor(_currentTurn);
            }
            else
            {
                _dragSourceButton = null;
            }
        }

        private void ResetHighlights()
        {
            ClearSelectionHighlight(); // ensure selection cleared
            foreach (var kv in _cells)
            {
                int file = kv.Key[0] - 'a';
                int rank = '8' - kv.Key[1];
                kv.Value.Background = ((file + rank) % 2 == 0) ? _lightSquareBrush : _darkSquareBrush;
            }
        }

        public void HighlightLastMove(string from, string to)
        {
            _logger.Info("HighlightLastMove_Start");
            if (!_cells.ContainsKey(from) || !_cells.ContainsKey(to)) return;

            // restore board default first (but do not clobber selection highlight)
            foreach (var kv in _cells)
            {
                var b = kv.Value;
                // if selected, keep selection look
                if (ReferenceEquals(_selectedButton, b)) continue;

                int file = kv.Key[0] - 'a';
                int rank = '8' - kv.Key[1];
                b.Background = ((file + rank) % 2 == 0) ? _lightSquareBrush : _darkSquareBrush;
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
            _logger.Info("HighlightLastMove_End");
        }

        private void AnimatePulse(Button btn)
        {
            var anim = new DoubleAnimation(1.0, 0.6, TimeSpan.FromMilliseconds(150))
            {
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(1)
            };
            btn.BeginAnimation(OpacityProperty, anim);
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
        /// Hiển thị các ô hợp lệ (highlight nhẹ) cho quân tại 'from'.
        /// </summary>
        private void ShowLegalMoves(string from, bool isOpponent)
        {
            _legalTargets.Clear();

            // duyệt qua mọi ô và hỏi _boardState.IsMoveLegal
            for (char f = 'a'; f <= 'h'; f++)
            {
                for (char r = '1'; r <= '8'; r++)
                {
                    string to = $"{f}{r}";
                    if (_boardState.IsMoveLegal(from, to, isOpponent))
                    {
                        _legalTargets.Add(to);
                        if (_cells.TryGetValue(to, out var targetBtn))
                        {
                            // highlight ô hợp lệ (màu xanh)
                            targetBtn.Background = Brushes.LightSkyBlue;
                        }
                    }
                }
            }

            // nếu không có target hợp lệ thì hủy chọn (ví dụ quân bị chặn)
            if (_legalTargets.Count == 0)
            {
                // giữ selection border một lúc rồi clear
                // nhưng theo yêu cầu không tô đỏ; ta chỉ clear ngay
                ClearSelectionHighlight();
            }
        }

        /// <summary>
        /// Xóa mọi highlight/gợi ý và selection.
        /// Giữ nguyên màu ô mặc định.
        /// </summary>
        public void ClearSelectionHighlight()
        {
            // reset selection border
            if (_selectedButton != null)
            {
                _selectedButton.BorderBrush = Brushes.Transparent;
                _selectedButton.BorderThickness = new Thickness(1);
                _selectedButton.Opacity = 1.0;
                _selectedButton = null;
            }

            // reset legal targets highlight
            foreach (var t in _legalTargets)
            {
                if (_cells.TryGetValue(t, out var btn))
                {
                    int file = btn.Tag!.ToString()![0] - 'a';
                    int rank = '8' - btn.Tag!.ToString()![1];
                    bool isLight = (file + rank) % 2 == 0;
                    btn.Background = isLight ? _lightSquareBrush : _darkSquareBrush;
                }
            }
            _legalTargets.Clear();
        }

        [Obsolete("Có thể dùng trong tương lai.")]
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

            // Lấy quân cờ gốc (nếu là string emoji hoặc TextBlock)
            string? pieceText = null;
            if (btnFrom.Content is TextBlock tb)
                pieceText = tb.Text;
            else if (btnFrom.Content is string s)
                pieceText = s;

            if (string.IsNullOrEmpty(pieceText)) return;

            // Lấy vị trí tương đối so với OverlayCanvas
            var fromPos = btnFrom.TransformToAncestor(OverlayCanvas)
                .Transform(new Point(0, 0));
            var toPos = btnTo.TransformToAncestor(OverlayCanvas)
                .Transform(new Point(0, 0));

            // Tạo “ghost” bay
            var flying = new TextBlock
            {
                Text = pieceText,
                FontSize = 40, // kích thước có thể chỉnh cho phù hợp bàn cờ
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false,
                Opacity = 0.9
            };

            // Thêm ghost vào overlay (không ảnh hưởng layout)
            OverlayCanvas.Children.Add(flying);
            Panel.SetZIndex(flying, 9999);
            Canvas.SetLeft(flying, fromPos.X);
            Canvas.SetTop(flying, fromPos.Y);

            // Animation bay tới ô đích
            var animX = new DoubleAnimation(fromPos.X, toPos.X, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };
            var animY = new DoubleAnimation(fromPos.Y, toPos.Y, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            // Khi hoàn thành → xóa ghost và refresh
            animY.Completed += (_, __) =>
            {
                OverlayCanvas.Children.Remove(flying);
                RefreshBoard();
            };

            flying.BeginAnimation(Canvas.LeftProperty, animX);
            flying.BeginAnimation(Canvas.TopProperty, animY);
        }

        /// <summary>
        /// Đặt lại bàn cờ về trạng thái ban đầu và cập nhật lại giao diện.
        /// </summary>
        public void ResetBoardDisplay()
        {
            BoardState.Reset();    // reset toàn bộ quân về vị trí khởi đầu
            _selectedButton = null;
            _lastFrom = null;
            _lastTo = null;

            // reset lượt
            _currentTurn = PlayerColor.None;
            _isPlayerColorChosen = false;

            RefreshBoard();        // vẽ lại giao diện
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

    public class PromotionEventArgs : EventArgs
    {
        public bool IsWhite { get; }
        public string From { get; }
        public string To { get; }

        public PromotionEventArgs(bool isWhite, string from, string to)
        {
            IsWhite = isWhite;
            From = from;
            To = to;
        }
    }

}
