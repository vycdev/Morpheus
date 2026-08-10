using Morpheus.Modules;

namespace Morpheus.Tests;

public class QuoteApprovalInteractionTests
{
    [Theory]
    [InlineData("quote_approve:42", true, 42)]
    [InlineData("quote_approve:0", false, 0)]
    [InlineData("quote_approve:-42", false, 0)]
    [InlineData("quote_approve:+42", false, 0)]
    [InlineData("quote_approve: 42", false, 0)]
    [InlineData("quote_approve:42:extra", false, 0)]
    [InlineData("quote_reject:42", false, 0)]
    [InlineData(null, false, 0)]
    public void TryParseApprovalId_RequiresUnsignedNonzeroInvariantComponentValue(
        string? customId,
        bool expectedSuccess,
        int expectedApprovalId)
    {
        bool success = QuotesModule.TryParseApprovalId(customId, out int approvalId);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedApprovalId, approvalId);
    }
}
