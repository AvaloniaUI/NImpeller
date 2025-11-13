namespace NImpeller;

/// <summary>
/// Represents a rectangle with position (X, Y) and dimensions (Width, Height).
/// </summary>
public partial record struct ImpellerRect
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImpellerRect"/> struct.
    /// </summary>
    public ImpellerRect()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImpellerRect"/> struct.
    /// </summary>
    /// <param name="x">The X coordinate of the top-left corner.</param>
    /// <param name="y">The Y coordinate of the top-left corner.</param>
    /// <param name="width">The width of the rectangle.</param>
    /// <param name="height">The height of the rectangle.</param>
    public ImpellerRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}