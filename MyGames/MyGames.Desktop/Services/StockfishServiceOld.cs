using Microsoft.Extensions.DependencyInjection;
using MyGames.Desktop.Logs;
using MyGames.Desktop.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Windows;

namespace MyGames.Desktop.Services
{
    /// <summary>
    /// Dịch vụ giao tiếp với engine Stockfish (chạy ở local).
    /// Dùng để phân tích, gợi ý nước đi.
    /// </summary>
    public class StockfishServiceOld : IDisposable
    {
        private Process? _stockfishProcess;
        private StreamWriter? _inputWriter;
        private StreamReader? _outputReader;
        private readonly object _lock = new();
        public bool IsRunning => _stockfishProcess != null && !_stockfishProcess.HasExited;
        private readonly LoggerService _logger;
        private readonly SemaphoreSlim _engineLock = new(1, 1);
        public StockfishServiceOld(LoggerService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        public async Task<string> SendCommandAsync(string command, int timeoutMs = 8000, CancellationToken? externalToken = null, bool waitForResponse = true)
        {
            await _engineLock.WaitAsync();
            try
            {
                if (!IsRunning)
                    throw new InvalidOperationException("Stockfish chưa được khởi động.");

                await _inputWriter!.WriteLineAsync(command);
                await _inputWriter.FlushAsync();
                _logger.Info($"➡ [{DateTime.Now:HH:mm:ss.fff}] Gửi: {command}");

                // Một số lệnh không có phản hồi (ucinewgame, position, setoption, stop)
                if (!waitForResponse)
                {
                    return "[no wait]";
                }
                var sb = new StringBuilder();
                using var cts = new CancellationTokenSource(timeoutMs);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, externalToken ?? CancellationToken.None);
                var token = linked.Token;

                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        string? line = await _outputReader!.ReadLineAsync().ConfigureAwait(false);
                        if (line == null) break;
                        line = line.Trim();
                        sb.AppendLine(line);
                        if (!string.IsNullOrEmpty(line))
                            _logger.Info($"[Stockfish] {line}");

                        if (line.StartsWith("bestmove", StringComparison.OrdinalIgnoreCase) ||
                            line.Equals("readyok", StringComparison.OrdinalIgnoreCase) ||
                            line.Equals("uciok", StringComparison.OrdinalIgnoreCase))
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    sb.AppendLine($"[timeout after {timeoutMs} ms]");
                    _logger.Warn($"⏱ Timeout {command}");
                }

                var result = sb.ToString();
                _logger.Info($"⬅ Hoàn tất {command}: {result}");
                return result;
            }
            finally
            {
                _engineLock.Release();
            }
        }
        public async Task<string> GetBestMoveAsync(string movesOrFen, int depth = 12, int timeoutMs = 10000, CancellationToken? token = null)
        {
            if (!IsRunning)
                throw new InvalidOperationException("Stockfish chưa được khởi động.");

            _logger.Info($"♟ [DEBUG] >>> BẮT ĐẦU PHÂN TÍCH depth={depth} <<<");

            // Các lệnh không có phản hồi, chỉ gửi
            await SendCommandAsync("ucinewgame", waitForResponse: false);
            await SendCommandAsync("isready", 2000, token);

            string posCmd = (!string.IsNullOrEmpty(movesOrFen) && movesOrFen.Contains('/'))
                ? $"position fen {movesOrFen}"
                : $"position startpos moves {movesOrFen}";
            await SendCommandAsync(posCmd, waitForResponse: false);

            _logger.Info("🧠 Gửi lệnh 'go depth'...");
            string output = await SendCommandAsync($"go depth {depth}", timeoutMs, token);

            var lineBest = output.Split('\n').FirstOrDefault(l => l.StartsWith("bestmove"));
            if (lineBest != null)
            {
                _logger.Info($"💡 [DEBUG] Bestmove từ engine: {lineBest}");
                return lineBest;
            }

            _logger.Warn($"⚠ [DEBUG] Engine không trả bestmove. Raw output:\n{output}");
            return "(Không có gợi ý)";
        }

        public async Task<double?> GetEvaluationAsync(string movesUci, int depth = 15)
        {
            if (!IsRunning)
                throw new InvalidOperationException("Stockfish chưa được khởi động.");

            await SendCommandAsync("uci");
            await SendCommandAsync("ucinewgame");
            await SendCommandAsync($"position startpos moves {movesUci}");
            await SendCommandAsync($"go depth {depth}");

            double? eval = null;

            // đọc output stockfish
            while (true)
            {
                string? line = await _outputReader.ReadLineAsync();
                if (line == null) break;

                if (line.StartsWith("info depth") && line.Contains("score"))
                {
                    // Ví dụ: info depth 15 score cp 23 ...
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int scoreIdx = Array.IndexOf(parts, "score");
                    if (scoreIdx >= 0 && scoreIdx + 2 < parts.Length)
                    {
                        string type = parts[scoreIdx + 1];
                        string val = parts[scoreIdx + 2];

                        if (type == "cp" && double.TryParse(val, out double cp))
                            eval = cp / 100.0; // centipawn -> pawn units
                        else if (type == "mate" && double.TryParse(val, out double mate))
                            eval = mate > 0 ? 9999 : -9999;
                    }
                }

                if (line.StartsWith("bestmove"))
                    break;
            }

            return eval;
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
            catch (Exception ex)
            {
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
