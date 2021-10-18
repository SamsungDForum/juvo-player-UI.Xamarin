using System;
using System.Runtime.CompilerServices;

namespace UI.Common.Logger
{
    public static class Log
    {
        private const string Tag = "JuvoUI";
        
        public static void Verbose(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Verbose(Tag, message, file, func, line);

        public static void Debug(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Debug(Tag, message, file, func, line);

        public static void Info(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Info(Tag, message, file, func, line);

        public static void Warn(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Warn(Tag, message, file, func, line);

        public static void Error(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Error(Tag, message, file, func, line);

        public static void Error(
            Exception error,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Error(Tag, error?.ToString(), file, func, line);

        public static void Fatal(
            string message,
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0)
            => Tizen.Log.Error(Tag, message, file, func, line);

        public static LogScope Scope(
            string message = "",
            [CallerFilePath] string file = "",
            [CallerMemberName] string func = "",
            [CallerLineNumber] int line = 0) => new LogScope(message, file, func, line);
    }

    public readonly struct LogScope : IDisposable
    {
        private readonly string _msg;
        private readonly string _file;
        private readonly string _method;
        private readonly int _line;

        public LogScope(string msg, string file, string method, int line)
        {
            _msg = msg;
            _file = file;
            _method = method;
            _line = line;

            Log.Debug($"Enter({msg}) -> ", file, method, line);
        }
       
        public void Dispose() => Log.Debug($"Exit({_msg}) <- ", _file, _method, _line);
    }
}
