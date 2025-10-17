namespace MyGames.Desktop.Models
{
    /// <summary>
    /// Đại diện cho một nước đi trong ván cờ.
    /// </summary>
    public class ChessMove
    {
        /// <summary>
        /// Số thứ tự nước đi (1, 2, 3, ...)
        /// </summary>
        public int MoveNumber { get; set; }

        /// <summary>
        /// Ký hiệu nước đi theo chuẩn SAN (ví dụ: "e4", "Nf3", "Qxe5")
        /// </summary>
        public string MoveNotation { get; set; } = string.Empty;

        /// <summary>
        /// Thời điểm thực hiện nước đi
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Người thực hiện nước đi (Trắng hoặc Đen)
        /// </summary>
        public PlayerColor Player { get; set; }

        public override string ToString()
        {
            return $"{MoveNumber}. {Player}: {MoveNotation} ({Timestamp:T})";
        }
    }
}
