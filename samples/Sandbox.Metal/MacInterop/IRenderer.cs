using System.Runtime.Versioning;
using SharpMetal.Metal;

namespace Sandbox.MacInterop
{
    [SupportedOSPlatform("macos")]
    public interface IRenderer
    {
        static abstract IRenderer Init(MTLDevice device);
        void Draw(MTKView view);
    }
}
