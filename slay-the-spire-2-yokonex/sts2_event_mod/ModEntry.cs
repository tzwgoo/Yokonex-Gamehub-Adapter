using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using STS2Bridge.Events;
using STS2Bridge.Logging;
using STS2Bridge.State;

namespace STS2Bridge;

[ModInitializer("Initialize")]
public static class ModEntry
{
    private static IDisposable? _eventSubscription;

    public static GameEventBus EventBus { get; private set; } = new();

    public static GameStateStore StateStore { get; private set; } = new();

    public static void Initialize()
    {
        try
        {
            // 独立 Mod 只初始化事件链路，不启动 API、控制动作、IM 或蓝牙功能。
            EventBus = new GameEventBus();
            StateStore = new GameStateStore();
            _eventSubscription = EventBus.Subscribe(EventFileSink.WriteEvent);
            ModLog.SetSink(EventFileSink.WriteDebug);

            var harmony = new Harmony("net.yokonex.sts2.events");
            harmony.PatchAll(typeof(ModEntry).Assembly);
            ModLog.Info("Yokonex STS2 event collector initialized.");
        }
        catch (Exception exception)
        {
            EventFileSink.WriteDebug($"STS2 event collector initialization failed: {exception}");
        }
    }
}
