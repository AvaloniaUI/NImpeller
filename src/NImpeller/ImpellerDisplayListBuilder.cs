namespace NImpeller;

public partial class ImpellerDisplayListBuilder
{
    // A shared, permanently-invalid handle (native pointer == IntPtr.Zero). The SafeHandle marshaller
    // passes its zero pointer straight through, which the Impeller C API interprets as "no backdrop".
    // Because it is invalid, SafeHandle never invokes ImpellerImageFilterRelease on it.
    private static readonly ImpellerImageFilterHandle NullBackdrop = new();

    /// <summary>
    /// Begins a layer with no backdrop image filter — the common case for opacity groups and clip
    /// layers. The generated three-argument <c>SaveLayer</c> dereferences its backdrop and therefore
    /// cannot accept a null one; this overload supplies the native null the C API expects.
    /// </summary>
    public void SaveLayer(ImpellerRect bounds, ImpellerPaint paint)
    {
        unsafe
        {
            UnsafeNativeMethods.ImpellerDisplayListBuilderSaveLayer(_handle, &bounds, paint.Handle, NullBackdrop);
        }
    }
}
