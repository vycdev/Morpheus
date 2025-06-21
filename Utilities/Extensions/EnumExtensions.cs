using Discord;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Morpheus.Utilities.Extensions;

public static class EnumExtensions
{
    public static string? GetDisplayName(this Enum value)
    {
        return value.GetAttribute<DisplayAttribute>()?.Name;
    }

    public static string? GetDisplayDescription(this Enum value)
    {
        return value.GetAttribute<DisplayAttribute>()?.Description;
    }

    public static Color? GetDiscordColor(this Enum value)
    {
        var hexString = value.GetDisplayDescription();
        if (hexString != null && uint.TryParse(hexString.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out uint rawColor))
        {
            return new Color(rawColor);
        }

        return null;
    }

    private static T? GetAttribute<T>(this Enum value) where T : Attribute
    {
        var type = value.GetType();
        var memberInfo = type.GetMember(value.ToString());
        if (memberInfo.Length > 0)
        {
            return memberInfo[0].GetCustomAttribute<T>();
        }
        return null;
    }
}
