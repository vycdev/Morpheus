
using color_names_csharp;
using Discord;

namespace Morpheus.Utilities;
public static class Colors
{
    public static ColorNames ColorNames =  new ColorNamesBuilder().LoadDefault().BuildColorNames;

    public static Color Blue = new(50, 153, 254);
    public static Color BlueShadow = new(19, 61, 101);
    public static Color White = new(255, 255, 255);
    public static Color WhiteShadow = new(185, 229, 254);
}
