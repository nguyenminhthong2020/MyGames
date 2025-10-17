using MyGames.Desktop.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGames.Desktop.Helpers
{
    public static class SanToUciConverter
    {
        // Bảng ánh xạ ký tự quân cờ SAN → chữ cái
        private static readonly char[] Files = "abcdefgh".ToCharArray();
        private static readonly char[] Ranks = "12345678".ToCharArray();

        public static string? ConvertSanToUci(string san, List<string> moveHistory, string startingFen = "startpos")
        {
            if (string.IsNullOrWhiteSpace(san))
                return string.Empty;

            san = san.Trim();

            // 1. Trường hợp đặc biệt: nhập thành
            if (san == "O-O" || san == "0-0")
                return "e1g1"; // giả định bên trắng, extension sẽ gửi thêm "side" nếu muốn phân biệt
            if (san == "O-O-O" || san == "0-0-0")
                return "e1c1";

            // 2. Loại bỏ ký hiệu chiếu (+, #)
            san = san.Replace("+", "").Replace("#", "");

            // 3. Phong cấp (ví dụ: e8=Q hoặc exd8=Q)
            string? promotion = null;
            if (san.Contains('='))
            {
                int idx = san.IndexOf('=');
                promotion = san.Substring(idx + 1, 1).ToLower();
                san = san.Substring(0, idx);
            }

            // 4. Nếu chỉ là nước tốt đi thẳng (vd: e4)
            if (san.Length == 2 && IsSquare(san))
            {
                string dest = san;
                string srcFile = dest[0].ToString();
                string srcRank = "2"; // mặc định trắng đi từ hàng 2
                return $"{srcFile}{srcRank}{dest}{promotion}";
            }

            // 5. Nếu có dạng "exd5" (tốt bắt quân)
            if (san.Length == 4 && san[1] == 'x' && IsSquare(san.Substring(2)))
            {
                string srcFile = san[0].ToString();
                string dest = san.Substring(2);
                string srcRank = "4"; // giả định trắng, đơn giản
                return $"{srcFile}{srcRank}{dest}{promotion}";
            }

            // 6. Nước đi quân khác (vd: Nf3, Rxe5, Qh4)
            char piece = 'P';
            int pos = 0;

            if ("KQRBN".Contains(san[0]))
            {
                piece = san[0];
                pos = 1;
            }

            // Bỏ 'x' (ăn quân)
            string pure = san.Substring(pos).Replace("x", "");

            // Xác định đích
            string destSq = pure.Length >= 2 ? pure.Substring(pure.Length - 2) : "";
            string srcHint = pure.Length > 2 ? pure.Substring(0, pure.Length - 2) : "";

            // Tạo UCI thô (không tra vị trí thật)
            string src = (srcHint.Length == 2 && IsSquare(srcHint))
                ? srcHint
                : GuessSourceSquare(piece, destSq, srcHint);

            return $"{src}{destSq}{promotion}";
        }

        private static bool IsSquare(string s)
        {
            return s.Length == 2 && Files.Contains(s[0]) && Ranks.Contains(s[1]);
        }

        private static string GuessSourceSquare(char piece, string dest, string hint)
        {
            // Đây là bản đoán đơn giản: bạn có thể mở rộng sau
            // Mặc định các quân đặt ở vị trí xuất phát của trắng
            return piece switch
            {
                'N' => "g1",
                'B' => "f1",
                'R' => "h1",
                'Q' => "d1",
                'K' => "e1",
                _ => "e2"
            };
        }

        #region Code mới
        /// <summary>
        /// Chuyển một nước SAN (ví dụ "Nf3") sang UCI (ví dụ "g1f3").
        /// Hiện tại chỉ là mô phỏng – sau này có thể tích hợp parser chess thật.
        /// </summary>
        public static string? ConvertSanToUci(string san)
        {
            if (string.IsNullOrWhiteSpace(san))
                return null;

            san = san.Trim();

            // Nếu đã có dạng UCI (4 ký tự, toàn chữ/số), thì trả lại luôn
            if (san.Length == 4 &&
                san.All(c => char.IsLetterOrDigit(c)) &&
                san[0] is >= 'a' and <= 'h')
                return san.ToLower();

            // Nếu là dạng "e4", tạm cho là tốt từ cột e lên hàng 4 (giả lập)
            if (san.Length == 2)
            {
                char file = san[0];
                char rank = san[1];
                return $"{file}2{file}{rank}".ToLower(); // ví dụ: e4 -> e2e4
            }

            // Chưa xử lý dạng phức tạp (Nf3, exd5, O-O, v.v.)
            return null;
        }

        /// <summary>
        /// Cố gắng chuyển toàn bộ danh sách moves (SAN) thành UCI.
        /// Trả về true nếu sau khi xử lý, tất cả moves đều là UCI.
        /// </summary>
        public static bool TryConvertAllToUci(List<ChessMove> moves, string startingFen = "startpos")
        {
            bool allConverted = true;

            for (int i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (string.IsNullOrWhiteSpace(move.MoveNotation))
                    continue;

                // Nếu chưa là UCI thì thử convert
                if (!IsUciFormat(move.MoveNotation))
                {
                    var uci = ConvertSanToUci(move.MoveNotation);
                    if (uci != null)
                        move.MoveNotation = uci;
                    else
                        allConverted = false;
                }
            }

            return allConverted;
        }

        private static bool IsUciFormat(string move)
        {
            move = move.Trim().ToLower();
            return move.Length == 4 &&
                   move[0] is >= 'a' and <= 'h' &&
                   move[2] is >= 'a' and <= 'h';
        }

        #endregion
    }
}
