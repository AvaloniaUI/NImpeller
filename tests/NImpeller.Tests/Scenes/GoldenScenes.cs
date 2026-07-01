using System.Numerics;
using NImpeller;

namespace NImpeller.Tests.Scenes;

internal static class Draw
{
    public static ImpellerPoint P(float x, float y) => new() { X = x, Y = y };

    // ImpellerRect's only ctor takes ints; this builds one from floats for computed coordinates.
    // TODO: ImpellerRect should have a float ctor.
    public static ImpellerRect Rect(float x, float y, float w, float h) =>
        new() { X = x, Y = y, Width = w, Height = h };

    public static ImpellerRoundingRadii UniformRadii(float r)
    {
        var p = P(r, r);
        return new ImpellerRoundingRadii { Top_left = p, Top_right = p, Bottom_left = p, Bottom_right = p };
    }

    public static void Background(ImpellerDisplayListBuilder b, ImpellerColor color)
    {
        using var paint = ImpellerPaint.New()!;
        paint.SetColor(color);
        b.DrawPaint(paint);
    }
}

/// <summary>Every primitive draw op, filled and stroked. Mirrors aiks_dl primitive shape tests.</summary>
public sealed class PrimitivesScene : IScene
{
    public string TestName => "primitives";
    public string Description => "All primitive draw ops, filled and stroked.";

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Draw.Background(b, ImpellerColor.FromRgb(18, 20, 28));
        using var paint = ImpellerPaint.New()!;

        paint.SetColor(ImpellerColor.FromRgb(220, 70, 70));
        b.DrawRect(new ImpellerRect(20, 20, 90, 60), paint);

        paint.SetColor(ImpellerColor.FromRgb(70, 200, 120));
        b.DrawOval(new ImpellerRect(130, 20, 90, 60), paint);

        paint.SetColor(ImpellerColor.FromRgb(80, 140, 240));
        b.DrawRoundedRect(new ImpellerRect(240, 20, 100, 60), Draw.UniformRadii(16), paint);

        paint.SetColor(ImpellerColor.FromRgb(240, 200, 70));
        b.DrawRoundedRectDifference(
            new ImpellerRect(20, 100, 100, 80), Draw.UniformRadii(18),
            new ImpellerRect(40, 120, 60, 40), Draw.UniformRadii(8),
            paint);

        // Stroked line + dashed line.
        paint.SetColor(ImpellerColor.FromRgb(230, 230, 230));
        paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleStroke);
        paint.SetStrokeWidth(5);
        b.DrawLine(Draw.P(150, 110), Draw.P(330, 110), paint);
        b.DrawDashedLine(Draw.P(150, 140), Draw.P(330, 140), 14, 8, paint);

        // Filled path (triangle).
        paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleFill);
        paint.SetColor(ImpellerColor.FromRgb(200, 120, 230));
        using var pb = ImpellerPathBuilder.New()!;
        pb.MoveTo(Draw.P(180, 250));
        pb.LineTo(Draw.P(240, 180));
        pb.LineTo(Draw.P(300, 250));
        pb.Close();
        using var path = pb.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
        b.DrawPath(path, paint);
    }
}

/// <summary>Stroke caps × joins grid. Mirrors aiks_dl_path StrokeCapsAndJoins.</summary>
public sealed class StrokesScene : IScene
{
    public string Name => "Strokes";
    public string TestName => "strokes";
    public string Description => "Stroke caps and joins across a grid.";

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Draw.Background(b, ImpellerColor.FromRgb(22, 24, 30));

        var caps = new[]
        {
            ImpellerStrokeCap.kImpellerStrokeCapButt,
            ImpellerStrokeCap.kImpellerStrokeCapRound,
            ImpellerStrokeCap.kImpellerStrokeCapSquare,
        };
        var joins = new[]
        {
            ImpellerStrokeJoin.kImpellerStrokeJoinMiter,
            ImpellerStrokeJoin.kImpellerStrokeJoinRound,
            ImpellerStrokeJoin.kImpellerStrokeJoinBevel,
        };

        using var paint = ImpellerPaint.New()!;
        paint.SetColor(ImpellerColor.FromRgb(120, 200, 255));
        paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleStroke);
        paint.SetStrokeWidth(12);

        for (int ci = 0; ci < caps.Length; ci++)
        {
            for (int ji = 0; ji < joins.Length; ji++)
            {
                paint.SetStrokeCap(caps[ci]);
                paint.SetStrokeJoin(joins[ji]);

                float ox = 30 + ci * 110;
                float oy = 30 + ji * 90;

                // An open "V" shows both caps (the open ends) and a join (the corner).
                using var pb = ImpellerPathBuilder.New()!;
                pb.MoveTo(Draw.P(ox, oy));
                pb.LineTo(Draw.P(ox + 35, oy + 50));
                pb.LineTo(Draw.P(ox + 70, oy));
                using var path = pb.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
                b.DrawPath(path, paint);
            }
        }
    }
}

/// <summary>Nested transforms (translate/scale/rotate/Transform) with save/restore.</summary>
public sealed class TransformsScene : IScene
{
    public string Name => "Transforms";
    public string TestName => "transforms";
    public string Description => "Translate/scale/rotate/matrix transforms with save/restore nesting.";

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Draw.Background(b, ImpellerColor.FromRgb(16, 18, 24));
        using var paint = ImpellerPaint.New()!;

        b.Save();
        b.Translate(90, 90);
        b.Rotate(25);
        paint.SetColor(ImpellerColor.FromRgb(220, 80, 80));
        b.DrawRect(new ImpellerRect(-40, -40, 80, 80), paint);
        b.Restore();

        b.Save();
        b.Translate(230, 90);
        b.Scale(1.6f, 0.7f);
        paint.SetColor(ImpellerColor.FromRgb(80, 210, 130));
        b.DrawRect(new ImpellerRect(-40, -40, 80, 80), paint);
        b.Restore();

        b.Save();
        b.SetTransform(Matrix4x4.CreateRotationZ(0.5f) * Matrix4x4.CreateTranslation(160, 220, 0));
        paint.SetColor(ImpellerColor.FromRgb(90, 150, 240));
        b.DrawOval(new ImpellerRect(-50, -30, 100, 60), paint);
        b.Restore();
    }
}

/// <summary>Clip ops (rect/oval/roundedRect/path), intersect vs difference, nested.</summary>
public sealed class ClipsScene : IScene
{
    public string Name => "Clips";
    public string TestName => "clips";
    public string Description => "Rect/oval/rounded/difference clips applied to a fill.";

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Draw.Background(b, ImpellerColor.FromRgb(20, 22, 28));
        using var fill = ImpellerPaint.New()!;
        fill.SetColor(ImpellerColor.FromRgb(230, 150, 60));

        b.Save();
        b.ClipRect(new ImpellerRect(20, 20, 130, 130), ImpellerClipOperation.kImpellerClipOperationIntersect);
        b.DrawPaint(fill);
        b.Restore();

        b.Save();
        b.ClipOval(new ImpellerRect(170, 20, 130, 130), ImpellerClipOperation.kImpellerClipOperationIntersect);
        fill.SetColor(ImpellerColor.FromRgb(90, 200, 210));
        b.DrawPaint(fill);
        b.Restore();

        // Difference clip: a rect with an oval punched out (a ring/frame).
        b.Save();
        b.ClipRect(new ImpellerRect(20, 170, 130, 110), ImpellerClipOperation.kImpellerClipOperationIntersect);
        b.ClipOval(new ImpellerRect(50, 190, 70, 70), ImpellerClipOperation.kImpellerClipOperationDifference);
        fill.SetColor(ImpellerColor.FromRgb(200, 110, 220));
        b.DrawPaint(fill);
        b.Restore();

        b.Save();
        b.ClipRoundedRect(new ImpellerRect(170, 170, 130, 110), Draw.UniformRadii(28),
            ImpellerClipOperation.kImpellerClipOperationIntersect);
        fill.SetColor(ImpellerColor.FromRgb(150, 210, 90));
        b.DrawPaint(fill);
        b.Restore();
    }
}

// NOTE on offscreen-layer ops (group opacity, image filters, advanced blend modes): these render
// correctly, but on the NVIDIA proprietary driver the GL context SIGSEGVs at *teardown* after
// using them (a driver bug — the same code runs cleanly on Mesa/llvmpipe). The golden tests
// therefore gate on a software renderer (see GoldenImageTests / README), and run everything there.

/// <summary>A grid of all 29 blend modes: a red oval drawn over a blue rect per mode.</summary>
public sealed class BlendModesScene : IScene
{
    public string Name => "Blend Modes";
    public string TestName => "blendmodes";
    public string Description => "All 29 blend modes in a grid (src oval over dst rect).";

    private static readonly ImpellerBlendMode[] Modes =
        (ImpellerBlendMode[])System.Enum.GetValues(typeof(ImpellerBlendMode));

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Draw.Background(b, ImpellerColor.FromRgb(40, 40, 48));

        using var dst = ImpellerPaint.New()!;
        using var src = ImpellerPaint.New()!;
        const int cols = 6, cell = 60;

        for (int i = 0; i < Modes.Length; i++)
        {
            int col = i % cols, row = i / cols;
            float x = 6 + col * cell, y = 6 + row * cell;

            dst.SetColor(ImpellerColor.FromArgb(255, 60, 120, 220));
            dst.SetBlendMode(ImpellerBlendMode.kImpellerBlendModeSourceOver);
            b.DrawRect(Draw.Rect(x, y, 40, 40), dst);

            src.SetColor(ImpellerColor.FromArgb(220, 230, 90, 60));
            src.SetBlendMode(Modes[i]);
            b.DrawOval(Draw.Rect(x + 12, y + 12, 36, 36), src);
        }
    }
}

/// <summary>Blend color filter and a color-matrix (invert) color filter.</summary>
public sealed class ColorFiltersScene : IScene
{
    public string Name => "Color Filters";
    public string TestName => "colorfilters";
    public string Description => "Blend color filter and an invert color-matrix filter.";

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Draw.Background(b, ImpellerColor.FromRgb(20, 22, 30));

        using (var blend = ImpellerColorFilter.CreateBlendNew(
                   ImpellerColor.FromArgb(180, 0, 140, 255), ImpellerBlendMode.kImpellerBlendModeModulate)!)
        {
            using var paint = ImpellerPaint.New()!;
            paint.SetColor(ImpellerColor.FromRgb(220, 200, 80));
            paint.SetColorFilter(blend);
            b.DrawRect(new ImpellerRect(30, 40, 130, 110), paint);
        }

        // Invert color matrix: negate RGB (bias +1), keep alpha.
        var invert = new ImpellerColorMatrix();
        float[] values =
        {
            -1, 0, 0, 0, 1,
            0, -1, 0, 0, 1,
            0, 0, -1, 0, 1,
            0, 0, 0, 1, 0,
        };
        unsafe
        {
            for (int i = 0; i < 20; i++) invert.m[i] = values[i];
        }
        using (var matrixFilter = ImpellerColorFilter.CreateColorMatrixNew(invert)!)
        {
            using var paint = ImpellerPaint.New()!;
            paint.SetColor(ImpellerColor.FromRgb(60, 200, 130));
            paint.SetColorFilter(matrixFilter);
            b.DrawOval(new ImpellerRect(190, 40, 120, 110), paint);
        }
    }
}

/// <summary>Mask-filter blur in all four blur styles.</summary>
public sealed class MaskBlurScene : IScene
{
    public string Name => "Mask Blur";
    public string TestName => "maskblur";
    public string Description => "Mask-filter blur in Normal/Solid/Outer/Inner styles.";

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Draw.Background(b, ImpellerColor.FromRgb(28, 28, 34));

        var styles = new[]
        {
            ImpellerBlurStyle.kImpellerBlurStyleNormal,
            ImpellerBlurStyle.kImpellerBlurStyleSolid,
            ImpellerBlurStyle.kImpellerBlurStyleOuter,
            ImpellerBlurStyle.kImpellerBlurStyleInner,
        };

        for (int i = 0; i < styles.Length; i++)
        {
            using var mask = ImpellerMaskFilter.CreateBlurNew(styles[i], 8)!;
            using var paint = ImpellerPaint.New()!;
            paint.SetColor(ImpellerColor.FromRgb(240, 230, 120));
            paint.SetMaskFilter(mask);
            float x = 40 + (i % 2) * 160;
            float y = 40 + (i / 2) * 130;
            b.DrawOval(Draw.Rect(x, y, 90, 90), paint);
        }
    }
}

/// <summary>DrawShadow under an opaque occluder. Mirrors aiks_dl_shadow / CanRenderShadows.</summary>
public sealed class ShadowsScene : IScene
{
    public string Name => "Shadows";
    public string TestName => "shadows";
    public string Description => "Elevation shadows cast by an oval and a rounded rect.";

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Draw.Background(b, ImpellerColor.FromRgb(210, 212, 220));
        var shadowColor = ImpellerColor.FromArgb(160, 0, 0, 0);

        using (var pb = ImpellerPathBuilder.New()!)
        {
            pb.AddOval(new ImpellerRect(40, 40, 110, 110));
            using var path = pb.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
            b.DrawShadow(path, shadowColor, 8f, 0, 1f);
            using var paint = ImpellerPaint.New()!;
            paint.SetColor(ImpellerColor.FromRgb(220, 90, 90));
            b.DrawPath(path, paint);
        }

        using (var pb = ImpellerPathBuilder.New()!)
        {
            pb.AddRoundedRect(new ImpellerRect(190, 50, 120, 90), Draw.UniformRadii(20));
            using var path = pb.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
            b.DrawShadow(path, shadowColor, 14f, 0, 1f);
            using var paint = ImpellerPaint.New()!;
            paint.SetColor(ImpellerColor.FromRgb(90, 140, 230));
            b.DrawPath(path, paint);
        }
    }
}

/// <summary>Group opacity: a nested display list composited at 50% alpha (needs an offscreen layer).</summary>
public sealed class GroupOpacityScene : IScene
{
    public string Name => "Group Opacity";
    public string TestName => "groupopacity";
    public string Description => "Two overlapping rects composited as a group at 50% opacity.";

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Draw.Background(b, ImpellerColor.FromRgb(24, 26, 34));

        ImpellerDisplayList child;
        using (var cb = ImpellerDisplayListBuilder.New(new ImpellerRect(0, 0, p.Width, p.Height))!)
        {
            using var cp = ImpellerPaint.New()!;
            cp.SetColor(ImpellerColor.FromRgb(230, 80, 80));
            cb.DrawRect(new ImpellerRect(30, 30, 150, 150), cp);
            cp.SetColor(ImpellerColor.FromRgb(80, 130, 230));
            cb.DrawRect(new ImpellerRect(120, 80, 150, 150), cp);
            child = cb.CreateDisplayListNew()!;
        }
        using (child)
        {
            b.DrawDisplayList(child, 0.5f);
        }
    }
}

/// <summary>Image filters applied via paint: blur, dilate, erode, matrix (each needs an offscreen layer).</summary>
public sealed class ImageFiltersScene : IScene
{
    public string Name => "Image Filters";
    public string TestName => "imagefilters";
    public string Description => "Blur, dilate, erode, and matrix image filters on shapes.";

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Draw.Background(b, ImpellerColor.FromRgb(18, 20, 26));

        using (var blur = ImpellerImageFilter.CreateBlurNew(5, 5, ImpellerTileMode.kImpellerTileModeClamp)!)
        {
            using var paint = ImpellerPaint.New()!;
            paint.SetColor(ImpellerColor.FromRgb(230, 90, 90));
            paint.SetImageFilter(blur);
            b.DrawRect(new ImpellerRect(30, 30, 80, 80), paint);
        }

        using (var dilate = ImpellerImageFilter.CreateDilateNew(4, 4)!)
        {
            using var paint = ImpellerPaint.New()!;
            paint.SetColor(ImpellerColor.FromRgb(90, 210, 120));
            paint.SetImageFilter(dilate);
            b.DrawRect(new ImpellerRect(150, 30, 80, 80), paint);
        }

        using (var erode = ImpellerImageFilter.CreateErodeNew(2, 2)!)
        {
            using var paint = ImpellerPaint.New()!;
            paint.SetColor(ImpellerColor.FromRgb(90, 150, 240));
            paint.SetImageFilter(erode);
            b.DrawOval(new ImpellerRect(30, 140, 80, 80), paint);
        }
    }
}

/// <summary>
/// Renders driven by queried geometry: each shape is framed by the rectangle reported by
/// <see cref="ImpellerPath.GetBounds"/>, and a marker is placed by mapping a local point through
/// the matrix reported by <see cref="ImpellerDisplayListBuilder.GetTransform"/>. If either query
/// returned nothing, the frames and marker would be misplaced, so the golden pins both features.
/// </summary>
public sealed class BoundsAndTransformScene : IScene
{
    public string TestName => "boundsandtransform";
    public string Description => "Shapes framed by their queried path bounds; a marker placed via the queried transform.";

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder b, SceneParameters p)
    {
        Draw.Background(b, ImpellerColor.FromRgb(18, 20, 28));

        using var fill = ImpellerPaint.New()!;
        using var frame = ImpellerPaint.New()!;
        frame.SetColor(ImpellerColor.FromRgb(90, 230, 140));
        frame.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleStroke);
        frame.SetStrokeWidth(3);

        // A triangle, a rect, and an oval — each filled, then outlined by its reported bounds.
        using (var pb = ImpellerPathBuilder.New()!)
        {
            pb.MoveTo(Draw.P(40, 120));
            pb.LineTo(Draw.P(90, 40));
            pb.LineTo(Draw.P(140, 120));
            pb.Close();
            using var tri = pb.CopyPathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
            fill.SetColor(ImpellerColor.FromRgb(220, 80, 80));
            b.DrawPath(tri, fill);
            tri.GetBounds(out var bounds);
            b.DrawRect(bounds, frame);
        }

        using (var pb = ImpellerPathBuilder.New()!)
        {
            pb.AddRect(new ImpellerRect(180, 40, 120, 70));
            using var rect = pb.CopyPathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
            fill.SetColor(ImpellerColor.FromRgb(80, 140, 240));
            b.DrawPath(rect, fill);
            rect.GetBounds(out var bounds);
            b.DrawRect(bounds, frame);
        }

        using (var pb = ImpellerPathBuilder.New()!)
        {
            pb.AddOval(new ImpellerRect(40, 170, 120, 90));
            using var oval = pb.CopyPathNew(ImpellerFillType.kImpellerFillTypeNonZero)!;
            fill.SetColor(ImpellerColor.FromRgb(240, 200, 70));
            b.DrawPath(oval, fill);
            oval.GetBounds(out var bounds);
            b.DrawRect(bounds, frame);
        }

        // Draw a rect under a translate+scale, query the resulting transform, then map its local
        // centre back to root space and drop a marker there.
        b.Save();
        b.Translate(240, 210);
        b.Scale(0.8f, 0.8f);
        fill.SetColor(ImpellerColor.FromRgb(200, 120, 230));
        b.DrawRect(new ImpellerRect(-40, -40, 80, 80), fill);
        b.GetTransform(out var transform);
        b.Restore();

        var centre = Vector2.Transform(Vector2.Zero, transform.Matrix);
        using var marker = ImpellerPaint.New()!;
        marker.SetColor(ImpellerColor.FromRgb(255, 255, 255));
        b.DrawOval(Draw.Rect(centre.X - 8, centre.Y - 8, 16, 16), marker);
    }
}

public sealed class SolidShapesScene : IScene
{
    public string TestName => "solidshapes";

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder scene, SceneParameters sceneParameters)
    {
        using var paint = ImpellerPaint.New()!;

        // Opaque background so every pixel is defined.
        paint.SetColor(ImpellerColor.FromRgb(20, 24, 40));
        scene.DrawPaint(paint);

        paint.SetColor(ImpellerColor.FromRgb(220, 60, 60));
        scene.DrawRect(new ImpellerRect(40, 40, 120, 80), paint);

        paint.SetColor(ImpellerColor.FromRgb(60, 200, 120));
        scene.DrawOval(new ImpellerRect(180, 50, 110, 110), paint);

        paint.SetColor(ImpellerColor.FromRgb(240, 200, 60));
        scene.DrawRect(new ImpellerRect(40, 160, 90, 90), paint);

        // A rotated rect to exercise the transform stack deterministically.
        paint.SetColor(ImpellerColor.FromRgb(90, 140, 240));
        scene.SetTransform(
            Matrix4x4.CreateRotationZ(0.4f) *
            Matrix4x4.CreateTranslation(220, 210, 0));
        scene.DrawRect(new ImpellerRect(-35, -35, 70, 70), paint);
    }
}

