using System.Diagnostics;
using System.IO;
using System.Text;

namespace MyGames.Desktop.Services
{
    /// <summary>
    /// Dịch vụ giao tiếp với engine Stockfish (chạy ở local).
    /// Dùng để phân tích, gợi ý nước đi.
    /// </summary>
    public class StockfishService : IDisposable
    {
        private Process? _stockfishProcess;
        private StreamWriter? _inputWriter;
        private StreamReader? _outputReader;

        private readonly object _lock = new();

        public bool IsRunning => _stockfishProcess != null && !_stockfishProcess.HasExited;

        /// <summary>
        /// Khởi động tiến trình Stockfish từ file thực thi.
        /// </summary>
        public void Start(string stockfishPath)
        {
            if (!File.Exists(stockfishPath))
                throw new FileNotFoundException("Không tìm thấy file Stockfish tại", stockfishPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = stockfishPath,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _stockfishProcess = new Process { StartInfo = startInfo };
            _stockfishProcess.Start();

            _inputWriter = _stockfishProcess.StandardInput;
            _outputReader = _stockfishProcess.StandardOutput;

            // Gửi lệnh "uci" để kiểm tra kết nối engine
            _inputWriter.WriteLine("uci");
            _inputWriter.Flush();
        }

        /// <summary>
        /// Gửi lệnh đến Stockfish và chờ phản hồi.
        /// </summary>
        public async Task<string> SendCommandAsync(string command)
        {
            if (!IsRunning)
                throw new InvalidOperationException("Stockfish chưa được khởi động.");

            lock (_lock)
            {
                _inputWriter!.WriteLine(command);
                _inputWriter!.Flush();
            }

            // Đọc output (đơn giản, chưa có timeout nâng cao)
            var sb = new StringBuilder();
            string? line;
            while ((line = await _outputReader!.ReadLineAsync()) != null)
            {
                if (line.Contains("bestmove"))
                {
                    sb.AppendLine(line);
                    break;
                }
                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Phân tích vị trí hiện tại và gợi ý nước đi tốt nhất.
        /// </summary>
        public async Task<string> GetBestMoveAsync(string fen, int depth = 15)
        {
            if (!IsRunning)
                throw new InvalidOperationException("Stockfish chưa được khởi động.");

            await SendCommandAsync($"position fen {fen}");
            string output = await SendCommandAsync($"go depth {depth}");

            // Tìm dòng "bestmove ..."
            foreach (var line in output.Split('\n'))
            {
                if (line.StartsWith("bestmove"))
                    return line;
            }

            return "Không tìm thấy bestmove";
        }

        public string GetBestMove(string fen, int depth = 15)
        {
            return GetBestMoveAsync(fen, depth).GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            try
            {
                if (IsRunning)
                {
                    _inputWriter?.WriteLine("quit");
                    _inputWriter?.Flush();
                    _stockfishProcess?.WaitForExit(1000);
                }
            }
            catch { /* ignored */ }
            finally
            {
                _inputWriter?.Dispose();
                _outputReader?.Dispose();
                _stockfishProcess?.Dispose();
            }
        }
    }
}
