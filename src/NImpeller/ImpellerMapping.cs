namespace NImpeller;

internal partial struct ImpellerMapping
{
    public unsafe struct Marshalled : IDisposable
    {
        public void Dispose()
        {
            
        }

        public ImpellerMapping* Value { get; set; }
    }
    
    public static Marshalled Marshal(IImpellerUnmanagedMemory contents)
    {
        throw new NotImplementedException();
    }
}

public interface IImpellerUnmanagedMemory
{
    
}