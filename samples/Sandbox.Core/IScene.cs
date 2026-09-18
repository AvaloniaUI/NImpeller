using NImpeller;

namespace Sandbox;

public class SceneParameters
{
    public int Complexity { get; set; } = 12;
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>
    /// Typography context to lay out text with. When null, scenes create a throwaway context that
    /// only sees the platform's system fonts. Hosts without system fonts (e.g. the browser) supply a
    /// context with fonts registered up front.
    /// </summary>
    public ImpellerTypographyContext? TypographyContext { get; set; }
}

public interface IScene
{
    /// <summary>
    /// Gets the name of the scene.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of what the scene demonstrates.
    /// </summary>
    /// <summary>
    /// Gets the command-line friendly name for the scene (no spaces, lowercase).
    /// Used for --scene argument.
    /// </summary>
    string CommandLineName { get; }

    string Description { get; }

    void Render(ImpellerContext context, ImpellerDisplayListBuilder scene, SceneParameters sceneParameters);
}