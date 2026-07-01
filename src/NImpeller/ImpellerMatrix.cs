using System;
using System.Numerics;

namespace NImpeller;

public partial struct ImpellerMatrix : IEquatable<ImpellerMatrix>
{
    public Matrix4x4 Matrix;
    public static implicit operator ImpellerMatrix(System.Numerics.Matrix4x4 m)
    {
        return new ImpellerMatrix() { Matrix = m };
    }

    public bool Equals(ImpellerMatrix other) => Matrix.Equals(other.Matrix);

    public override bool Equals(object? obj) => obj is ImpellerMatrix other && Equals(other);

    public override int GetHashCode() => Matrix.GetHashCode();

    public static bool operator ==(ImpellerMatrix left, ImpellerMatrix right) => left.Equals(right);

    public static bool operator !=(ImpellerMatrix left, ImpellerMatrix right) => !left.Equals(right);
}