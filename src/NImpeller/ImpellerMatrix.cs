using System.Numerics;

namespace NImpeller;

/// <summary>
/// Represents a 4×4 transformation matrix.
/// </summary>
/// <remarks>
/// <para>
/// A Matrix is used for 2D and 3D transformations including translation, rotation, scaling, and skewing.
/// Matrices are stored in column-major order.
/// </para>
/// <para>
/// The matrix elements are indexed as:
/// <code>
/// [ m0  m4  m8   m12 ]
/// [ m1  m5  m9   m13 ]
/// [ m2  m6  m10  m14 ]
/// [ m3  m7  m11  m15 ]
/// </code>
/// </para>
/// </remarks>
public partial struct ImpellerMatrix
{
    /// <summary>
    /// The matrix elements in column-major order (16 floats).
    /// </summary>
    public Matrix4x4 Matrix;

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix"/> struct with the specified elements.
    /// </summary>
    /// <param name="m">A 16-element array containing matrix values in column-major order.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="m"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="m"/> length is not 16.</exception>
    public static implicit operator ImpellerMatrix(System.Numerics.Matrix4x4 m)
    {
        ArgumentNullException.ThrowIfNull(m);
        if (m.Length != 16)
        {
            throw new ArgumentException($"Matrix must contain exactly 16 elements. Provided: {m.Length} elements.", nameof(m));
        }
        
        return new ImpellerMatrix() { Matrix = m };
    }
}