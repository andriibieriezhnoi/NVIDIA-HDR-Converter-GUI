using FluentAssertions;
using NHC.Core.Imaging;
using NHC.Core.ToneMapping;
using Xunit;

namespace NHC.Core.Tests;

public class ToneMapperTests
{
    [Theory]
    [InlineData(ToneMapperKind.Aces)]
    [InlineData(ToneMapperKind.Hable)]
    [InlineData(ToneMapperKind.ReinhardExtended)]
    public void MapsAllPixelsIntoUnitRange(ToneMapperKind kind)
    {
        var image = SyntheticHdr(64, 64, maxStop: 8f);
        var mapper = ToneMapperFactory.Create(kind);

        var mapped = mapper.Apply(image, ToneMapperSettings.Default);

        foreach (var v in mapped.Pixels)
        {
            v.Should().BeInRange(0f, 1f);
        }
    }

    [Fact]
    public void PreservesShape()
    {
        var image = SyntheticHdr(17, 23, maxStop: 4f);
        var mapper = ToneMapperFactory.Create(ToneMapperKind.Aces);

        var mapped = mapper.Apply(image, ToneMapperSettings.Default);

        mapped.Width.Should().Be(17);
        mapped.Height.Should().Be(23);
        mapped.Pixels.Length.Should().Be(image.Pixels.Length);
    }

    [Fact]
    public void AutoRequiresResolution()
    {
        Action act = () => ToneMapperFactory.Create(ToneMapperKind.Auto);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExposureMultipliesInput()
    {
        var image = SyntheticHdr(8, 8, maxStop: 2f);
        var mapper = ToneMapperFactory.Create(ToneMapperKind.ReinhardExtended);

        var low = mapper.Apply(image, new ToneMapperSettings(0.5f, 1f, 1000f));
        var high = mapper.Apply(image, new ToneMapperSettings(2.0f, 1f, 1000f));

        // Mean brightness must grow with exposure (monotone operator).
        low.Pixels.Average().Should().BeLessThan(high.Pixels.Average());
    }

    private static HdrImage SyntheticHdr(int w, int h, float maxStop)
    {
        var pixels = new float[w * h * 3];
        var maxValue = MathF.Pow(2f, maxStop);
        for (int y = 0, p = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++, p += 3)
            {
                var t = (x + y) / (float)(w + h);
                pixels[p] = t * maxValue;
                pixels[p + 1] = t * maxValue * 0.8f;
                pixels[p + 2] = t * maxValue * 0.6f;
            }
        }
        return new HdrImage(w, h, pixels, HdrMetadata.Default);
    }
}
