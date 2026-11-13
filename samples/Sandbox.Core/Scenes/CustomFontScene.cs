using System.Diagnostics;
using NImpeller;
using static Sandbox.Scenes.Draw;

namespace Sandbox.Scenes;

/// <summary>Port of the interop <c>CanCreateParagraphsWithCustomFont</c> playground.</summary>
public sealed class CustomFontScene : IScene, IDisposable
{
    public string Name => "Custom Font";
    public string CommandLineName => "customfont";
    public string Description => "A font registered from memory, by its own family name and by an alias, with per-glyph styles and fallback for missing glyphs";

    private const string Alias = "SandboxWtf";

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly ImpellerTypographyContext _tc = ImpellerTypographyContext.New()!;
    private readonly bool _registered;
    private readonly bool _aliased;

    public CustomFontScene()
    {
        using var stream = typeof(CustomFontScene).Assembly.GetManifestResourceStream("Fonts/wtf.otf")
            ?? throw new InvalidOperationException("Missing embedded font 'wtf.otf'.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        using var mem = new ImpellerUnmanagedMemory(copy.GetBuffer().AsSpan(0, (int)copy.Length));
        _registered = _tc.RegisterFont(mem);
        _aliased = _tc.RegisterFont(mem, Alias);
    }

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        float t = (float)_clock.Elapsed.TotalSeconds;
        Background(b, ImpellerColor.FromRgb(18, 20, 26));

        float k = MathF.Max(1f, p.Height / 640f);
        float pad = 16f * k;
        float width = p.Width - pad * 2f;
        if (width <= 1f) return;

        var muted = ImpellerColor.FromRgb(150, 156, 170);
        float y = pad;
        Label(b, _tc, $"RegisterFont: family name {(_registered ? "ok" : "FAILED")}, alias \"{Alias}\" {(_aliased ? "ok" : "FAILED")}",
            pad, y, 13f * k, muted);
        y += 24f * k;

        Label(b, _tc, "\"WhatTheFlutter\" (upstream test)", pad, y, 13f * k, muted);
        y += 18f * k;
        using (var para = Paragraph(_tc, "0F0F0F0", 110f * k, new ImpellerColor { Green = 1f, Blue = 1f, Alpha = 1f }, width,
                   s => s.SetFontFamily("WhatTheFlutter")))
        {
            b.DrawParagraph(para, Point(pad, y));
            y += para.GetHeight() + 8f * k;
        }

        Label(b, _tc, $"\"{Alias}\" with one pushed style per glyph", pad, y, 13f * k, muted);
        y += 18f * k;
        using (var para = PerGlyph("F0F0F0F", 80f * k, t, width))
        {
            b.DrawParagraph(para, Point(pad, y));
            y += para.GetHeight() + 8f * k;
        }

        Label(b, _tc, "Same family with characters it may not cover (falls back)", pad, y, 13f * k, muted);
        y += 18f * k;
        using (var para = Paragraph(_tc, "0F Flutter 0F Impeller 0F", 44f * k, ImpellerColor.FromRgb(240, 200, 90), width,
                   s => s.SetFontFamily(Alias)))
        {
            b.DrawParagraph(para, Point(pad, y));
            y += para.GetHeight() + 8f * k;
        }

        Label(b, _tc, "Default font for comparison", pad, y, 13f * k, muted);
        y += 18f * k;
        using (var para = Paragraph(_tc, "0F0F0F0", 80f * k, ImpellerColor.FromRgb(200, 200, 210), width))
        {
            b.DrawParagraph(para, Point(pad, y));
        }
    }

    private ImpellerParagraph PerGlyph(string text, float size, float t, float width)
    {
        using var builder = _tc.ParagraphBuilderNew()!;
        for (int i = 0; i < text.Length; i++)
        {
            using var style = ImpellerParagraphStyle.New()!;
            using var fg = Fill(Hsv(t * 0.2f + i / (float)text.Length, 0.7f, 1f));
            style.SetForeground(fg);
            style.SetFontFamily(Alias);
            style.SetFontSize(size * (0.8f + 0.2f * MathF.Sin(t * 2f + i)));
            builder.PushStyle(style);
            builder.AddText(text[i].ToString());
            builder.PopStyle();
        }
        return builder.BuildParagraphNew(width)!;
    }

    public void Dispose() => _tc.Dispose();
}
