namespace NImpeller;

public partial struct ImpellerTextDecoration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImpellerTextDecoration"/> struct.
    /// </summary>
    /// <param name="type">The type of decoration (can be combined flags).</param>
    /// <param name="style">The style of the decoration line.</param>
    /// <param name="color">The color of the decoration.</param>
    public ImpellerTextDecoration(
        ImpellerTextDecorationType types,
        ImpellerTextDecorationStyle style,
        ImpellerColor color,
        float thickness_multiplier = 1f)
    {
        Types = (int)types;
        Style = style;
        Color = color;
        this.Thickness_multiplier = thickness_multiplier;
    }

    /// <summary>
    /// Creates a solid underline decoration with the specified color.
    /// </summary>
    /// <param name="color">The color of the underline.</param>
    /// <param name="thickness_multiplier">The thickness multiplier of the underline.</param>
    /// <returns>A new <see cref="ImpellerTextDecoration"/> with a solid underline.</returns>
    public static ImpellerTextDecoration Underline(ImpellerColor color, float thickness_multiplier = 1f) =>
        new(ImpellerTextDecorationType.kImpellerTextDecorationTypeUnderline, ImpellerTextDecorationStyle.kImpellerTextDecorationStyleSolid, color, thickness_multiplier);

    /// <summary>
    /// Creates a solid overline decoration with the specified color.
    /// </summary>
    /// <param name="color">The color of the overline.</param>
    /// <param name="thickness_multiplier">The thickness multiplier of the overline.</param>
    /// <returns>A new <see cref="ImpellerTextDecoration"/> with a solid overline.</returns>
    public static ImpellerTextDecoration Overline(ImpellerColor color, float thickness_multiplier = 1f) =>
        new(ImpellerTextDecorationType.kImpellerTextDecorationTypeOverline, ImpellerTextDecorationStyle.kImpellerTextDecorationStyleSolid, color, thickness_multiplier);

    /// <summary>
    /// Creates a solid line-through decoration with the specified color.
    /// </summary>
    /// <param name="color">The color of the line-through.</param>
    /// <param name="thickness_multiplier">The thickness multiplier of the line-through.</param>
    /// <returns>A new <see cref="ImpellerTextDecoration"/> with a solid line-through.</returns>
    public static ImpellerTextDecoration LineThrough(ImpellerColor color, float thickness_multiplier = 1f) =>
        new(ImpellerTextDecorationType.kImpellerTextDecorationTypeLineThrough, ImpellerTextDecorationStyle.kImpellerTextDecorationStyleSolid, color, thickness_multiplier);
}