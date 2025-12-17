using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Telegram.Bot.Types;

namespace dn42Bot;

internal static class BotLogger
{
    readonly static Task _backgroundRefreshTask = Task.CompletedTask;
    readonly static ConcurrentQueue<Log> _queue = new();
    static BotLogger()
    {
        _backgroundRefreshTask = Task.Factory.StartNew(() =>
        {
            var sb = new StringBuilder();
            while (true)
            {
                while(_queue.TryDequeue(out var log))
                {
                    try
                    {
                        var frontgroundColor = ConsoleColor.White;
                        sb.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss}]", log.Timestamp);
                        switch (log.Level)
                        {
                            case LogLevel.Debug:
                                sb.Append("[Debug]");
                                frontgroundColor = ConsoleColor.White;
                                break;
                            case LogLevel.Info:
                                sb.Append("[Info]");
                                frontgroundColor = ConsoleColor.White;
                                break;
                            case LogLevel.Warning:
                                sb.Append("[Warning]");
                                frontgroundColor = ConsoleColor.Yellow;
                                break;
                            case LogLevel.Error:
                                sb.Append("[Error]");
                                frontgroundColor = ConsoleColor.Red;
                                break;
                            default:
                                continue;
                        }
                        sb.Append(log.Message);
                        if(log.StackTrace is not null)
                        {
                            sb.AppendLine();
                            sb.Append(log.StackTrace);
                        }
                        Console.ForegroundColor = frontgroundColor;
                        Console.WriteLine(sb.ToString());
                        Console.ResetColor();
                    }
                    finally
                    {
                        sb.Clear();
                    }
                }
                Thread.Sleep(100);
            }
        }, TaskCreationOptions.LongRunning);
    }
    public static void LogDebug(string message)
    {
        var stackTrace = default(StackTrace);
#if DEBUG
        //stackTrace = new StackTrace(1, true);
#endif
        _queue.Enqueue(new Log
        {
            Level = LogLevel.Debug,
            Message = message,
            StackTrace = stackTrace,
            Timestamp = DateTime.Now
        });
    }
    public static void LogInfo(string message)
    {
        var stackTrace = default(StackTrace);
#if DEBUG
        //stackTrace = new StackTrace(1, true);
#endif
        _queue.Enqueue(new Log
        {
            Level = LogLevel.Info,
            Message = message,
            StackTrace = stackTrace,
            Timestamp = DateTime.Now
        });
    }
    public static void LogWarning(string message)
    {
        var stackTrace = default(StackTrace);
#if DEBUG
        //stackTrace = new StackTrace(1, true);
#endif
        _queue.Enqueue(new Log
        {
            Level = LogLevel.Warning,
            Message = message,
            StackTrace = stackTrace,
            Timestamp = DateTime.Now
        });
    }
    public static void LogError(string message)
    {
        var stackTrace = default(StackTrace);
#if DEBUG
        stackTrace = new StackTrace(1, true);
#endif
        _queue.Enqueue(new Log
        {
            Level = LogLevel.Error,
            Message = message,
            StackTrace = stackTrace,
            Timestamp = DateTime.Now
        });
    }
    public static void LogError(Exception e)
    {
        _queue.Enqueue(new Log
        {
            Level = LogLevel.Error,
            Message = e.ToString(),
            StackTrace = null,
            Timestamp = DateTime.Now
        });
    }
    struct Log
    {
        public LogLevel Level { get; init; }
        public string Message { get; init; }
        public StackTrace? StackTrace { get; init; }
        public DateTime Timestamp { get; init; }
    }
    enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
