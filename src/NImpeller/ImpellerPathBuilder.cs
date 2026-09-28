namespace NImpeller;

public unsafe partial class ImpellerPathBuilder
{
    /// <summary>Adds the contours of <paramref name="path"/> without transforming them.</summary>
    public void AddPath(ImpellerPath path)
    {
        UnsafeNativeMethods.ImpellerPathBuilderAddPath(Handle, path.Handle, null);
    }
}
