using FluentAssertions;
using NHC.Core.Analysis;
using NHC.Core.Imaging;
using Xunit;

namespace NHC.Core.Tests;

public class HistogramAndDitherTests
{
    [Fact]
    public void Histogram_ThreeChannelsSumToPixelCount()
    {
        var pixels = new byte[] { 0, 128, 255, 10, 20, 30, 250, 128, 1 };
        var sdr = new SdrImage(3, 1, pixels);

        var hist = Histogram.Compute(sdr);

        hist.Red.Sum().Should().Be(3);
        hist.Green.Sum().Should().Be(3);
        hist.Blue.Sum().Should().Be(3);
    }

    [Fact]
    public void BayerDither_ProducesSdrImageOfExpectedShape()
    {
        var linear = new HdrImage(16, 16, new float[16 * 16 * 3], HdrMetadata.Default);
        var sdr = BayerDither.EncodeToSrgb(linear);

        sdr.Width.Should().Be(16);
        sdr.Height.Should().Be(16);
        sdr.Pixels.Length.Should().Be(16 * 16 * 3);
    }

    [Fact]
    public void BayerDither_FullBrightMapsTo255()
    {
        var buf = new float[3 * 4];
        Array.Fill(buf, 1.0f);
        var linear = new HdrImage(2, 2, buf, HdrMetadata.Default);

        var sdr = BayerDither.EncodeToSrgb(linear);

        foreach (var b in sdr.Pixels)
        {
            b.Should().Be(255);
        }
    }

    [Fact]
    public void BayerDither_ZeroMapsToZero()
    {
        var linear = new HdrImage(2, 2, new float[12], HdrMetadata.Default);
        var sdr = BayerDither.EncodeToSrgb(linear);
        foreach (var b in sdr.Pixels)
        {
            b.Should().Be(0);
        }
    }
}
