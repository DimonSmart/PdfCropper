namespace DimonSmart.PdfCropper;

/// <summary>
/// Represents per-side safety margins in points.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CropMargins"/> struct.
/// </remarks>
/// <param name="left">Left margin in points.</param>
/// <param name="bottom">Bottom margin in points.</param>
/// <param name="right">Right margin in points.</param>
/// <param name="top">Top margin in points.</param>
public readonly struct CropMargins(float left, float bottom, float right, float top)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CropMargins"/> struct with the same margin on all sides.
    /// </summary>
    /// <param name="uniform">Uniform margin in points.</param>
    public CropMargins(float uniform) : this(uniform, uniform, uniform, uniform)
    {
    }

    /// <summary>
    /// Gets the left margin in points.
    /// </summary>
    public float Left { get; } = left;

    /// <summary>
    /// Gets the bottom margin in points.
    /// </summary>
    public float Bottom { get; } = bottom;

    /// <summary>
    /// Gets the right margin in points.
    /// </summary>
    public float Right { get; } = right;

    /// <summary>
    /// Gets the top margin in points.
    /// </summary>
    public float Top { get; } = top;

    /// <summary>
    /// Gets a value indicating whether all margins are equal.
    /// </summary>
    public bool IsUniform => Left == Right && Left == Top && Left == Bottom;

    /// <summary>
    /// Gets the average margin in points across all sides.
    /// </summary>
    public float Average => (Left + Right + Top + Bottom) / 4f;
}
