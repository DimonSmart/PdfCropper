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
    /// <param name="margin">Uniform safety margin in points to add around detected content bounds.</param>
    /// <param name="edgeExclusionTolerance">Maximum distance from the page edge (in points) for content to be considered touching the edge.</param>
    /// <param name="detectRepeatedObjects">Whether to exclude content objects that repeat across the majority of pages.</param>
    /// <param name="repeatedObjectOccurrenceThreshold">Percentage of analyzed pages on which an object must appear to be considered repeated.</param>
    /// <param name="repeatedObjectMinimumPageCount">Minimum document page count before repeated object detection is attempted.</param>
    /// <param name="pageRange">Optional page range filter that limits which pages are processed and retained.</param>
    public CropSettings(
        CropMethod method,
        bool excludeEdgeTouchingObjects = false,
        float margin = 0.5f,
        float edgeExclusionTolerance = 1.0f,
        bool detectRepeatedObjects = false,
        double repeatedObjectOccurrenceThreshold = 40.0,
        int repeatedObjectMinimumPageCount = 3,
        PageRange? pageRange = null)
        : this(
            method,
            excludeEdgeTouchingObjects,
            new CropMargins(margin),
            edgeExclusionTolerance,
            detectRepeatedObjects,
            repeatedObjectOccurrenceThreshold,
            repeatedObjectMinimumPageCount,
            pageRange)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CropSettings"/> struct.
    /// </summary>
    /// <param name="method">Cropping method to use.</param>
    /// <param name="margins">Per-side safety margins in points to add around detected content bounds.</param>
    /// <param name="edgeExclusionTolerance">Maximum distance from the page edge (in points) for content to be considered touching the edge.</param>
    /// <param name="detectRepeatedObjects">Whether to exclude content objects that repeat across the majority of pages.</param>
    /// <param name="repeatedObjectOccurrenceThreshold">Percentage of analyzed pages on which an object must appear to be considered repeated.</param>
    /// <param name="repeatedObjectMinimumPageCount">Minimum document page count before repeated object detection is attempted.</param>
    /// <param name="pageRange">Optional page range filter that limits which pages are processed and retained.</param>
    public CropSettings(
        CropMethod method,
        CropMargins margins,
        float edgeExclusionTolerance = 1.0f,
        bool detectRepeatedObjects = false,
        double repeatedObjectOccurrenceThreshold = 40.0,
        int repeatedObjectMinimumPageCount = 3,
        PageRange? pageRange = null)
        : this(
            method,
            excludeEdgeTouchingObjects: false,
            margins,
            edgeExclusionTolerance,
            detectRepeatedObjects,
            repeatedObjectOccurrenceThreshold,
            repeatedObjectMinimumPageCount,
            pageRange)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CropSettings"/> struct.
    /// </summary>
    /// <param name="method">Cropping method to use.</param>
    /// <param name="excludeEdgeTouchingObjects">
    /// Whether to ignore content that touches current page boundaries when analyzing content bounds.
    /// </param>
    /// <param name="margins">Per-side safety margins in points to add around detected content bounds.</param>
    /// <param name="edgeExclusionTolerance">Maximum distance from the page edge (in points) for content to be considered touching the edge.</param>
    /// <param name="detectRepeatedObjects">Whether to exclude content objects that repeat across the majority of pages.</param>
    /// <param name="repeatedObjectOccurrenceThreshold">Percentage of analyzed pages on which an object must appear to be considered repeated.</param>
    /// <param name="repeatedObjectMinimumPageCount">Minimum document page count before repeated object detection is attempted.</param>
    /// <param name="pageRange">Optional page range filter that limits which pages are processed and retained.</param>
    public CropSettings(
        CropMethod method,
        bool excludeEdgeTouchingObjects,
        CropMargins margins,
        float edgeExclusionTolerance = 1.0f,
        bool detectRepeatedObjects = false,
        double repeatedObjectOccurrenceThreshold = 40.0,
        int repeatedObjectMinimumPageCount = 3,
        PageRange? pageRange = null)
    {
        Method = method;
        ExcludeEdgeTouchingObjects = excludeEdgeTouchingObjects;
        Margins = margins;
        if (edgeExclusionTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(edgeExclusionTolerance), edgeExclusionTolerance, "Edge exclusion tolerance must be non-negative.");

        EdgeExclusionTolerance = edgeExclusionTolerance;
        DetectRepeatedObjects = detectRepeatedObjects;
        if (repeatedObjectOccurrenceThreshold <= 0 || repeatedObjectOccurrenceThreshold > 100)
            throw new ArgumentOutOfRangeException(nameof(repeatedObjectOccurrenceThreshold), repeatedObjectOccurrenceThreshold, "Repeated object threshold must be between 0 and 100 percent.");

        RepeatedObjectOccurrenceThreshold = repeatedObjectOccurrenceThreshold;
        if (repeatedObjectMinimumPageCount < 2)
            throw new ArgumentOutOfRangeException(nameof(repeatedObjectMinimumPageCount), repeatedObjectMinimumPageCount, "Repeated object detection requires at least two pages.");

        RepeatedObjectMinimumPageCount = repeatedObjectMinimumPageCount;
        if (pageRange?.HasError == true)
            throw new ArgumentException($"Page range expression is invalid: {pageRange.ErrorMessage}", nameof(pageRange));

        PageRange = pageRange;
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
    /// Gets the average safety margin in points across all sides.
    /// </summary>
    public float Margin => Margins.Average;

    /// <summary>
    /// Gets the per-side safety margins in points to add around detected content bounds.
    /// </summary>
    public CropMargins Margins { get; }

    /// <summary>
    /// Gets the tolerance distance (in points) for classifying content as touching a page edge.
    /// </summary>
    public float EdgeExclusionTolerance { get; }

    /// <summary>
    /// Gets a value indicating whether repeated content objects should be excluded from bounds detection.
    /// </summary>
    public bool DetectRepeatedObjects { get; }

    /// <summary>
    /// Gets the minimum percentage of analyzed pages on which an object must appear to be considered repeated.
    /// </summary>
    public double RepeatedObjectOccurrenceThreshold { get; }

    /// <summary>
    /// Gets the minimum number of pages required in a document before repeated object detection is applied.
    /// </summary>
    public int RepeatedObjectMinimumPageCount { get; }

    /// <summary>
    /// Gets an optional page range filter that limits which pages are processed and retained.
    /// </summary>
    public PageRange? PageRange { get; }

    /// <summary>
    /// Gets the default cropping settings.
    /// </summary>
    public static CropSettings Default => new(CropMethod.ContentBased);
}
