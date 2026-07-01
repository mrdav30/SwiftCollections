using FixedMathSharp;
using FixedMathSharp.Bounds;
using Xunit;

namespace SwiftCollections.Query.Tests;

public class FixedBoundVolumeFactoryTests
{
    [Fact]
    public void Create_FromFixedBoundBox_UsesBoxMinAndMax()
    {
        var bounds = FixedBoundBox.FromCenterAndSize(new Vector3d(4, 5, 6), new Vector3d(2, 4, 6));

        FixedBoundVolume volume = FixedBoundVolumeFactory.Create(bounds);

        AssertVolume(bounds.Min, bounds.Max, volume);
    }

    [Fact]
    public void Create_FromFixedBoundSphere_UsesSphereEnclosingMinAndMax()
    {
        var bounds = new FixedBoundSphere(new Vector3d(4, 5, 6), (Fixed64)3);

        FixedBoundVolume volume = FixedBoundVolumeFactory.Create(bounds);

        AssertVolume(bounds.Min, bounds.Max, volume);
    }

    [Fact]
    public void Create_FromFixedBoundFrustum_UsesFrustumEnclosingMinAndMax()
    {
        Fixed4x4 projection = Fixed4x4.CreateOrthographicOffCenter(
            (Fixed64)(-2),
            (Fixed64)6,
            (Fixed64)(-3),
            (Fixed64)5,
            (Fixed64)1,
            (Fixed64)9);
        var bounds = new FixedBoundFrustum(projection);

        FixedBoundVolume volume = FixedBoundVolumeFactory.Create(bounds);

        AssertVolume(bounds.Min, bounds.Max, volume);
    }

    private static void AssertVolume(Vector3d expectedMin, Vector3d expectedMax, FixedBoundVolume volume)
    {
        Assert.Equal(expectedMin, volume.Min);
        Assert.Equal(expectedMax, volume.Max);
    }
}
