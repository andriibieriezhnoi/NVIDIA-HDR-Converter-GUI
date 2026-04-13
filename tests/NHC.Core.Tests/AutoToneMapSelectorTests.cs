using FluentAssertions;
using NHC.Core.Analysis;
using NHC.Core.ToneMapping;
using Xunit;

namespace NHC.Core.Tests;

public class AutoToneMapSelectorTests
{
    [Fact]
    public void HighDynamicRangeWithDeepShadows_PicksHable()
    {
        var stats = new ImageStatistics(
            MinLuminance: 0.0005f,
            MaxLuminance: 12f,
            MeanLuminance: 0.4f,
            DynamicRangeStops: 14.5f,
            ShadowFraction: 0.4f,
            MidtoneFraction: 0.5f,
            HighlightFraction: 0.1f,
            Saturation: 0.3f);

        AutoToneMapSelector.Select(stats).Should().Be(ToneMapperKind.Hable);
    }

    [Fact]
    public void HeavyHighlights_PicksAces()
    {
        var stats = new ImageStatistics(
            MinLuminance: 0.05f,
            MaxLuminance: 10f,
            MeanLuminance: 1.2f,
            DynamicRangeStops: 7f,
            ShadowFraction: 0.1f,
            MidtoneFraction: 0.6f,
            HighlightFraction: 0.3f,
            Saturation: 0.2f);

        AutoToneMapSelector.Select(stats).Should().Be(ToneMapperKind.Aces);
    }

    [Fact]
    public void ModerateScene_PicksReinhardExtended()
    {
        var stats = new ImageStatistics(
            MinLuminance: 0.1f,
            MaxLuminance: 2f,
            MeanLuminance: 0.5f,
            DynamicRangeStops: 4.3f,
            ShadowFraction: 0.2f,
            MidtoneFraction: 0.7f,
            HighlightFraction: 0.1f,
            Saturation: 0.25f);

        AutoToneMapSelector.Select(stats).Should().Be(ToneMapperKind.ReinhardExtended);
    }
}
