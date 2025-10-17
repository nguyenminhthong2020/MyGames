namespace MyGames.Desktop.Models
{
    /// <summary>
    /// Màu quân cờ.
    /// </summary>
    public enum PieceColor
    {
        White,
        Black
    }

    /// <summary>
    /// Loại quân cờ.
    /// </summary>
    public enum PieceType
    {
        Pawn,
        Rook,
        Knight,
        Bishop,
        Queen,
        King
    }

    /// <summary>
    /// Đại diện cho một quân cờ trên bàn.
    /// lưu loại và màu.
    /// </summary>
    public struct ChessPiece
    {
        public PieceType Type { get; set; }
        public PieceColor Color { get; set; }

        public ChessPiece(PieceType type, PieceColor color)
        {
            Type = type;
            Color = color;
        }

        public override string ToString()
        {
            string symbol = Type switch
            {
                PieceType.Pawn => "P",
                PieceType.Rook => "R",
                PieceType.Knight => "N",
                PieceType.Bishop => "B",
                PieceType.Queen => "Q",
                PieceType.King => "K",
                _ => "?"
            };
            return Color == PieceColor.White ? symbol : symbol.ToLower();
        }
    }

    /// <summary>
    /// Quản lý trạng thái toàn bộ bàn cờ.
    /// </summary>
    public class ChessBoardState
    {
        /// <summary>
        /// giữ trạng thái.
        /// </summary>
        private readonly ChessPiece?[,] _board = new ChessPiece?[8, 8];

        public ChessBoardState()
        {
            Reset();
        }

        /// <summary>
        /// Đặt lại bàn cờ về trạng thái ban đầu.
        /// </summary>
        public void Reset()
        {
            Clear();

            // Hàng tốt
            for (int i = 0; i < 8; i++)
            {
                _board[1, i] = new ChessPiece(PieceType.Pawn, PieceColor.White);
                _board[6, i] = new ChessPiece(PieceType.Pawn, PieceColor.Black);
            }

            // Hàng chính
            _board[0, 0] = new ChessPiece(PieceType.Rook, PieceColor.White);
            _board[0, 7] = new ChessPiece(PieceType.Rook, PieceColor.White);
            _board[7, 0] = new ChessPiece(PieceType.Rook, PieceColor.Black);
            _board[7, 7] = new ChessPiece(PieceType.Rook, PieceColor.Black);

            _board[0, 1] = new ChessPiece(PieceType.Knight, PieceColor.White);
            _board[0, 6] = new ChessPiece(PieceType.Knight, PieceColor.White);
            _board[7, 1] = new ChessPiece(PieceType.Knight, PieceColor.Black);
            _board[7, 6] = new ChessPiece(PieceType.Knight, PieceColor.Black);

            _board[0, 2] = new ChessPiece(PieceType.Bishop, PieceColor.White);
            _board[0, 5] = new ChessPiece(PieceType.Bishop, PieceColor.White);
            _board[7, 2] = new ChessPiece(PieceType.Bishop, PieceColor.Black);
            _board[7, 5] = new ChessPiece(PieceType.Bishop, PieceColor.Black);

            _board[0, 3] = new ChessPiece(PieceType.Queen, PieceColor.White);
            _board[0, 4] = new ChessPiece(PieceType.King, PieceColor.White);
            _board[7, 3] = new ChessPiece(PieceType.Queen, PieceColor.Black);
            _board[7, 4] = new ChessPiece(PieceType.King, PieceColor.Black);
        }

        /// <summary>
        /// Xóa toàn bộ quân trên bàn.
        /// </summary>
        public void Clear()
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    _board[r, c] = null;
        }

        /// <summary>
        /// Lấy quân cờ tại vị trí (a1 → h8).
        /// </summary>
        public ChessPiece? GetPieceAt(string square)
        {
            if (!TryParseSquare(square, out int row, out int col))
                throw new ArgumentException($"Ô '{square}' không hợp lệ");

            return _board[row, col];
        }

        /// <summary>
        /// Di chuyển quân từ ô from đến ô to. 
        /// Không kiểm tra hợp lệ nước đi, chỉ cập nhật vị trí.
        /// </summary>
        public bool MovePiece(string from, string to)
        {
            if (!TryParseSquare(from, out int fromRow, out int fromCol)) return false;
            if (!TryParseSquare(to, out int toRow, out int toCol)) return false;

            var piece = _board[fromRow, fromCol];
            if (piece == null)
                return false; // Không có quân để di chuyển

            _board[toRow, toCol] = piece;
            _board[fromRow, fromCol] = null;
            return true;
        }

        /// <summary>
        /// Chuyển ký hiệu “a1” sang chỉ số hàng, cột.
        /// </summary>
        public static bool TryParseSquare(string square, out int row, out int col)
        {
            row = col = -1;
            if (string.IsNullOrWhiteSpace(square) || square.Length != 2)
                return false;

            col = square[0] - 'a';
            row = square[1] - '1';
            if (row < 0 || row > 7 || col < 0 || col > 7)
                return false;

            // Hàng 1 (index 0) là trắng dưới → đảo trục y cho hiển thị chuẩn
            row = 7 - row;
            return true;
        }

        /// <summary>
        /// In bàn cờ dạng text (phục vụ debug).
        /// </summary>
        public override string ToString()
        {
            var lines = new List<string>();
            for (int r = 0; r < 8; r++)
            {
                var row = new List<string>();
                for (int c = 0; c < 8; c++)
                    row.Add(_board[r, c]?.ToString() ?? ".");
                lines.Add(string.Join(" ", row));
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
