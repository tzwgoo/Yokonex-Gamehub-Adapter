namespace STS2Bridge.Runtime;

internal static class PlayerHookArgumentLogic
{
    public static object? FindDamageTargetPlayerArgument(object?[]? args)
    {
        if (args is null || args.Length == 0)
        {
            return null;
        }

        if (args.Length > 3)
        {
            var target = args[3];
            return PlayerEventBridgeLogic.IsPlayerCreature(target) ? target : null;
        }

        foreach (var arg in args)
        {
            if (PlayerEventBridgeLogic.IsPlayerCreature(arg))
            {
                return arg;
            }
        }

        return null;
    }
}
