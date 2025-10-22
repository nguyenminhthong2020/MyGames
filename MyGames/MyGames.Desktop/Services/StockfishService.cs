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

        ///// <summary>
        ///// Gửi command tới Stockfish và đọc phản hồi một cách an toàn (async, không dùng Peek()).
        ///// - Nếu waitForBestMove == true => đọc tới khi thấy "bestmove".
        ///// - Nếu waitForBestMove == false => đọc tới khi thấy "readyok" hoặc "uciok" (tùy command).
        ///// Hàm có timeout để tránh treo vô hạn.
        ///// </summary>
        //public async Task<string> SendCommandAsyncOld(string command, int timeoutMs = 10_000, CancellationToken? externalToken = null, bool waitForBestMove = true)
        //{
        //    await _engineLock.WaitAsync();
        //    try
        //    {
        //        if (!IsRunning)
        //        {
        //            _logger.Error("⚠ SendCommandAsync được gọi nhưng Stockfish chưa chạy hoặc đã thoát.");
        //            throw new InvalidOperationException("Stockfish chưa được khởi động.");
        //        }

        //        // Ghi lệnh tới stdin (async)
        //        try
        //        {
        //            await _inputWriter!.WriteLineAsync(command).ConfigureAwait(false);
        //            await _inputWriter.FlushAsync().ConfigureAwait(false);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.Error($"❌ Lỗi khi ghi lệnh vào Stockfish stdin: {ex.Message}");
        //            throw;
        //        }

        //        _logger.Info($"➡ Gửi lệnh: {command} (waitForBestMove={waitForBestMove})");

        //        var sb = new StringBuilder();
        //        using var cts = new CancellationTokenSource(timeoutMs);
        //        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, externalToken ?? CancellationToken.None);
        //        var token = linked.Token;

        //        try
        //        {
        //            // ReadLineAsync luôn an toàn (không dùng Peek). Vòng lặp dừng khi token bị cancel hoặc gặp marker.
        //            while (!token.IsCancellationRequested)
        //            {
        //                // Read next line (async). Nếu engine chưa trả dòng nào, ReadLineAsync sẽ await — nhưng sẽ bị hủy bởi token khi timeout.
        //                string? line = await _outputReader!.ReadLineAsync().ConfigureAwait(false);

        //                // Nếu null => stream đóng (hiếm khi xảy ra với engine chạy ngầm)
        //                if (line == null)
        //                {
        //                    _logger.Warn("⚠ _outputReader returned null (stream có thể đã đóng).");
        //                    break;
        //                }

        //                line = line.Trim();
        //                if (!string.IsNullOrEmpty(line))
        //                    _logger.Info($"[Stockfish] {line}");

        //                sb.AppendLine(line);

        //                // Quy tắc dừng:
        //                if (waitForBestMove)
        //                {
        //                    // Trong chế độ chờ bestmove: dừng khi thấy bestmove
        //                    if (line.StartsWith("bestmove", StringComparison.OrdinalIgnoreCase))
        //                        break;
        //                }
        //                else
        //                {
        //                    // Không chờ bestmove: dừng khi thấy readyok / uciok
        //                    if (string.Equals(line, "readyok", StringComparison.OrdinalIgnoreCase) ||
        //                        string.Equals(line, "uciok", StringComparison.OrdinalIgnoreCase))
        //                    {
        //                        break;
        //                    }

        //                    // Một số lệnh (ví dụ ucinewgame) thường không trả gì — nên loop sẽ chờ tới timeout rồi thoát.
        //                    // Chúng ta tiếp tục đọc nếu engine phát ra output khác (ví dụ option list sau 'uci').
        //                }
        //            }
        //        }
        //        catch (OperationCanceledException)
        //        {
        //            // timeout hoặc external token cancel
        //            sb.AppendLine($"[timeout after {timeoutMs} ms]");
        //            _logger.Warn($"⏱ SendCommandAsync('{command}') timed out after {timeoutMs} ms.");
        //        }
        //        catch (Exception ex)
        //        {
        //            sb.AppendLine($"[error: {ex.Message}]");
        //            _logger.Error($"❌ Exception khi đọc response từ Stockfish: {ex}");
        //        }

        //        string result = sb.ToString();
        //        _logger.Info($"⬅ Kết quả từ Stockfish ({command}): {result}");
        //        return result;
        //    }
        //    finally
        //    {
        //        _engineLock.Release();
        //    }
        //}

        //public async Task<string> GetBestMoveAsyncOld(string fenOrMoves, int depth = 15, int timeoutMs = 5000, CancellationToken? token = null)
        //{
        //    if (!IsRunning)
        //        throw new InvalidOperationException("Stockfish chưa được khởi động.");

        //    _logger.Info($"♟ Bắt đầu phân tích FEN/moves (depth={depth})");

        //    // 0) đảm bảo engine sẵn sàng
        //    await SendCommandAsync("ucinewgame", 1000, token, waitForBestMove: false);
        //    await SendCommandAsync("isready", 2000, token, waitForBestMove: false);

        //    // 1) chuẩn bị position
        //    // tự động nhận biết FEN vs moves
        //    string cmd;
        //    if (!string.IsNullOrEmpty(fenOrMoves) && fenOrMoves.Contains('/'))
        //    {
        //        cmd = $"position fen {fenOrMoves}";
        //    }
        //    else if (!string.IsNullOrWhiteSpace(fenOrMoves))
        //    {
        //        cmd = $"position startpos moves {fenOrMoves}";
        //    }
        //    else
        //    {
        //        cmd = "position startpos";
        //    }


        //    // gửi position nhưng KHÔNG chờ bestmove (position không tạo bestmove).
        //    await SendCommandAsync(cmd, 2000, token, waitForBestMove: false);


        //    // 2) Gọi go và chờ bestmove
        //    string output = await SendCommandAsync($"go depth {depth}", timeoutMs, token, waitForBestMove: true);

        //    if (string.IsNullOrWhiteSpace(output))
        //    {
        //        _logger.Warn("Stockfish không trả lời trong giới hạn thời gian.");
        //        return "Error::(timeout hoặc không có phản hồi)";
        //    }

        //    foreach (var line in output.Split('\n'))
        //    {
        //        if (line.StartsWith("bestmove"))
        //        {
        //            _logger.Info($"💡 Gợi ý row: {line}");
        //            return line;
        //        }
        //    }

        //    _logger.Warn("⚠ Không tìm thấy 'bestmove' trong output.");
        //    return "(timeout hoặc không tìm thấy bestmove)";
        //}

        public async Task<string> SendCommandAsync(
    string command, int timeoutMs = 8000,
    CancellationToken? externalToken = null,
    bool waitForResponse = true)
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

        public async Task<string> GetBestMoveAsync(string movesOrFen, int depth = 12,
            int timeoutMs = 10000, CancellationToken? token = null)
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
