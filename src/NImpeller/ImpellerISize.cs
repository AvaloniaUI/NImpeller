namespace NImpeller;

/// <summary>
/// Represents a size with width and height.
/// </summary>
public partial record struct ImpellerISize
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImpellerISize"/> struct.
    /// </summary>
    public ImpellerISize()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ImpellerISize"/> struct.
    /// </summary>
    /// <param name="width">The extent of the element along the x axis (usually horizontal).</param>
    /// <param name="height">The extent of the element along the y axis (usually vertical).</param>
    public ImpellerISize(long width, long height)
    {
        Width = width;
        Height = height;
    }
}