namespace DimonSmart.PdfCropper;

/// <summary>
/// Optional logger extension for emitting per-page debug diagnostics.
/// </summary>
public interface IPdfCropDebugLogger
{
    /// <summary>
    /// Returns true when detailed diagnostics should be logged for the specified page.
    /// </summary>
    /// <param name="pageIndex">The 1-based page index.</param>
    bool ShouldLogDebugForPage(int pageIndex);

    /// <summary>
    /// Gets the maximum number of content objects to log per page.
    /// </summary>
    int MaxObjectLogs { get; }
}
