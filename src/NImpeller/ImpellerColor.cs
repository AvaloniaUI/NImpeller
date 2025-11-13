namespace NImpeller;

public partial record struct ImpellerColor
{
    public static ImpellerColor FromArgb(float a, float r, float g, float b) =>
        new()
        {
            Alpha = a / 255.0f,
            Red = r / 255.0f,
            Green = g / 255.0f,
            Blue = b / 255.0f
        };
    
    public static ImpellerColor FromRgb(float r, float g, float b) =>
        FromArgb(255, r, g, b);
}