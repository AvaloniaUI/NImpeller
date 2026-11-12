using NImpeller;

namespace Sandbox.Android.Rendering;

public enum GraphicsApi
{
    OpenGLES,
    Vulkan,
}

/// <summary>
/// Owns an Impeller context and presents display lists to an ANativeWindow. All members are called
/// on the render thread.
/// </summary>
internal interface IRenderBackend : IDisposable
{
    ImpellerContext Context { get; }

    /// <summary>Starts presenting to <paramref name="nativeWindow"/>.</summary>
    void Attach(IntPtr nativeWindow);

    /// <summary>Stops presenting and releases everything tied to the attached window.</summary>
    void Detach();

    /// <summary>Draws and presents one frame of <paramref name="width"/> x <paramref name="height"/> pixels.</summary>
    void Present(ImpellerDisplayList displayList, int width, int height);
}
