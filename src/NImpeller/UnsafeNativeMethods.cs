namespace NImpeller;

static partial class UnsafeNativeMethods
{
    static UnsafeNativeMethods()
    {
        if (ImpellerGetVersion() != ImpellerVersion)
            throw new InvalidOperationException("Version mismatch between NImpeller and Impeller SDK.");
    }
}