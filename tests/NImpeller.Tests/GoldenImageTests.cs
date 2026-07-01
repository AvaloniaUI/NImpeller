using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NImpeller.Tests.Headless;
using NImpeller.Tests.Scenes;
using Xunit;

namespace NImpeller.Tests;

/// <summary>
/// Golden-image regression tests: render a deterministic scene offscreen and compare the pixels
/// against a committed baseline.
///
/// Generate or refresh baselines by setting <c>UPDATE_GOLDENS=1</c>:
///   <c>UPDATE_GOLDENS=1 dotnet test</c>
/// A missing baseline is written automatically and the test is skipped (so first run is green and
/// the new baseline can be reviewed before committing).
/// </summary>
[Collection(ImpellerGLCollection.Name)]
public sealed class GoldenImageTests
{
    private const int PerChannelTolerance = 8;
    private const double MaxDiffFraction = 0.0001; // 0.01% of pixels may differ

    private readonly ImpellerGLFixture _gl;

    public GoldenImageTests(ImpellerGLFixture gl) => _gl = gl;

    private sealed record SceneCase(IScene Scene, int Width, int Height);

    private static readonly Dictionary<string, SceneCase> Cases = BuildCases(
        new SolidShapesScene(),    320, 280,
        new PrimitivesScene(),     360, 280,
        new StrokesScene(),        360, 300,
        new TransformsScene(),     320, 300,
        new ClipsScene(),          320, 300,
        new BlendModesScene(),     372, 312,
        new ColorFiltersScene(),   340, 190,
        new MaskBlurScene(),       360, 300,
        new ShadowsScene(),        340, 180,
        new GroupOpacityScene(),   300, 260,
        new ImageFiltersScene(),   260, 240,
        new BoundsAndTransformScene(), 340, 300,
        new GradientsScene(),      360, 310,
        new CpuTextureScene(),     240, 200,
        new CustomFontScene(),     360, 260);

    public static TheoryData<string> SceneNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in Cases.Keys)
        {
            data.Add(name);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void Scene_matches_golden(string sceneName)
    {
        RenderGate.Require(_gl);

        var @case = Cases[sceneName];
        var actual = _gl.Render(@case.Scene, @case.Width, @case.Height);
        Assert.True(actual.HasContent(), $"Scene '{sceneName}' produced an empty (all-zero) image.");

        GoldenAssert.Matches(sceneName, actual, PerChannelTolerance, MaxDiffFraction);
    }

    private static Dictionary<string, SceneCase> BuildCases(params object[] entries)
    {
        var dict = new Dictionary<string, SceneCase>();
        for (int i = 0; i < entries.Length; i += 3)
        {
            var scene = (IScene)entries[i];
            dict[scene.TestName] = new SceneCase(scene, (int)entries[i + 1], (int)entries[i + 2]);
        }
        return dict;
    }
}

internal static class GoldenAssert
{
    public static void Matches(string name, RawImage actual, int perChannelTolerance, double maxDiffFraction)
    {
        string rawPath = Path.Combine(GoldensDir(), name + ".rimg");
        bool update = Environment.GetEnvironmentVariable("UPDATE_GOLDENS") == "1";

        if (update || !File.Exists(rawPath))
        {
            if (!update && RenderGate.IsCI)
            {
                Assert.Fail($"Golden baseline not found in CI: {Path.GetFullPath(rawPath)} — it must be committed.");
            }

            actual.SaveRaw(rawPath);
            actual.SavePng(Path.Combine(GoldensDir(), name + ".png"));
            Assert.Skip(update
                ? $"Baseline updated: {rawPath}"
                : $"Baseline created (review before committing): {rawPath}");
        }

        var golden = RawImage.LoadRaw(rawPath);
        var diff = golden.Compare(actual, perChannelTolerance);

        if (!diff.SizeMatches || diff.DiffFraction > maxDiffFraction)
        {
            string outDir = FailureDir();
            actual.SavePng(Path.Combine(outDir, name + ".actual.png"));
            golden.SavePng(Path.Combine(outDir, name + ".golden.png"));
            Assert.Fail(
                $"'{name}' differs from golden: sizeMatches={diff.SizeMatches}, " +
                $"diffPixels={diff.DiffPixels}/{diff.TotalPixels} ({diff.DiffFraction:P3}), " +
                $"maxChannelDiff={diff.MaxChannelDiff}. Wrote actual/golden PNGs to {outDir}. " +
                $"If this change is intended, regenerate with UPDATE_GOLDENS=1.");
        }
    }

    public static string GoldensDir([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "Goldens");

    private static string FailureDir()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "TestFailures");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
