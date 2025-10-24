using static MyGames.Desktop.Models.ChessPiece;

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

        /// <summary>
        /// Kết quả ván cờ (nullable nếu chưa kết thúc)
        /// </summary>
        public enum GameResult
        {
            None,
            WhiteWins,
            BlackWins,
            Draw
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

        // =====================
        // Trạng thái ván chơi
        // =====================
        public PieceColor CurrentTurn { get; private set; } = PieceColor.White;
        public bool WhiteCanCastleKingside { get; private set; } = true;
        public bool WhiteCanCastleQueenside { get; private set; } = true;
        public bool BlackCanCastleKingside { get; private set; } = true;
        public bool BlackCanCastleQueenside { get; private set; } = true;
        /// <summary>
        /// Square (UCI) that can be captured en-passant on the next move, e.g. "e3"
        /// </summary>
        public string? EnPassantTarget { get; private set; } = null;

        // Game end
        public bool IsGameOver { get; private set; } = false;
        public GameResult GameResult { get; private set; } = GameResult.None;

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
                _board[6, i] = new ChessPiece(PieceType.Pawn, PieceColor.White);
                _board[1, i] = new ChessPiece(PieceType.Pawn, PieceColor.Black);
            }

            // Rooks
            _board[7, 0] = new ChessPiece(PieceType.Rook, PieceColor.White);
            _board[7, 7] = new ChessPiece(PieceType.Rook, PieceColor.White);
            _board[0, 0] = new ChessPiece(PieceType.Rook, PieceColor.Black);
            _board[0, 7] = new ChessPiece(PieceType.Rook, PieceColor.Black);

            // Knights
            _board[7, 1] = new ChessPiece(PieceType.Knight, PieceColor.White);
            _board[7, 6] = new ChessPiece(PieceType.Knight, PieceColor.White);
            _board[0, 1] = new ChessPiece(PieceType.Knight, PieceColor.Black);
            _board[0, 6] = new ChessPiece(PieceType.Knight, PieceColor.Black);

            // Bishops
            _board[7, 2] = new ChessPiece(PieceType.Bishop, PieceColor.White);
            _board[7, 5] = new ChessPiece(PieceType.Bishop, PieceColor.White);
            _board[0, 2] = new ChessPiece(PieceType.Bishop, PieceColor.Black);
            _board[0, 5] = new ChessPiece(PieceType.Bishop, PieceColor.Black);

            // Queens and Kings
            _board[7, 3] = new ChessPiece(PieceType.Queen, PieceColor.White);
            _board[7, 4] = new ChessPiece(PieceType.King, PieceColor.White);
            _board[0, 3] = new ChessPiece(PieceType.Queen, PieceColor.Black);
            _board[0, 4] = new ChessPiece(PieceType.King, PieceColor.Black);

            CurrentTurn = PieceColor.White;
            WhiteCanCastleKingside = WhiteCanCastleQueenside = true;
            BlackCanCastleKingside = BlackCanCastleQueenside = true;
            EnPassantTarget = null;

            IsGameOver = false;
            GameResult = GameResult.None;
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
        /// --> Public MovePiece wrapper (không dùng cho luật, chỉ dùng nội bộ)
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
        /// Di chuyển quân từ ô from đến ô to. Không kiểm tra hợp lệ nước đi.
        /// </summary>
        internal void MovePieceInternal(int fromRow, int fromCol, int toRow, int toCol)
        {
            _board[toRow, toCol] = _board[fromRow, fromCol];
            _board[fromRow, fromCol] = null;
        }

        // =====================================
        // Kiểm tra và thực hiện nước đi hợp lệ
        // =====================================
        /// <summary>
        /// Thử thực hiện nước đi nếu hợp lệ. Nếu promotionChar không truyền, mặc định 'q' (queen).
        /// Trả về true nếu đã thực hiện và cập nhật trạng thái (turn, en passant, castling rights, game end).
        /// </summary>
        public bool TryMakeMove(string from, string to, char promotion = 'q', bool isOpponent = false)
        {
            if (!TryParseSquare(from, out int fr, out int fc)) return false;
            if (!TryParseSquare(to, out int tr, out int tc)) return false;

            var piece = _board[fr, fc];
            if (piece == null) return false;

            // Kiểm tra xem side có đúng lượt không
            if (piece.Value.Color != CurrentTurn) return false;

            // Kiểm tra move cơ bản theo luật từng quân (pseudo-legal)
            if (!IsMovePseudoLegal(fr, fc, tr, tc, piece.Value))
                return false;

            if(!isOpponent)
            {
                // Simulate move và kiểm tra king có bị chiếu hay không (move không được để king mình trong chiếu)
                var clone = CloneInternal();
                // thực hiện trên clone, bao gồm en passant / castling / promotion khi cần để simulate đúng
                clone.ApplyMoveOnClone(fr, fc, tr, tc, promotion);

                if (clone.IsKingInCheck(piece.Value.Color))
                {
                    // không hợp lệ vì để vua mình bị chiếu
                    return false;
                }
            }

            // Nếu tới đây thì hợp lệ — thực hiện thật trên board này (kèm xử lý en-passant, castling, promotion)
            ApplyMoveOnThisBoard(fr, fc, tr, tc, promotion);

            // Đổi lượt
            CurrentTurn = CurrentTurn == PieceColor.White ? PieceColor.Black : PieceColor.White;

            // Sau move, kiểm tra kết thúc ván
            EvaluateGameEnd();

            return true;
        }

        /// <summary>
        /// Kiểm tra hợp lệ (cơ bản) theo luật từng quân nhưng chưa kiểm tra chiếu (pseudo-legal).
        /// </summary>
        public bool IsMoveLegal(string from, string to, bool isOpponent)
        {
            if (!TryParseSquare(from, out int fr, out int fc)) return false;
            if (!TryParseSquare(to, out int tr, out int tc)) return false;

            var piece = _board[fr, fc];
            if (piece == null) return false;

            if (piece.Value.Color != CurrentTurn) return false;

            // target cùng màu
            var target = _board[tr, tc];
            if (target != null && target.Value.Color == piece.Value.Color) return false;

            // pseudo-legal check
            if (!IsMovePseudoLegal(fr, fc, tr, tc, piece.Value)) return false;

            if(!isOpponent)
            {
                // simulate to ensure not leaving king in check
                var clone = CloneInternal();
                clone.ApplyMoveOnClone(fr, fc, tr, tc, 'q'); // promotion default q for legality check

                if (clone.IsKingInCheck(piece.Value.Color)) return false;
            }

            return true;
        }

        // ====================================
        // Helpers: pseudo-legal movement rules
        // (không kiểm tra chiếu, không kiểm tra leaving-king-in-check)
        // ====================================
        private bool IsMovePseudoLegal(int fr, int fc, int tr, int tc, ChessPiece piece)
        {
            int dr = tr - fr;
            int dc = tc - fc;

            switch (piece.Type)
            {
                case PieceType.Pawn:
                    {
                        int dir = piece.Color == PieceColor.White ? -1 : 1;
                        int startRow = piece.Color == PieceColor.White ? 6 : 1;

                        // forward one
                        if (dc == 0 && dr == dir && _board[tr, tc] == null)
                            return true;

                        // two steps from starting row
                        if (dc == 0 && fr == startRow && dr == 2 * dir && _board[fr + dir, fc] == null && _board[tr, tc] == null)
                            return true;

                        // capture diagonal
                        if (Math.Abs(dc) == 1 && dr == dir && _board[tr, tc] != null && _board[tr, tc].Value.Color != piece.Color)
                            return true;

                        // en passant capture: target must be empty but EnPassantTarget == to
                        if (Math.Abs(dc) == 1 && dr == dir && _board[tr, tc] == null && EnPassantTarget == SquareName(tr, tc))
                            return true;

                        // promotion allowed as part of forward one or capture; we'll handle promotion in ApplyMove
                        return false;
                    }

                case PieceType.Rook:
                    if (fr != tr && fc != tc) return false;
                    return PathClear(fr, fc, tr, tc);

                case PieceType.Bishop:
                    if (Math.Abs(fr - tr) != Math.Abs(fc - tc)) return false;
                    return PathClear(fr, fc, tr, tc);

                case PieceType.Queen:
                    if (fr == tr || fc == tc || Math.Abs(fr - tr) == Math.Abs(fc - tc))
                        return PathClear(fr, fc, tr, tc);
                    return false;

                case PieceType.Knight:
                    return (Math.Abs(fr - tr) == 2 && Math.Abs(fc - tc) == 1) ||
                           (Math.Abs(fr - tr) == 1 && Math.Abs(fc - tc) == 2);

                case PieceType.King:
                    // normal one-square king moves
                    if (Math.Abs(fr - tr) <= 1 && Math.Abs(fc - tc) <= 1)
                        return true;

                    // Castling: detect king two-square move
                    if (piece.Color == PieceColor.White && fr == 7 && fc == 4)
                    {
                        // kingside g1 (tr==7,tc==6) or queenside c1 (tr==7,tc==2)
                        if (tc == 6 && WhiteCanCastleKingside && _board[7, 5] == null && _board[7, 6] == null)
                            return true;
                        if (tc == 2 && WhiteCanCastleQueenside && _board[7, 1] == null && _board[7, 2] == null && _board[7, 3] == null)
                            return true;
                    }
                    if (piece.Color == PieceColor.Black && fr == 0 && fc == 4)
                    {
                        if (tc == 6 && BlackCanCastleKingside && _board[0, 5] == null && _board[0, 6] == null)
                            return true;
                        if (tc == 2 && BlackCanCastleQueenside && _board[0, 1] == null && _board[0, 2] == null && _board[0, 3] == null)
                            return true;
                    }
                    return false;
            }

            return false;
        }

        // ====================================
        // Apply move helpers (handle en-passant, castling, promotion)
        // Two variants: on-clone (for simulate) and on-this-board (real execution)
        // ====================================

        private void ApplyMoveOnClone(int fr, int fc, int tr, int tc, char promotion)
        {
            // We apply similar logic as ApplyMoveOnThisBoard but without updating current-turn or GameResult
            var piece = _board[fr, fc];
            if (piece == null) return;

            // reset enpassant target on clone before applying (we will set new one when needed)
            string? prevEnPassant = EnPassantTarget;
            EnPassantTarget = null;

            // Pawn moves: en passant capture and promotion
            if (piece.Value.Type == PieceType.Pawn)
            {
                // en passant capture
                if (fc != tc && _board[tr, tc] == null && prevEnPassant == SquareName(tr, tc))
                {
                    // captured pawn is behind target square (opponent pawn)
                    int capRow = piece.Value.Color == PieceColor.White ? tr + 1 : tr - 1;
                    _board[capRow, tc] = null;
                }

                // move pawn
                _board[tr, tc] = _board[fr, fc];
                _board[fr, fc] = null;

                // promotion?
                int targetRank = GetRankIndexFromRow(tr); // 1..8
                if ((piece.Value.Color == PieceColor.White && targetRank == 8) ||
                    (piece.Value.Color == PieceColor.Black && targetRank == 1))
                {
                    _board[tr, tc] = new ChessPiece(CharToPromotionPiece(promotion), piece.Value.Color);
                }

                // set en passant target if moved two squares
                if (Math.Abs(tr - fr) == 2 && piece.Value.Type == PieceType.Pawn)
                {
                    int betweenRow = (fr + tr) / 2;
                    EnPassantTarget = SquareName(betweenRow, fc);
                }
                return;
            }

            // Castling detection for king two-square move
            if (piece.Value.Type == PieceType.King && Math.Abs(fc - tc) == 2)
            {
                // kingside
                if (tc == 6)
                {
                    // move king
                    _board[tr, tc] = _board[fr, fc];
                    _board[fr, fc] = null;
                    // move rook from h-file to f-file
                    int rookRow = fr;
                    _board[rookRow, 5] = _board[rookRow, 7];
                    _board[rookRow, 7] = null;
                }
                else if (tc == 2)
                {
                    _board[tr, tc] = _board[fr, fc];
                    _board[fr, fc] = null;
                    int rookRow = fr;
                    _board[rookRow, 3] = _board[rookRow, 0];
                    _board[rookRow, 0] = null;
                }

                // castling rights removed in ApplyMoveOnThisBoard normally
                return;
            }

            // Normal piece move
            _board[tr, tc] = _board[fr, fc];
            _board[fr, fc] = null;
        }

        private void ApplyMoveOnThisBoard(int fr, int fc, int tr, int tc, char promotion)
        {
            var piece = _board[fr, fc];
            if (piece == null) return;

            // Clear en passant default (it will be re-set if needed by pawn two-step)
            EnPassantTarget = null;

            // Pawn special
            if (piece.Value.Type == PieceType.Pawn)
            {
                // en passant capture (to square empty but EnPassantTarget == to)
                if (fc != tc && _board[tr, tc] == null && EnPassantTarget == SquareName(tr, tc))
                {
                    int capRow = piece.Value.Color == PieceColor.White ? tr + 1 : tr - 1;
                    _board[capRow, tc] = null;
                }

                // Move pawn
                _board[tr, tc] = _board[fr, fc];
                _board[fr, fc] = null;

                // Promotion check: if reached last rank
                int targetRank = GetRankIndexFromRow(tr); // 1..8
                if ((piece.Value.Color == PieceColor.White && targetRank == 8) ||
                    (piece.Value.Color == PieceColor.Black && targetRank == 1))
                {
                    var promType = CharToPromotionPiece(promotion); // convert 'q','r','b','n'
                    _board[tr, tc] = new ChessPiece(promType, piece.Value.Color);
                }

                // If pawn moved 2 squares, set EnPassantTarget to the square jumped over
                if (Math.Abs(tr - fr) == 2)
                {
                    int betweenRow = (fr + tr) / 2;
                    EnPassantTarget = SquareName(betweenRow, fc);
                }
                else
                {
                    EnPassantTarget = null;
                }

                // moving pawn cancels castling rights? No
                return;
            }

            // Castling move
            if (piece.Value.Type == PieceType.King && Math.Abs(fc - tc) == 2)
            {
                // move king
                _board[tr, tc] = _board[fr, fc];
                _board[fr, fc] = null;

                // rook move
                if (tc == 6)
                {
                    // kingside
                    _board[fr, 5] = _board[fr, 7];
                    _board[fr, 7] = null;
                }
                else if (tc == 2)
                {
                    _board[fr, 3] = _board[fr, 0];
                    _board[fr, 0] = null;
                }

                // revoke castling rights for that color
                if (piece.Value.Color == PieceColor.White)
                {
                    WhiteCanCastleKingside = WhiteCanCastleQueenside = false;
                }
                else
                {
                    BlackCanCastleKingside = BlackCanCastleQueenside = false;
                }
                EnPassantTarget = null;
                return;
            }

            // normal move
            _board[tr, tc] = _board[fr, fc];
            _board[fr, fc] = null;

            // If king moves, revoke castling rights for that color
            if (piece.Value.Type == PieceType.King)
            {
                if (piece.Value.Color == PieceColor.White)
                {
                    WhiteCanCastleKingside = WhiteCanCastleQueenside = false;
                }
                else
                {
                    BlackCanCastleKingside = BlackCanCastleQueenside = false;
                }
                EnPassantTarget = null;
                return;
            }

            // If rook moves, adjust castling rights accordingly
            if (piece.Value.Type == PieceType.Rook)
            {
                if (piece.Value.Color == PieceColor.White)
                {
                    // rook from a1 (7,0) or h1 (7,7) in this internal layout
                    if (fr == 7 && fc == 0) WhiteCanCastleQueenside = false;
                    if (fr == 7 && fc == 7) WhiteCanCastleKingside = false;
                }
                else
                {
                    if (fr == 0 && fc == 0) BlackCanCastleQueenside = false;
                    if (fr == 0 && fc == 7) BlackCanCastleKingside = false;
                }
            }

            // reset en-passant if not set by pawn two-step (already cleared)
            EnPassantTarget = null;
        }

        // ====================================
        // Game end evaluation (checkmate/stalemate)
        // ====================================
        private void EvaluateGameEnd()
        {
            // CurrentTurn đã được chuyển sang opponent trước khi hàm này được gọi.
            var opponent = CurrentTurn; // opponent = bên phải đi tiếp (người bị kiểm tra)
            var mover = opponent == PieceColor.White ? PieceColor.Black : PieceColor.White; // ai vừa đi

            // 0) Nếu đối thủ (opponent) không còn vua -> bên vừa đi (mover) thắng ngay
            if (!HasKing(opponent))
            {
                IsGameOver = true;
                GameResult = (mover == PieceColor.White) ? GameResult.WhiteWins : GameResult.BlackWins;
                return;
            }

            // 1) Kiểm tra xem opponent có còn bất kỳ nước pseudo-legal nào không
            bool opponentHasLegal = OpponentHasAnyLegalMove(opponent);

            if (!opponentHasLegal)
            {
                // Nếu opponent không có nước pseudo-legal nào nữa thì:
                // - nếu đang bị chiếu => checkmate
                // - ngược lại => stalemate (hòa)
                if (IsKingInCheck(opponent))
                {
                    IsGameOver = true;
                    GameResult = (mover == PieceColor.White) ? GameResult.WhiteWins : GameResult.BlackWins;
                }
                else
                {
                    IsGameOver = true;
                    GameResult = GameResult.Draw;
                }
            }
            else
            {
                IsGameOver = false;
                GameResult = GameResult.None;
            }
        }

        private bool HasKing(PieceColor color)
        {
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var p = _board[r, c];
                    if (p.HasValue && p.Value.Type == PieceType.King && p.Value.Color == color)
                        return true;
                }
            }
            return false;
        }

        private bool OpponentHasAnyLegalMove(PieceColor opponent)
        {
            // verify if opponent has any legal move
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var p = _board[r, c];
                    if (p == null || p.Value.Color != opponent) continue;

                    // iterate all targets
                    for (int tr = 0; tr < 8; tr++)
                    {
                        for (int tc = 0; tc < 8; tc++)
                        {
                            // skip same square
                            if (tr == r && tc == c) continue;

                            // pseudo-legal?
                            // Nếu nước đi này giả định hợp lệ theo kiểu di chuyển quân
                            if (IsMovePseudoLegal(r, c, tr, tc, p.Value))
                            {
                                // ✅ Bỏ kiểm tra vua bị chiếu, chỉ cần có ít nhất 1 nước hợp lệ là OK
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        // ====================================
        // King in check detection
        // ====================================
        private bool IsKingInCheck(PieceColor color)
        {
            // find king
            int kingR = -1, kingC = -1;
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var p = _board[r, c];
                    if (p.HasValue && p.Value.Type == PieceType.King && p.Value.Color == color)
                    {
                        kingR = r; kingC = c;
                        break;
                    }
                }
                if (kingR != -1) break;
            }
            if (kingR == -1) return false; // king missing -> treat as not check (but should not happen)

            var attacker = color == PieceColor.White ? PieceColor.Black : PieceColor.White;

            // scan all opponent pieces and see if any pseudo-legal attack reaches king
            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var p = _board[r, c];
                    if (!p.HasValue || p.Value.Color != attacker) continue;

                    if (IsPseudoAttack(r, c, kingR, kingC, p.Value))
                        return true;
                }
            }

            return false;
        }

        // Similar to IsMovePseudoLegal but adapted for attack detection (pawns attack differently)
        private bool IsPseudoAttack(int fr, int fc, int tr, int tc, ChessPiece piece)
        {
            int dr = tr - fr;
            int dc = tc - fc;

            switch (piece.Type)
            {
                case PieceType.Pawn:
                    {
                        int dir = piece.Color == PieceColor.White ? -1 : 1;
                        // pawn attacks diagonally only
                        return (dr == dir && Math.Abs(dc) == 1);
                    }
                case PieceType.Rook:
                    if (fr != tr && fc != tc) return false;
                    return PathClear(fr, fc, tr, tc);
                case PieceType.Bishop:
                    if (Math.Abs(fr - tr) != Math.Abs(fc - tc)) return false;
                    return PathClear(fr, fc, tr, tc);
                case PieceType.Queen:
                    if (fr == tr || fc == tc || Math.Abs(fr - tr) == Math.Abs(fc - tc))
                        return PathClear(fr, fc, tr, tc);
                    return false;
                case PieceType.Knight:
                    return (Math.Abs(fr - tr) == 2 && Math.Abs(fc - tc) == 1) ||
                           (Math.Abs(fr - tr) == 1 && Math.Abs(fc - tc) == 2);
                case PieceType.King:
                    return Math.Abs(fr - tr) <= 1 && Math.Abs(fc - tc) <= 1;
            }
            return false;
        }

        // ====================================
        // Clone helper for simulation
        // ====================================
        private ChessBoardState CloneInternal()
        {
            var nb = new ChessBoardState();
            // clear default initial setup
            nb.Clear();

            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    nb._board[r, c] = this._board[r, c];

            nb.CurrentTurn = this.CurrentTurn;
            nb.WhiteCanCastleKingside = this.WhiteCanCastleKingside;
            nb.WhiteCanCastleQueenside = this.WhiteCanCastleQueenside;
            nb.BlackCanCastleKingside = this.BlackCanCastleKingside;
            nb.BlackCanCastleQueenside = this.BlackCanCastleQueenside;
            nb.EnPassantTarget = this.EnPassantTarget;
            nb.IsGameOver = this.IsGameOver;
            nb.GameResult = this.GameResult;
            return nb;
        }



        /// <summary>
        /// Kiểm tra đường đi có trống không (dành cho quân đi thẳng hoặc chéo).
        /// </summary>
        private bool PathClear(int fr, int fc, int tr, int tc)
        {
            int dr = Math.Sign(tr - fr);
            int dc = Math.Sign(tc - fc);
            int r = fr + dr, c = fc + dc;

            while (r != tr || c != tc)
            {
                if (_board[r, c] != null)
                    return false;
                r += dr;
                c += dc;
            }
            return true;
        }

        /// <summary>
        /// Chuyển ký hiệu “a1” sang chỉ số hàng, cột.
        /// // Parse "e2" -> row(0..7), col(0..7) with row=0 => rank8
        /// </summary>
        public static bool TryParseSquare(string square, out int row, out int col)
        {
            row = col = -1;
            if (string.IsNullOrWhiteSpace(square) || square.Length < 2) return false;
            char f = char.ToLower(square[0]);
            char r = square[1];
            if (f < 'a' || f > 'h') return false;
            if (r < '1' || r > '8') return false;
            col = f - 'a';
            int rank = r - '1'; // 0..7 with 0 => rank1
            row = 7 - rank;     // map to internal 0..7 where 0 is rank8
            return true;
        }

        private static string SquareName(int row, int col)
        {
            int rank = 8 - row; // row0->rank8
            char file = (char)('a' + col);
            return $"{file}{rank}";
        }

        // Given internal row (0..7), produce rank index 1..8
        private static int GetRankIndexFromRow(int row)
        {
            // returns 1..8 rank number
            // row 0 => rank 8; row 7 => rank 1
            return 8 - row;
        }

        private static PieceType CharToPromotionPiece(char c)
        {
            c = char.ToLower(c);
            return c switch
            {
                'q' => PieceType.Queen,
                'r' => PieceType.Rook,
                'b' => PieceType.Bishop,
                'n' => PieceType.Knight,
                _ => PieceType.Queen
            };
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
