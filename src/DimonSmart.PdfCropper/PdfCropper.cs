namespace DimonSmart.PdfCropper;

/// <summary>
/// Default implementation of <see cref="IPdfCropper"/> for PDF cropping operations.
/// </summary>
public sealed class PdfCropper : IPdfCropper
{
    /// <inheritdoc />
    public Task<byte[]> CropAsync(
        byte[] inputPdf,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger? logger = null,
        CancellationToken ct = default)
    {
        return PdfSmartCropper.CropAsync(inputPdf, cropSettings, optimizationSettings, logger, ct);
    }
}
