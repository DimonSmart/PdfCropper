namespace DimonSmart.PdfCropper;

/// <summary>
/// Specifies the method to use for cropping PDF pages.
/// </summary>
public enum CropMethod
{
    /// <summary>
    /// Uses content analysis to determine crop boundaries (default).
    /// Analyzes text, images, and paths to find content bounds.
    /// </summary>
    ContentBased = 0,

    /// <summary>
    /// Renders page to bitmap and analyzes pixels to determine crop boundaries.
    /// More accurate for complex layouts but slower.
    /// </summary>
    BitmapBased = 1
}
