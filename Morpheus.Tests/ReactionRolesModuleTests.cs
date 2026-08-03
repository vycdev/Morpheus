using Discord;
using Morpheus.Modules;

namespace Morpheus.Tests;

public class ReactionRolesModuleTests
{
    [Fact]
    public void FormatButtonLabel_PreservesLabelsWithinDiscordLimit()
    {
        string roleName = new('a', ButtonBuilder.MaxButtonLabelLength);

        string label = ReactionRolesModule.FormatButtonLabel(roleName);

        Assert.Equal(roleName, label);
    }

    [Fact]
    public void FormatButtonLabel_TruncatesLabelsBeyondDiscordLimit()
    {
        string roleName = new('a', ButtonBuilder.MaxButtonLabelLength + 1);

        string label = ReactionRolesModule.FormatButtonLabel(roleName);

        Assert.Equal(new string('a', ButtonBuilder.MaxButtonLabelLength - 1) + "…", label);
        Assert.Equal(ButtonBuilder.MaxButtonLabelLength, label.Length);
    }

    [Fact]
    public void FormatButtonLabel_DoesNotSplitSurrogatePairs()
    {
        string prefix = new('a', ButtonBuilder.MaxButtonLabelLength - 2);
        string roleName = prefix + "😀bc";

        string label = ReactionRolesModule.FormatButtonLabel(roleName);

        Assert.Equal(prefix + "…", label);
    }
}