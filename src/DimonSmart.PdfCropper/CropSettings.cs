using System;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Represents configuration for PDF cropping operations.
/// </summary>
public readonly struct CropSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CropSettings"/> struct.
    /// </summary>
    /// <param name="method">Cropping method to use.</param>
    /// <param name="excludeEdgeTouchingObjects">
    /// Whether to ignore content that touches current page boundaries when analyzing content bounds.
    /// </param>
    /// <param name="margin">Safety margin in points to add around detected content bounds.</param>
    /// <param name="edgeExclusionTolerance">Maximum distance from the page edge (in points) for content to be considered touching the edge.</param>
    public CropSettings(
        CropMethod method,
        bool excludeEdgeTouchingObjects = false,
        float margin = 0.5f,
        float edgeExclusionTolerance = 1.0f)
    {
        Method = method;
        ExcludeEdgeTouchingObjects = excludeEdgeTouchingObjects;
        Margin = margin;
        if (edgeExclusionTolerance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(edgeExclusionTolerance), edgeExclusionTolerance, "Edge exclusion tolerance must be non-negative.");
        }

        EdgeExclusionTolerance = edgeExclusionTolerance;
    }

    /// <summary>
    /// Gets the cropping method to use.
    /// </summary>
    public CropMethod Method { get; }

    /// <summary>
    /// Gets a value indicating whether edge-touching content should be ignored during content analysis.
    /// </summary>
    public bool ExcludeEdgeTouchingObjects { get; }

    /// <summary>
    /// Gets the safety margin in points to add around detected content bounds.
    /// </summary>
    public float Margin { get; }

    /// <summary>
    /// Gets the tolerance distance (in points) for classifying content as touching a page edge.
    /// </summary>
    public float EdgeExclusionTolerance { get; }

    /// <summary>
    /// Gets the default cropping settings.
    /// </summary>
    public static CropSettings Default => new(CropMethod.ContentBased);
}
