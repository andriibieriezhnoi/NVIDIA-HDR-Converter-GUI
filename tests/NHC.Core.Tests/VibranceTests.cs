using FluentAssertions;
using NHC.Core.Analysis;
using NHC.Core.Imaging;
using Xunit;

namespace NHC.Core.Tests;

public class VibranceTests
{
    [Fact]
    public void StrengthZero_IsIdentity()
    {
        var image = ColorPatch(16, 16, 0.3f, 0.5f, 0.7f);
        var before = (float[])image.Pixels.Clone();

        var result = Vibrance.Apply(image, 0f);

        result.Pixels.Should().BeEquivalentTo(before);
    }

    [Fact]
    public void GreyStaysGrey()
    {
        var image = ColorPatch(8, 8, 0.5f, 0.5f, 0.5f);

        var result = Vibrance.Apply(image, 1f);

        foreach (var v in result.Pixels)
        {
            v.Should().BeApproximately(0.5f, 1e-5f);
        }
    }

    [Fact]
    public void FullySaturatedPixel_Unchanged()
    {
        // Pure red: saturation=1, so boost factor = 1 (no change).
        var image = ColorPatch(4, 4, 1f, 0f, 0f);
        var before = (float[])image.Pixels.Clone();

        var result = Vibrance.Apply(image, 1f);

        result.Pixels.Should().BeEquivalentTo(before);
    }

    [Fact]
    public void DesaturatedPixel_GetsMoreSaturated()
    {
        // Near-grey with a slight red tint. Vibrance should expand that tint.
        var image = ColorPatch(8, 8, 0.55f, 0.5f, 0.5f);

        var result = Vibrance.Apply(image, 1f);

        // Red channel pushed higher, green/blue pulled lower (around the mean).
        result.Pixels[0].Should().BeGreaterThan(0.55f);
        result.Pixels[1].Should().BeLessThan(0.5f);
        result.Pixels[2].Should().BeLessThan(0.5f);
    }

    [Fact]
    public void PreservesShape()
    {
        var image = ColorPatch(23, 41, 0.4f, 0.5f, 0.6f);

        var result = Vibrance.Apply(image, 0.5f);

        result.Width.Should().Be(23);
        result.Height.Should().Be(41);
        result.Pixels.Length.Should().Be(image.Pixels.Length);
    }

    private static HdrImage ColorPatch(int w, int h, float r, float g, float b)
    {
        var pixels = new float[w * h * 3];
        for (var i = 0; i < w * h; i++)
        {
            pixels[i * 3] = r;
            pixels[i * 3 + 1] = g;
            pixels[i * 3 + 2] = b;
        }
        return new HdrImage(w, h, pixels, HdrMetadata.Default);
    }
}
