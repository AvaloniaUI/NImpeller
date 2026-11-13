using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Port of the interop <c>CanRenderShadows</c> playground, extended to several shapes and elevations.</summary>
public sealed class ShadowsScene : IScene, IDisposable
{
    public string Name => "Shadows";
    public string CommandLineName => "shadows";
    public string Description => "DrawShadow under rect, rounded rect, oval and star paths at animated elevations, with opaque and transparent occluders";

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(214, 218, 226));

        float k = MathF.Max(1f, p.Height / 640f);
        float pad = 20f * k, header = 28f * k;
        const int cols = 4, rows = 2;
        float cw = (p.Width - pad * (cols + 1)) / cols;
        float ch = (p.Height - pad * (rows + 1) - header * rows) / rows;
        if (cw <= 1f || ch <= 1f) return;

        string[] rowLabels = ["Opaque occluder", "Transparent occluder"];
        var shadowColor = ImpellerColor.FromArgb(150, 0, 0, 0);

        for (int row = 0; row < rows; row++)
        {
            bool transparent = row == 1;
            float y = pad + row * (ch + header + pad);
            Label(b, _tc, rowLabels[row], pad, y + 4f * k, 15f * k, ImpellerColor.FromRgb(60, 64, 76));
            y += header;

            for (int col = 0; col < cols; col++)
            {
                float x = pad + col * (cw + pad);
                float size = MathF.Min(cw, ch) * 0.62f;
                float sx = x + (cw - size) / 2f, sy = y + (ch - size) / 2f;
                float elevation = k * 2f + k * 22f * (0.5f + 0.5f * MathF.Sin(t * 1.3f + col * 0.8f + row * 0.4f));

                using var path = Shape(col, sx, sy, size, t);
                b.DrawShadow(path, shadowColor, elevation, transparent ? 1 : 0, 1f);

                var color = Hsv(0.02f + col * 0.17f, 0.6f, 0.92f, transparent ? 0.35f : 1f);
                using var fill = Fill(color);
                b.DrawPath(path, fill);

                Label(b, _tc, $"elevation {elevation / k:0}", x, y + ch - 16f * k, 12f * k);
            }
        }
    }

    private static ImpellerPath Shape(int index, float x, float y, float size, float t) => index switch
    {
        0 => Path(pb => pb.AddRect(Rect(x, y, size, size))),
        1 => Path(pb => pb.AddRoundedRect(Rect(x, y, size, size), Radii(size * 0.2f))),
        2 => Path(pb => pb.AddOval(Rect(x, y, size, size))),
        _ => Star(x + size / 2f, y + size / 2f, size / 2f, size / 4.5f, 5, t * 0.3f),
    };

    public void Dispose() => _tc.Dispose();
}
