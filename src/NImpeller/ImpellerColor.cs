namespace NImpeller;

public partial record struct ImpellerColor
{
    public static ImpellerColor FromArgb(int a, int r, int g, int b) =>
        new()
        {
            Alpha = a / 255.0f,
            Red = r / 255.0f,
            Green = g / 255.0f,
            Blue = b / 255.0f
        };
    
    public static ImpellerColor FromRgb(int r, int g, int b) =>
        FromArgb(255, r, g, b);
}