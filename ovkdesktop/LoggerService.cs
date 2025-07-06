using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace ovkdesktop
{
    public sealed class LoggerService : IDisposable
    {
        public static LoggerService Instance { get; } = new LoggerService();

        private readonly Queue<string> _recentLogs = new Queue<string>();
        private const int MaxRecentLogsCount = 100;

        private StreamWriter? _logWriter;
        private readonly object _lock = new object();

        private LoggerService() { }

        public void Initialize()
        {
            try
            {
                string baseDirectory = AppContext.BaseDirectory;

                string logsFolderPath = Path.Combine(baseDirectory, "logs");
                Directory.CreateDirectory(logsFolderPath);

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileName = $"ovklog-{timestamp}.log";
                string logFilePath = Path.Combine(logsFolderPath, fileName);

                _logWriter = new StreamWriter(logFilePath, append: true, Encoding.UTF8) { AutoFlush = true };

                var fileTraceListener = new FileTraceListener(_logWriter, _lock, this);
                Trace.Listeners.Add(fileTraceListener);

                Log("LoggerService Initialized. Log file: " + logFilePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FATAL: Failed to initialize LoggerService. {ex}");
            }
        }

        public void Log(string message)
        {
            WriteMessage($"INFO | {message}");
        }

        public void LogWarning(string message)
        {
            WriteMessage($"WARN | {message}");
        }

        public void LogError(string message, Exception? ex = null)
        {
            if (ex != null)
            {
                WriteMessage($"ERROR| {message}\n{ex}");
            }
            else
            {
                WriteMessage($"ERROR| {message}");
            }
        }

        public List<string> GetRecentLogs()
        {
            lock (_lock)
            {
                return _recentLogs.ToList();
            }
        }

        private void WriteMessage(string fullMessage)
        {
            if (_logWriter == null) return;

            string formattedMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {fullMessage}";

            lock (_lock)
            {
                _logWriter.WriteLine(formattedMessage);

                _recentLogs.Enqueue(formattedMessage);
                while (_recentLogs.Count > MaxRecentLogsCount)
                {
                    _recentLogs.Dequeue();
                }
            }
        }
        public void Dispose()
        {
            Log("[LoggerService] Service Disposing...");

            // Корректное удаление listener'а
            var listenerToRemove = Trace.Listeners.OfType<FileTraceListener>().FirstOrDefault();
            if (listenerToRemove != null)
            {
                Trace.Listeners.Remove(listenerToRemove);
            }

            lock (_lock)
            {
                _logWriter?.Flush();
                _logWriter?.Close();
                _logWriter?.Dispose();
                _logWriter = null;
            }
        }

        private sealed class FileTraceListener : TraceListener
        {
            private readonly StreamWriter _writer;
            private readonly object _writerLock;
            private readonly LoggerService _logger;

            public FileTraceListener(StreamWriter writer, object writerLock, LoggerService logger)
            {
                _writer = writer;
                _writerLock = writerLock;
                _logger = logger;
            }

            public override void Write(string? message)
            {
                lock (_writerLock)
                {
                    _writer.Write(message);
                }
            }

            public override void WriteLine(string? message)
            {
                if (!string.IsNullOrEmpty(message))
                {
                    _logger.WriteMessage($"DEBUG| {message}");
                }
            }
        }


    }
}
