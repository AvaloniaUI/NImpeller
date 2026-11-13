namespace Sandbox.Scenes;

/// <summary>The scenes every Sandbox host offers.</summary>
public static class SceneCatalog
{
    /// <summary>Creates a fresh instance of every scene. Scenes keep per-run state such as clocks.</summary>
    public static IScene[] CreateAll() =>
    [
        new MMarkScene(),
        new ParagraphScene(),
        new CirclingSquares(),
        new AnimatedGradientsScene(),
        new FragmentProgramScene(),
        new ShadowsScene(),
        new ImageFiltersScene(),
        new TextFeaturesScene(),
    ];
}
