using NImpeller;

namespace Sandbox;

public class SceneParameters
{
    public int Complexity { get; set; } = 12;
    public int Width { get; set; }
    public int Height { get; set; }
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
    string Description { get; }

    void Render(ImpellerContext context, ImpellerDisplayListBuilder scene, SceneParameters sceneParameters);
}