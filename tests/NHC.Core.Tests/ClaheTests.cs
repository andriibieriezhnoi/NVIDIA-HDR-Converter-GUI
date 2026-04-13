using FluentAssertions;
using NHC.Core.Analysis;
using NHC.Core.Imaging;
using Xunit;

namespace NHC.Core.Tests;

public class ClaheTests
{
    [Fact]
    public void StrengthZero_IsIdentity()
    {
        var image = Gradient(64, 48);
        var before = (float[])image.Pixels.Clone();

        var result = Clahe.Apply(image, 0f);

        result.Pixels.Should().BeEquivalentTo(before);
    }

    [Fact]
    public void PreservesShape()
    {
        var image = Gradient(47, 33);

        var result = Clahe.Apply(image, 1f);

        result.Width.Should().Be(47);
        result.Height.Should().Be(33);
        result.Pixels.Length.Should().Be(image.Pixels.Length);
    }

    [Fact]
    public void StaysInUnitRange_ForSdrInput()
    {
        var image = Gradient(64, 64);

        var result = Clahe.Apply(image, 1f);

        foreach (var v in result.Pixels)
        {
            v.Should().BeInRange(0f, 1f + 1e-4f);
        }
    }

    [Fact]
    public void IncreasesContrast_OnLowContrastImage()
    {
        // Narrow gradient from 0.4 to 0.6 — low stddev, CLAHE should stretch it.
        var image = NarrowGradient(128, 96, low: 0.4f, high: 0.6f);
        var beforeStd = Stddev(image.Pixels);

        var result = Clahe.Apply(image, 1f);
        var afterStd = Stddev(result.Pixels);

        afterStd.Should().BeGreaterThan(beforeStd);
    }

    [Fact]
    public void TooSmallImage_ReturnsInput()
    {
        // Smaller than the tile grid — CLAHE no-ops instead of crashing.
        var image = Gradient(8, 8);
        var result = Clahe.Apply(image, 1f);
        result.Should().BeSameAs(image);
    }

    private static HdrImage Gradient(int w, int h)
    {
        var pixels = new float[w * h * 3];
        for (int y = 0, p = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++, p += 3)
            {
                var t = (x + y) / (float)(w + h);
                pixels[p] = t;
                pixels[p + 1] = t;
                pixels[p + 2] = t;
            }
        }
        return new HdrImage(w, h, pixels, HdrMetadata.Default);
    }

    private static HdrImage NarrowGradient(int w, int h, float low, float high)
    {
        var pixels = new float[w * h * 3];
        for (int y = 0, p = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++, p += 3)
            {
                var t = (x + y) / (float)(w + h);
                var v = low + t * (high - low);
                pixels[p] = v;
                pixels[p + 1] = v;
                pixels[p + 2] = v;
            }
        }
        return new HdrImage(w, h, pixels, HdrMetadata.Default);
    }

    private static double Stddev(float[] values)
    {
        double mean = 0;
        for (var i = 0; i < values.Length; i++) mean += values[i];
        mean /= values.Length;
        double sq = 0;
        for (var i = 0; i < values.Length; i++)
        {
            var d = values[i] - mean;
            sq += d * d;
        }
        return Math.Sqrt(sq / values.Length);
    }
}
