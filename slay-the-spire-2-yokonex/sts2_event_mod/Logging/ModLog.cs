namespace STS2Bridge.Logging;

public static class ModLog
{
    private static readonly object SyncRoot = new();
    private static Action<string>? _sink;

    public static void SetSink(Action<string>? sink)
    {
        lock (SyncRoot)
        {
            _sink = sink;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warn(string message) => Write("WARN", message);

    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");
    }

    private static void Write(string level, string message)
    {
        var line = $"[{DateTimeOffset.UtcNow:O}] [{level}] {message}";
        lock (SyncRoot)
        {
            _sink?.Invoke(line);
        }
    }
}
