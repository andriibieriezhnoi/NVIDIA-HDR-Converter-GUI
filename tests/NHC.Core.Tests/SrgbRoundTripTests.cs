using FluentAssertions;
using NHC.Core.Imaging;
using Xunit;

namespace NHC.Core.Tests;

public class SrgbRoundTripTests
{
    [Theory]
    [InlineData(0f)]
    [InlineData(0.001f)]
    [InlineData(0.05f)]
    [InlineData(0.18f)]
    [InlineData(0.5f)]
    [InlineData(0.99f)]
    [InlineData(1f)]
    public void RoundTrip_WithinFloatEpsilon(float linear)
    {
        var encoded = Srgb.LinearToEncoded(linear);
        var back = Srgb.EncodedToLinear(encoded);
        back.Should().BeApproximately(linear, 1e-4f);
    }
}
