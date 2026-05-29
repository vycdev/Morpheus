using Morpheus.Utilities.Text;

namespace Morpheus.Tests;

public class SimHasherTests
{
    [Fact]
    public void ComputeSimHash_ReturnsZeroHashForVeryShortNormalizedText()
    {
        (ulong hash, int normalizedLength) = SimHasher.ComputeSimHash("hi!");

        Assert.Equal(0UL, hash);
        Assert.Equal(2, normalizedLength);
    }

    [Fact]
    public void ComputeSimHash_TreatsCasePunctuationAndDigitChangesAsEquivalent()
    {
        (ulong firstHash, int firstLength) = SimHasher.ComputeSimHash("Hello, 123!!!");
        (ulong secondHash, int secondLength) = SimHasher.ComputeSimHash("hello 456");

        Assert.Equal(firstHash, secondHash);
        Assert.Equal(firstLength, secondLength);
    }

    [Fact]
    public void ComputeSimHash_MakesSimilarTextCloserThanUnrelatedText()
    {
        ulong original = SimHasher.ComputeSimHash("the quick brown fox jumps over the lazy dog today").hash;
        ulong similar = SimHasher.ComputeSimHash("the quick brown fox jumps over the lazy dog tomorrow").hash;
        ulong unrelated = SimHasher.ComputeSimHash("database migrations compile slowly under heavy moonlight").hash;

        int similarDistance = SimHasher.HammingDistance(original, similar);
        int unrelatedDistance = SimHasher.HammingDistance(original, unrelated);

        Assert.True(similarDistance < unrelatedDistance, $"{similarDistance} should be less than {unrelatedDistance}");
    }
}
