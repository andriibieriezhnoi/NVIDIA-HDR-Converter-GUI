namespace NHC.Core.Imaging;

/// <summary>
/// HDR display-referred metadata. Values are in cd/m² (nits) where applicable.
/// The decoder populates what it can; unknown fields stay at their defaults.
/// </summary>
public readonly record struct HdrMetadata(
    float MaxContentLightLevel,
    float MaxFrameAverageLightLevel,
    float DisplayPeakLuminance,
    float DisplayMinLuminance,
    ColorSpaceTag ColorSpace)
{
    public static HdrMetadata Default => new(
        MaxContentLightLevel: 1000f,
        MaxFrameAverageLightLevel: 400f,
        DisplayPeakLuminance: 1000f,
        DisplayMinLuminance: 0.005f,
        ColorSpace: ColorSpaceTag.ScRgb);
}

public enum ColorSpaceTag
{
    Unknown = 0,
    ScRgb,
    Rec2020Linear,
    Rec2020Pq,
}
