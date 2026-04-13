namespace NHC.Core.ToneMapping;

/// <summary>
/// Creates tone mappers by kind. Used by the pipeline and by consumers that
/// want to pin a specific operator. For <see cref="ToneMapperKind.Auto"/> the
/// pipeline invokes <see cref="AutoToneMapSelector"/> first and then
/// re-enters this factory with the chosen kind.
/// </summary>
public static class ToneMapperFactory
{
    public static IToneMapper Create(ToneMapperKind kind) => kind switch
    {
        ToneMapperKind.Aces => new AcesToneMapper(),
        ToneMapperKind.Hable => new HableToneMapper(),
        ToneMapperKind.ReinhardExtended => new ReinhardExtendedToneMapper(),
        ToneMapperKind.Auto => throw new ArgumentException(
            "Auto must be resolved by AutoToneMapSelector before creating a mapper.", nameof(kind)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown tone mapper kind."),
    };

    public static IReadOnlyList<ToneMapperKind> AvailableKinds { get; } = new[]
    {
        ToneMapperKind.Auto,
        ToneMapperKind.Aces,
        ToneMapperKind.Hable,
        ToneMapperKind.ReinhardExtended,
    };
}
