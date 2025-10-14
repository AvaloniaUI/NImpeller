namespace NImpeller;

public partial record struct ImpellerRect
{
    public ImpellerRect()
    {
        
    }

    public ImpellerRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}