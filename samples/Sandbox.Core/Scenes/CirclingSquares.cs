using System.Diagnostics;
using System.Numerics;
using NImpeller;

namespace Sandbox.Scenes;

public class CirclingSquares : IScene
{
    public string Name => "Circling Squares";
    public string Description => "Animated red squares rotating in a circle pattern, demonstrating matrix transformations and animation";

    Stopwatch st = Stopwatch.StartNew();
    public void Render(ImpellerContext context, ImpellerDisplayListBuilder scene, SceneParameters sceneParameters)
    {
        var time = st.Elapsed.TotalSeconds;
        using var paint = ImpellerPaint.New()!;
        paint.SetColor(ImpellerColor.FromRgb(255, 0, 0));
                
        for (int c = 0; c < 8; c++)
        {
            var positionAngle = time + (c * 3.14 / 4);    
            var rotationAngle = -time + (c * 3.14 / 4);

            var center = new Vector2(
                sceneParameters.Width / 2f + (float)(Math.Cos(positionAngle) * 100),
                sceneParameters.Height / 2f + (float)(Math.Sin(positionAngle) * 100)
            );

            var transform = Matrix4x4.CreateRotationZ((float)rotationAngle) *
                            Matrix4x4.CreateTranslation(center.X, center.Y, 0);


            scene.SetTransform(transform);
                    
                    
            scene.DrawRect(new ImpellerRect(0, 0, 50, 50), paint);
        }
    }
}