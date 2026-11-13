using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Port of the interop <c>CanMeasureText</c> and <c>CanCreateParagraphs</c> playgrounds, with the metrics drawn.</summary>
public sealed class TextMetricsScene : IScene, IDisposable
{
    public string Name => "Text Metrics";
    public string CommandLineName => "textmetrics";
    public string Description => "Line metrics, grapheme cluster bounds by code unit and by coordinate, and word boundaries drawn over a multi-line emoji paragraph";

    private const string Text =
        "🏁 Can 👨‍👨‍👦‍👦 Measure 🔍 Text\nAnd this is line two.\nWhoa! Three lines. How high does this go?\r\nI stopped counting.";

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(250, 250, 252));

        float k = Draw.Scale(p);
        float pad = 16f * k;
        float width = p.Width - pad * 2f;
        if (width <= 1f) return;

        using var para = Paragraph(_tc, Text, 34f * k, ImpellerColor.FromRgb(20, 20, 24), width);
        var origin = Point(pad, pad + 20f * k);
        Label(b, _tc, $"lines {para.GetLineCount()}  height {para.GetHeight() / k:0.0}  longest line {para.GetLongestLineWidth() / k:0.0}  (logical px)",
            pad, pad, 13f * k);

        DrawLineMetrics(b, para, origin, k);
        float codeUnits = Text.Length;
        var index = (ulong)((t * 4f) % codeUnits);
        DrawWordAndCluster(b, para, origin, index, k);

        float px = para.GetLongestLineWidth() * (0.5f + 0.48f * MathF.Sin(t * 0.7f));
        float py = para.GetHeight() * (0.5f + 0.45f * MathF.Sin(t * 1.1f + 1f));
        DrawProbe(b, para, origin, px, py, k);

        b.DrawParagraph(para, origin);

        float y = origin.Y + para.GetHeight() + pad;
        Label(b, _tc, $"code unit {index} (yellow cluster, blue word)   probe ({px / k:0}, {py / k:0}) (red)", pad, y, 13f * k);
        y += 24f * k;

        using var big = Paragraph(_tc, "the ⚡️ quick ⚡️ brown 🦊 fox jumps over the lazy dog 🐶.", 44f * k,
            ImpellerColor.FromRgb(255, 0, 0), width, s =>
            {
                s.SetHeight(2f);
                using var bg = Fill(ImpellerColor.FromRgb(255, 240, 240));
                s.SetBackground(bg);
            });
        Label(b, _tc, "CanCreateParagraphs: height 2.0 with background paint", pad, y, 13f * k);
        b.DrawParagraph(big, Point(pad, y + 18f * k));
    }

    private static void DrawLineMetrics(ImpellerDisplayListBuilder b, ImpellerParagraph para, ImpellerPoint o, float k)
    {
        using var metrics = para.GetLineMetrics()!;
        using var box = Stroke(ImpellerColor.FromRgb(160, 160, 170), 1f * k);
        using var baseline = Stroke(ImpellerColor.FromRgb(0, 160, 90), 1.5f * k);
        using var hard = Fill(ImpellerColor.FromRgb(240, 120, 0));
        for (ulong i = 0; i < para.GetLineCount(); i++)
        {
            float left = o.X + (float)metrics.GetLeft(i);
            float base_ = o.Y + (float)metrics.GetBaseline(i);
            float top = base_ - (float)metrics.GetAscent(i);
            float w = (float)metrics.GetWidth(i);
            b.DrawRect(Rect(left, top, w, (float)(metrics.GetAscent(i) + metrics.GetDescent(i))), box);
            b.DrawLine(Point(left, base_), Point(left + w, base_), baseline);
            if (metrics.IsHardbreak(i) != 0)
            {
                b.DrawOval(Rect(left + w + 4f * k, base_ - 8f * k, 6f * k, 6f * k), hard);
            }
        }
    }

    private static void DrawWordAndCluster(ImpellerDisplayListBuilder b, ImpellerParagraph para, ImpellerPoint o, ulong index, float k)
    {
        para.GetWordBoundary(index, out var word);
        using var wordPaint = Fill(ImpellerColor.FromArgb(70, 60, 120, 255));
        for (ulong i = word.Start; i < word.End;)
        {
            using var g = para.CreateGlyphInfoAtCodeUnitIndexNew(i);
            if (g is null) break;
            var r = ClusterBounds(g);
            b.DrawRect(Rect(o.X + r.X, o.Y + r.Y, r.Width, r.Height), wordPaint);
            i = Math.Max(i + 1, g.GetGraphemeClusterCodeUnitRangeEnd());
        }

        using var glyph = para.CreateGlyphInfoAtCodeUnitIndexNew(index);
        if (glyph is null) return;
        var bounds = ClusterBounds(glyph);
        using var paint = Fill(ImpellerColor.FromArgb(150, 255, 210, 0));
        b.DrawRect(Rect(o.X + bounds.X, o.Y + bounds.Y, bounds.Width, bounds.Height), paint);
    }

    private static void DrawProbe(ImpellerDisplayListBuilder b, ImpellerParagraph para, ImpellerPoint o, float x, float y, float k)
    {
        using var glyph = para.CreateGlyphInfoAtParagraphCoordinatesNew(x, y);
        using var red = Stroke(ImpellerColor.FromRgb(230, 30, 30), 2f * k);
        if (glyph is not null)
        {
            var r = ClusterBounds(glyph);
            b.DrawRect(Rect(o.X + r.X, o.Y + r.Y, r.Width, r.Height), red);
        }
        using var dot = Fill(ImpellerColor.FromRgb(230, 30, 30));
        b.DrawOval(Rect(o.X + x - 4f * k, o.Y + y - 4f * k, 8f * k, 8f * k), dot);
    }

    // Upstream GlyphInfo::GetGraphemeClusterBounds (interop/glyph_info.cc) returns x and y swapped.
    private static ImpellerRect ClusterBounds(ImpellerGlyphInfo glyph)
    {
        glyph.GetGraphemeClusterBounds(out var r);
        return Rect(r.Y, r.X, r.Width, r.Height);
    }

    public void Dispose() => _tc.Dispose();
}
