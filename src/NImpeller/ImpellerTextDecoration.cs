using System;

namespace NImpeller;

public partial struct ImpellerTextDecoration : IEquatable<ImpellerTextDecoration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImpellerTextDecoration"/> struct.
    /// </summary>
    /// <param name="type">The type of decoration (can be combined flags).</param>
    /// <param name="style">The style of the decoration line.</param>
    /// <param name="color">The color of the decoration.</param>
    /// <param name="thicknessMultiplier">The thickness multiplier of the decoration line.</param>
    public ImpellerTextDecoration(
        ImpellerTextDecorationType types,
        ImpellerTextDecorationStyle style,
        ImpellerColor color,
        float thicknessMultiplier = 1f)
    {
        Types = (int)types;
        Style = style;
        Color = color;
        Thickness_multiplier = thicknessMultiplier;
    }

    /// <summary>
    /// Creates a solid underline decoration with the specified color.
    /// </summary>
    /// <param name="color">The color of the underline.</param>
    /// <param name="thicknessMultiplier">The thickness multiplier of the underline.</param>
    /// <returns>A new <see cref="ImpellerTextDecoration"/> with a solid underline.</returns>
    public static ImpellerTextDecoration Underline(ImpellerColor color, float thicknessMultiplier = 1f) =>
        new(ImpellerTextDecorationType.kImpellerTextDecorationTypeUnderline, ImpellerTextDecorationStyle.kImpellerTextDecorationStyleSolid, color, thicknessMultiplier);

    /// <summary>
    /// Creates a solid overline decoration with the specified color.
    /// </summary>
    /// <param name="color">The color of the overline.</param>
    /// <param name="thicknessMultiplier">The thickness multiplier of the overline.</param>
    /// <returns>A new <see cref="ImpellerTextDecoration"/> with a solid overline.</returns>
    public static ImpellerTextDecoration Overline(ImpellerColor color, float thicknessMultiplier = 1f) =>
        new(ImpellerTextDecorationType.kImpellerTextDecorationTypeOverline, ImpellerTextDecorationStyle.kImpellerTextDecorationStyleSolid, color, thicknessMultiplier);

    /// <summary>
    /// Creates a solid line-through decoration with the specified color.
    /// </summary>
    /// <param name="color">The color of the line-through.</param>
    /// <param name="thicknessMultiplier">The thickness multiplier of the line-through.</param>
    /// <returns>A new <see cref="ImpellerTextDecoration"/> with a solid line-through.</returns>
    public static ImpellerTextDecoration LineThrough(ImpellerColor color, float thicknessMultiplier = 1f) =>
        new(ImpellerTextDecorationType.kImpellerTextDecorationTypeLineThrough, ImpellerTextDecorationStyle.kImpellerTextDecorationStyleSolid, color, thicknessMultiplier);

    public bool Equals(ImpellerTextDecoration other) =>
        Types == other.Types
        && Style == other.Style
        && Color.Equals(other.Color)
        && Thickness_multiplier.Equals(other.Thickness_multiplier);

    public override bool Equals(object? obj) => obj is ImpellerTextDecoration other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Types, Style, Color, Thickness_multiplier);

    public static bool operator ==(ImpellerTextDecoration left, ImpellerTextDecoration right) => left.Equals(right);

    public static bool operator !=(ImpellerTextDecoration left, ImpellerTextDecoration right) => !left.Equals(right);
}