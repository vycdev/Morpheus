using ColorNamesSharp;
using Discord;

namespace Morpheus.Utilities;
public static class Colors
{
    public static readonly ColorNames ColorNames = new ColorNamesBuilder().LoadDefault().Build();

    public static readonly Color Blue = new(50, 153, 254);
    public static readonly Color BlueShadow = new(19, 61, 101);
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color WhiteShadow = new(185, 229, 254);
}
