using NImpeller;

namespace NImpeller.Tests.Scenes;

public sealed class SceneParameters
{
    public int Width { get; set; }
    public int Height { get; set; }
}

public interface IScene
{
    string TestName { get; }

    void Render(ImpellerContext context, ImpellerDisplayListBuilder builder, SceneParameters parameters);
}
