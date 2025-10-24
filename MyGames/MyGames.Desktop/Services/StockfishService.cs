using MyGames.Desktop.Logs;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Channels;

namespace MyGames.Desktop.Services
{
    public enum StockfishJobType
    {
        BestMove,
        Evaluation,
        CustomCommand
    }

    public class StockfishJob
    {
        public StockfishJobType Type { get; set; }
        public string MovesOrFen { get; set; } = "";
        public int Depth { get; set; } = 12;
        public int TimeoutMs { get; set; } = 8000;
        public Action<string>? OnCompleted { get; set; }
    }

    public class StockfishService : IDisposable
    {
        private Process? _stockfishProcess;
        private StreamWriter? _inputWriter;
        private StreamReader? _outputReader;
        private readonly LoggerService _logger;
        private readonly AppSettings _appSettings;

        // --- Engine queue ---
        private readonly Channel<Func<Task>> _engineQueue = Channel.CreateUnbounded<Func<Task>>();
        private readonly CancellationTokenSource _queueCts = new();
        private readonly Task _queueWorkerTask;

        public bool IsRunning => _stockfishProcess != null && !_stockfishProcess.HasExited;

        //private readonly Channel<StockfishJob> _jobQueue = Channel.CreateUnbounded<StockfishJob>();
        //private readonly Task _jobWorker;

        public StockfishService(LoggerService logger, AppSettings appSettings)
        {
            _logger = logger;
            _appSettings = appSettings;

            //_queueWorkerTask = Task.Run(ProcessEngineQueueAsync);
            //_jobWorker = Task.Run(ProcessStockfishJobsAsync);
            Start(_appSettings.StockfishPath);
            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessQueueAsync();
                }
                catch (Exception ex)
                {
                    _logger.Error($"❌ ProcessQueueAsync chết ngay khi khởi tạo: {ex}");
                }
            });

        }

        //========================= ENGINE CORE =========================

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

            _stockfish = _stockfishProcess;

            _inputWriter = _stockfishProcess.StandardInput;
            _outputReader = _stockfishProcess.StandardOutput;

            _logger.Info($"🚀 Stockfish started (PID={_stockfishProcess.Id})");

            _inputWriter.WriteLine("uci");
            _inputWriter.Flush();

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
                _logger.Warn("⚠ Stockfish không phản hồi 'uciok' trong 3 giây — có thể chưa sẵn sàng.");
            else
                _logger.Info("✅ Stockfish sẵn sàng nhận lệnh UCI.");
        }

        //========================= QUEUE SYSTEM =========================

        private Process? _stockfish;
        private readonly Channel<StockfishJob> _jobQueue = Channel.CreateUnbounded<StockfishJob>();
        private readonly CancellationTokenSource _cts = new();


        // --------------------------
        // Queue interface
        // --------------------------
        public async Task<string> EnqueueCommandAsync(StockfishJob job)
        {
            _logger.Info("📤 EnqueueCommandAsync bắt đầu");

            var tcs = new TaskCompletionSource<string>();
            job.OnCompleted = (result) => tcs.TrySetResult(result);
            await _jobQueue.Writer.WriteAsync(job);

            _logger.Info("🧾 Đã ghi job vào queue, chờ kết quả...");
            var res = await tcs.Task.ConfigureAwait(false);
            _logger.Info("📩 Nhận được kết quả từ queue");
            return res;
        }


        // --------------------------
        // Queue processor
        // --------------------------
        private async Task ProcessQueueAsync()
        {
            _logger.Info("▶️ ProcessQueueAsync bắt đầu");

            await foreach (var job in _jobQueue.Reader.ReadAllAsync(_cts.Token))
            {
                _logger.Info("📬 Đã nhận được job từ queue");
                try
                {
                    string result = job.Type switch
                    {
                        StockfishJobType.BestMove => await HandleBestMoveJob(job),
                        StockfishJobType.Evaluation => await HandleEvalJob(job),
                        StockfishJobType.CustomCommand => await HandleCustomJob(job),
                        _ => "UnknownJob"
                    };

                    _logger.Info("✅ Đã xử lý xong job");
                    job.OnCompleted?.Invoke(result);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[ProcessQueue] Lỗi: {ex}");
                    job.OnCompleted?.Invoke($"ERROR::{ex.Message}");
                }
            }

            _logger.Warn("⚠️ ProcessQueueAsync kết thúc");
        }


        // --------------------------
        // Handler từng loại Job
        // --------------------------
        private async Task<string> HandleBestMoveJob(StockfishJob job)
        {
            await SendCommandAsync("isready");
            await WaitForResponseAsync(l => l == "readyok", 2000);

            await SendCommandAsync("ucinewgame");
            await SendCommandAsync($"position startpos moves {job.MovesOrFen}");

            var output = await LowLevelSendAndReadAsync(
                $"go depth {job.Depth}",
                job.TimeoutMs,
                line => line.StartsWith("bestmove", StringComparison.OrdinalIgnoreCase)
            );

            return ParseBestMove(output) ?? "(none)";
        }

        private async Task<string> HandleEvalJob(StockfishJob job)
        {
            await SendCommandAsync("isready");
            await WaitForResponseAsync(l => l == "readyok", 2000);

            await SendCommandAsync($"position startpos moves {job.MovesOrFen}");

            var output = await LowLevelSendAndReadAsync(
                $"go depth {job.Depth}",
                job.TimeoutMs,
                line => line.StartsWith("bestmove", StringComparison.OrdinalIgnoreCase)
            );

            double? eval = ParseEval(output);
            return eval?.ToString("F2") ?? "NaN";
        }

        private async Task<string> HandleCustomJob(StockfishJob job)
        {
            return await LowLevelSendAndReadAsync(
                job.MovesOrFen,
                job.TimeoutMs,
                line => line == "readyok" || line.StartsWith("bestmove")
            );
        }

        // --------------------------
        // Core đọc/ghi
        // --------------------------
        private async Task SendCommandAsync(string command)
        {
            if (_inputWriter == null) return;
            await _inputWriter.WriteLineAsync(command);
            await _inputWriter.FlushAsync();
        }

        private async Task<string> LowLevelSendAndReadAsync(
            string command,
            int timeoutMs,
            Func<string, bool> breakCondition)
        {
            if (_stockfish == null || _inputWriter == null || _outputReader == null)
                throw new InvalidOperationException("Stockfish chưa khởi tạo.");

            await _inputWriter.WriteLineAsync(command);
            await _inputWriter.FlushAsync();

            var sb = new StringBuilder();
            using var cts = new CancellationTokenSource(timeoutMs);

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    string? line = await _outputReader.ReadLineAsync().ConfigureAwait(false);
                    if (line == null) break;
                    line = line.Trim();
                    if (line.Length == 0) continue;

                    sb.AppendLine(line);
                    // debug log:
                    // Console.WriteLine($"[Stockfish] {line}");

                    if (breakCondition(line))
                        break;
                }
            }
            catch (TaskCanceledException)
            {
                sb.AppendLine("TIMEOUT");
            }

            return sb.ToString();
        }

        private async Task WaitForResponseAsync(Func<string, bool> breakCondition, int timeoutMs)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            while (!cts.Token.IsCancellationRequested)
            {
                string? line = await _outputReader!.ReadLineAsync().ConfigureAwait(false);
                if (line == null) break;
                line = line.Trim();
                if (breakCondition(line)) break;
            }
        }

        private async Task DrainOutputAsync()
        {
            if (_outputReader == null) return;
            while (_outputReader.Peek() >= 0)
            {
                string? line = await _outputReader.ReadLineAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line)) break;
                // Console.WriteLine($"[Drain] {line}");
            }
        }

        // --------------------------
        // Parser helpers
        // --------------------------
        private static string? ParseBestMove(string output)
        {
            foreach (var line in output.Split('\n'))
            {
                if (line.StartsWith("bestmove", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    return parts.Length > 1 ? parts[1] : null;
                }
            }
            return null;
        }

        private static double? ParseEval(string output)
        {
            var lines = output.Split('\n');
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i];
                if (line.Contains("score cp"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int idx = Array.IndexOf(parts, "cp");
                    if (idx >= 0 && idx + 1 < parts.Length &&
                        double.TryParse(parts[idx + 1], out double cp))
                        return cp / 100.0;
                }
                else if (line.Contains("score mate"))
                {
                    return line.Contains("score mate -") ? -9999 : 9999;
                }
            }
            return null;
        }



        //========================= DISPOSE =========================

        public void Dispose()
        {
            try
            {
                _queueCts.Cancel();
                _engineQueue.Writer.TryComplete();
                try { _queueWorkerTask?.Wait(300); } catch { }

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
