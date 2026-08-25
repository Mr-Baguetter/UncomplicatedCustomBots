using Discord;
using MEC;
using System;
using System.Collections.Generic;

namespace UncomplicatedCustomBots.API.Managers
{
    internal class LogManager
    {
        public static readonly List<LogEntry> History = [];
        private const int MaxHistory = 2048;
        private static readonly object _historyLock = new();

        public static bool MessageSent { get; internal set; } = false;

        private static readonly Queue<string> _pendingDebug = new();
        private static CoroutineHandle _flushHandle;
        private static readonly object _debugLock = new();

        private static void AddHistory(LogEntry entry)
        {
            lock (_historyLock)
            {
                History.Add(entry);
                if (History.Count > MaxHistory)
                    History.RemoveRange(0, History.Count - MaxHistory);
            }
        }

        public static void StartFlushCoroutine()
            => _flushHandle = Timing.RunCoroutine(FlushLoop());

        public static void StopFlushCoroutine()
        {
            if (_flushHandle.IsRunning)
                Timing.KillCoroutines(_flushHandle);

            FlushDebugLogs();
        }

        private static IEnumerator<float> FlushLoop()
        {
            for (;;)
            {
                yield return Timing.WaitForSeconds(Plugin.Instance.Config.DebugBatchInterval);
                FlushDebugLogs();
            }
        }

        public static void FlushDebugLogs()
        {
            string combined;
            lock (_debugLock)
            {
                if (_pendingDebug.Count == 0)
                    return;

                combined = string.Join("\n", _pendingDebug);
                _pendingDebug.Clear();
            }
            Logger.Raw(combined, ConsoleColor.Green);
        }

        public static void Debug(string message)
        {
            string formatted = $"[DEBUG] [{Plugin.Instance.GetType().Assembly.GetName().Name}] {message}";
            AddHistory(new(DateTimeOffset.Now.ToUnixTimeMilliseconds(), LogLevel.Debug.ToString(), message));
            if (Plugin.Instance.Config.Debug)
            {
                lock (_debugLock)
                    _pendingDebug.Enqueue(formatted);
            }
        }

        public static void Info(string message)
        {
            AddHistory(new(DateTimeOffset.Now.ToUnixTimeMilliseconds(), LogLevel.Info.ToString(), message));
            Logger.Info(message);
        }

        public static void Warn(string message, string error = "CS0000")
        {
            AddHistory(new(DateTimeOffset.Now.ToUnixTimeMilliseconds(), LogLevel.Warn.ToString(), message, error));
            Logger.Warn(message);
        }

        public static void Error(string message, string error = "CS0000")
        {
            AddHistory(new(DateTimeOffset.Now.ToUnixTimeMilliseconds(), LogLevel.Error.ToString(), message, error));
            Logger.Error(message);
        }
        
        public static void Raw(string message, ConsoleColor color, string logLevel, string category)
        {
            AddHistory(new(DateTimeOffset.Now.ToUnixTimeMilliseconds(), logLevel, message));
            Logger.Raw($"[{category}] [{Plugin.Instance.GetType().Assembly.GetName().Name}] {message}", color);
        }
        
        public static void Updater(string message)
        {
            AddHistory(new(DateTimeOffset.Now.ToUnixTimeMilliseconds(), "Updater", message));
            Logger.Raw($"[Updater] [{Plugin.Instance.GetType().Assembly.GetName().Name}] {message}", ConsoleColor.Blue);
        }
        
        public static void Silent(string message)
        {
            AddHistory(new(DateTimeOffset.Now.ToUnixTimeMilliseconds(), "SILENT", message));
            if (Plugin.Instance.Config.ShowSilentLogs)
                Logger.Raw($"[Silent] [{Plugin.Instance.GetType().Assembly.GetName().Name}] {message}", ConsoleColor.White);
        }

        public static void System(string message) => AddHistory(new(DateTimeOffset.Now.ToUnixTimeMilliseconds(), "SYSTEM", message));
    }
}
