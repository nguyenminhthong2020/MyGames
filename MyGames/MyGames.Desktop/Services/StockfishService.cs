using Microsoft.Extensions.DependencyInjection;
using MyGames.Desktop.Logs;
using MyGames.Desktop.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

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

        private readonly LoggerService _logger;

        private readonly SemaphoreSlim _engineLock = new(1, 1);

        public StockfishService(LoggerService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Khởi động tiến trình Stockfish từ file thực thi.
        /// </summary>
        public void StartOld(string stockfishPath)
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
            bool isSuccess = _stockfishProcess.Start();

            _inputWriter = _stockfishProcess.StandardInput;
            _outputReader = _stockfishProcess.StandardOutput;

            // Gửi lệnh "uci" để kiểm tra kết nối engine
            _inputWriter.WriteLine("uci");
            _inputWriter.Flush();
        }

        public void Start(string stockfishPath)
        {
            if (!File.Exists(stockfishPath))
                throw new FileNotFoundException("Không tìm thấy file Stockfish tại", stockfishPath);

            if (IsRunning)
            {
                _logger.Warn("Stockfish đã chạy rồi — bỏ qua Start().");
                return;
            }

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

            _logger.Info($"🚀 Stockfish started (PID={_stockfishProcess.Id})");

            // Gửi lệnh uci
            _inputWriter.WriteLine("uci");
            _inputWriter.Flush();

            // Chờ phản hồi "uciok" trong 3s
            var startTime = DateTime.Now;
            string? line;
            bool uciOk = false;

            while ((DateTime.Now - startTime).TotalMilliseconds < 3000)
            {
                if (_stockfishProcess.HasExited)
                {
                    _logger.Error("❌ Stockfish process exited sớm trước khi gửi 'uciok'.");
                    return;
                }

                line = _outputReader.ReadLine();
                if (line == null) continue;

                _logger.Info($"[Stockfish] {line}");
                if (line.Contains("uciok"))
                {
                    uciOk = true;
                    break;
                }
            }

            if (!uciOk)
            {
                _logger.Warn("⚠ Stockfish không phản hồi 'uciok' trong 3 giây — có thể chưa sẵn sàng.");
            }
            else
            {
                _logger.Info("✅ Stockfish sẵn sàng nhận lệnh UCI.");
            }
        }

        public async Task<string> SendCommandAsync(string command, int timeoutMs = 5000, CancellationToken? externalToken = null)
        {
            await _engineLock.WaitAsync();
            try
            {
                if (!IsRunning)
                {
                    _logger.Error("⚠ SendCommandAsync được gọi nhưng Stockfish chưa chạy hoặc đã thoát.");
                    throw new InvalidOperationException("Stockfish chưa được khởi động.");
                }

                lock (_lock)
                {
                    _inputWriter!.WriteLine(command);
                    _inputWriter!.Flush();
                }

                _logger.Info($"➡ Gửi lệnh: {command}");
                var sb = new StringBuilder();
                string? line;

                using var cts = new CancellationTokenSource(timeoutMs);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, externalToken ?? CancellationToken.None);

                try
                {
                    while (!linked.Token.IsCancellationRequested)
                    {
                        line = await _outputReader!.ReadLineAsync();
                        if (line == null)
                            break;

                        sb.AppendLine(line);
                        if (line.StartsWith("bestmove"))
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    sb.AppendLine($"[timeout after {timeoutMs} ms]");
                }

                _logger.Info($"⬅ Kết quả từ Stockfish ({command}): {sb}");
                return sb.ToString();
            }
            finally
            {
                _engineLock.Release();
            }
        }

        public async Task<string> GetBestMoveAsync(string fenOrMoves, int depth = 15, int timeoutMs = 5000, CancellationToken? token = null)
        {
            if (!IsRunning)
                throw new InvalidOperationException("Stockfish chưa được khởi động.");

            _logger.Info($"♟ Bắt đầu phân tích FEN (depth={depth})");

            // ✅ FIX: tự động nhận biết FEN vs moves
            string cmd;
            if (fenOrMoves.Contains('/'))
            {
                cmd = $"position fen {fenOrMoves}";
            }
            else
            {
                cmd = $"position startpos moves {fenOrMoves}";
            }

            await SendCommandAsync(cmd, timeoutMs, token);
            string output = await SendCommandAsync($"go depth {depth}", timeoutMs, token);

            foreach (var line in output.Split('\n'))
            {
                if (line.StartsWith("bestmove"))
                {
                    _logger.Info($"💡 Gợi ý: {line}");
                    return line;
                }
            }

            _logger.Warn("⚠ Không tìm thấy 'bestmove' trong output.");
            return "(timeout hoặc không tìm thấy bestmove)";
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
                    _logger.Info("🛑 Dừng Stockfish...");
                    _inputWriter?.WriteLine("quit");
                    _inputWriter?.Flush();
                    _stockfishProcess?.WaitForExit(1000);
                }
            }
            catch (Exception ex){
                _logger.Error($"Lỗi khi dispose Stockfish: {ex.Message}");
            }
            finally
            {
                _inputWriter?.Dispose();
                _outputReader?.Dispose();
                _stockfishProcess?.Dispose();
            }
        }
    }
}
