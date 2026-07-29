namespace STS2Bridge.Runtime;

internal static class PlayerBlockHookTargetCatalog
{
    public static IReadOnlyList<string> BlockLossMethodNames { get; } =
    [
        "LoseBlockInternal",
        "DamageBlockInternal"
    ];
}
