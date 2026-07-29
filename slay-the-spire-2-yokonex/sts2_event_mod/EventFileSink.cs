using STS2Bridge.Events;
using System.Text;
using System.Text.Json;

namespace STS2Bridge;

internal static class EventFileSink
{
    private static readonly object SyncRoot = new();
    private static readonly string DataRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SlayTheSpire2");
    private static readonly string EventPath = Path.Combine(DataRoot, "yokonex_events.log");
    private static readonly string DebugPath = Path.Combine(DataRoot, "yokonex_events_debug.log");

    public static void WriteEvent(GameEvent gameEvent)
    {
        var message = new
        {
            kind = "event",
            type = gameEvent.Type,
            timestamp = gameEvent.Timestamp,
            data = new
            {
                gameEvent.EventId,
                gameEvent.RunId,
                gameEvent.Floor,
                gameEvent.RoomType,
                gameEvent.Payload
            }
        };
        Append(EventPath, JsonSerializer.Serialize(message));
    }

    public static void WriteDebug(string line)
    {
        Append(DebugPath, line);
    }

    private static void Append(string path, string line)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(DataRoot);
                // 每行一条完整记录，桥接器中断后可以安全恢复。
                File.AppendAllText(path, line + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch
        {
        }
    }
}
