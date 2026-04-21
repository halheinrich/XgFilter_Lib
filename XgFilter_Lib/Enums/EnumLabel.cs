using System.ComponentModel;
using System.Reflection;

namespace XgFilter_Lib.Enums;

/// <summary>
/// Reads UI-facing labels from <see cref="DescriptionAttribute"/> annotations
/// on enum members. Consumers call <c>PlayType.Make20Pt.ToLabel()</c> instead
/// of maintaining their own enum-to-string maps, keeping display text owned
/// by the library that defines the enum.
/// </summary>
public static class EnumLabel
{
    /// <summary>
    /// Returns the <see cref="DescriptionAttribute"/> text for the given enum
    /// value. Throws <see cref="ArgumentException"/> if the value is not a
    /// declared member, or if the declared member has no
    /// <c>[Description]</c>. Missing annotations are surfaced as failures
    /// rather than silently falling back to <c>ToString()</c>, to keep the
    /// "every enum value has a label" contract exhaustive.
    /// </summary>
    public static string ToLabel<TEnum>(this TEnum value) where TEnum : struct, Enum
    {
        var name = value.ToString();
        var field = typeof(TEnum).GetField(name);
        if (field is null)
            throw new ArgumentException(
                $"Unknown {typeof(TEnum).Name} value: {value}", nameof(value));

        var attr = field.GetCustomAttribute<DescriptionAttribute>();
        if (attr is null)
            throw new ArgumentException(
                $"{typeof(TEnum).Name}.{name} has no [Description] attribute",
                nameof(value));

        return attr.Description;
    }
}
