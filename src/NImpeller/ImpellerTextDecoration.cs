namespace NImpeller;

/// <summary>
/// Represents text decoration configuration including type, style, and color.
/// </summary>
/// <remarks>
/// Text decorations can include underlines, overlines, and line-through (strikethrough).
/// Multiple decoration types can be combined using bitwise OR on the <see cref="ImpellerTextDecoration.Types"/> property.
/// </remarks>
public partial struct ImpellerTextDecoration
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
}