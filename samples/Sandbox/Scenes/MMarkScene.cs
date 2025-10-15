using System;
using System.Collections.Generic;
using NImpeller;

namespace Sandbox.Scenes;

public class MMarkScene : IScene
{
    private const int Width = 1600;
    private const int Height = 900;
    private const int GridWidth = 80;
    private const int GridHeight = 40;

    private static readonly ImpellerColor[] Colors = new[]
    {
        new ImpellerColor { Red = 0x10 / 255f, Green = 0x10 / 255f, Blue = 0x10 / 255f, Alpha = 1f, Color_space = ImpellerColorSpace.kImpellerColorSpaceSRGB },
        new ImpellerColor { Red = 0x80 / 255f, Green = 0x80 / 255f, Blue = 0x80 / 255f, Alpha = 1f, Color_space = ImpellerColorSpace.kImpellerColorSpaceSRGB },
        new ImpellerColor { Red = 0xc0 / 255f, Green = 0xc0 / 255f, Blue = 0xc0 / 255f, Alpha = 1f, Color_space = ImpellerColorSpace.kImpellerColorSpaceSRGB },
        new ImpellerColor { Red = 0x10 / 255f, Green = 0x10 / 255f, Blue = 0x10 / 255f, Alpha = 1f, Color_space = ImpellerColorSpace.kImpellerColorSpaceSRGB },
        new ImpellerColor { Red = 0x80 / 255f, Green = 0x80 / 255f, Blue = 0x80 / 255f, Alpha = 1f, Color_space = ImpellerColorSpace.kImpellerColorSpaceSRGB },
        new ImpellerColor { Red = 0xc0 / 255f, Green = 0xc0 / 255f, Blue = 0xc0 / 255f, Alpha = 1f, Color_space = ImpellerColorSpace.kImpellerColorSpaceSRGB },
        new ImpellerColor { Red = 0xe0 / 255f, Green = 0x10 / 255f, Blue = 0x40 / 255f, Alpha = 1f, Color_space = ImpellerColorSpace.kImpellerColorSpaceSRGB },
    };

    private static readonly (int, int)[] Offsets = new[] { (-4, 0), (2, 0), (1, -2), (1, 2) };

    private readonly List<Element> _elements = new();
    private readonly Random _rng = new();

    private struct GridPoint
    {
        public int X;
        public int Y;

        public GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public ImpellerPoint Coordinate()
        {
            var scaleX = Width / (float)(GridWidth + 1);
            var scaleY = Height / (float)(GridHeight + 1);
            return new ImpellerPoint
            {
                X = (X + 0.5f) * scaleX,
                Y = 100.0f + (Y + 0.5f) * scaleY
            };
        }

        public GridPoint RandomPoint(Random rng)
        {
            var offset = Offsets[rng.Next(Offsets.Length)];
            var x = X + offset.Item1;
            if (x < 0 || x > GridWidth)
                x -= offset.Item1 * 2;

            var y = Y + offset.Item2;
            if (y < 0 || y > GridHeight)
                y -= offset.Item2 * 2;

            return new GridPoint(x, y);
        }
    }

    private enum SegmentType
    {
        Line,
        Quad,
        Cubic
    }

    private class Element
    {
        public SegmentType Type;
        public ImpellerPoint Start;
        public ImpellerPoint End;
        public ImpellerPoint Control1;
        public ImpellerPoint Control2;
        public int ColorIndex;
        public float Width;
        public bool IsSplit;
        public GridPoint GridPoint;

        public static Element NewRandom(GridPoint last, Random rng)
        {
            var segType = rng.Next(4);
            var next = last.RandomPoint(rng);
            
            var element = new Element
            {
                Start = last.Coordinate(),
                ColorIndex = rng.Next(Colors.Length),
                Width = (float)Math.Pow(rng.NextDouble(), 5) * 20.0f + 1.0f,
                IsSplit = rng.NextDouble() > 0.5
            };

            if (segType < 2)
            {
                element.Type = SegmentType.Line;
                element.End = next.Coordinate();
                element.GridPoint = next;
            }
            else if (segType < 3)
            {
                var p2 = next.RandomPoint(rng);
                element.Type = SegmentType.Quad;
                element.Control1 = next.Coordinate();
                element.End = p2.Coordinate();
                element.GridPoint = p2;
            }
            else
            {
                var p2 = next.RandomPoint(rng);
                var p3 = next.RandomPoint(rng);
                element.Type = SegmentType.Cubic;
                element.Control1 = next.Coordinate();
                element.Control2 = p2.Coordinate();
                element.End = p3.Coordinate();
                element.GridPoint = p3;
            }

            return element;
        }
    }

    public void Resize(int n)
    {
        var oldN = _elements.Count;
        if (n < oldN)
        {
            _elements.RemoveRange(n, oldN - n);
        }
        else if (n > oldN)
        {
            var last = _elements.Count > 0
                ? _elements[^1].GridPoint
                : new GridPoint(GridWidth / 2, GridHeight / 2);

            for (int i = oldN; i < n; i++)
            {
                var element = Element.NewRandom(last, _rng);
                last = element.GridPoint;
                _elements.Add(element);
            }
        }
    }

    public void Render(ImpellerContext context, ImpellerDisplayListBuilder builder, SceneParameters sceneParameters)
    {
        var complexity = sceneParameters.Complexity;
        var n = complexity < 10
            ? (complexity + 1) * 1000
            : Math.Min((complexity - 8) * 10000, 120_000);

        Resize(n);

        
        var pathBuilder = ImpellerPathBuilder.New();
        if (pathBuilder == null) return;
        
        using var paint = ImpellerPaint.New();
        if(paint == null)
            return;
        paint.SetDrawStyle(ImpellerDrawStyle.kImpellerDrawStyleStroke);
        paint.SetStrokeCap(ImpellerStrokeCap.kImpellerStrokeCapRound);
        paint.SetStrokeJoin(ImpellerStrokeJoin.kImpellerStrokeJoinRound);
        
        int colorIndex = -1;

        var len = _elements.Count;
        for (int i = 0; i < len; i++)
        {

            
            var element = _elements[i];
            
            // Start new path if needed
            if (i == 0 || _elements[i - 1].IsSplit)
            {
                pathBuilder.MoveTo(element.Start);
            }

            // Add segment
            switch (element.Type)
            {
                case SegmentType.Line:
                    pathBuilder.LineTo(element.End);
                    break;
                case SegmentType.Quad:
                    pathBuilder.QuadraticCurveTo(element.Control1, element.End);
                    break;
                case SegmentType.Cubic:
                    pathBuilder.CubicCurveTo(element.Control1, element.Control2, element.End);
                    break;
            }

            // Draw path if split or last element
            if (element.IsSplit || i == len - 1)
            {
                using var path = pathBuilder.TakePathNew(ImpellerFillType.kImpellerFillTypeNonZero);
                pathBuilder.Dispose();
                pathBuilder = ImpellerPathBuilder.New()!;
                
                if (path != null)
                {
                    if (colorIndex != element.ColorIndex)
                    {
                        colorIndex = element.ColorIndex;
                        paint.SetColor(Colors[colorIndex]);
                    }

                    paint.SetStrokeWidth(element.Width);

                        
                    builder.DrawPath(path, paint);
                }

                // Start new path for next segment if not last
                if (i < len - 1)
                {
                    pathBuilder.MoveTo(element.End);
                }
            }

            // Randomly toggle split
            if (_rng.NextDouble() > 0.995)
            {
                element.IsSplit = !element.IsSplit;
            }
        }
        pathBuilder.Dispose();
    }
}