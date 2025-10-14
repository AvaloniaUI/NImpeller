using System.Numerics;

namespace NImpeller;

public partial struct ImpellerMatrix
{
    public Matrix4x4 Matrix;
    public static implicit operator ImpellerMatrix(System.Numerics.Matrix4x4 m)
    {
        return new ImpellerMatrix() { Matrix = m };
    }
}