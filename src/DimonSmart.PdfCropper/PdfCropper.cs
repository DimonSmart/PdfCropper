using System.Collections.Generic;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Default implementation of <see cref="IPdfCropper"/> for PDF cropping operations.
/// </summary>
public sealed class PdfCropper : IPdfCropper
{
    /// <inheritdoc />
    public async Task<byte[]> CropAsync(
        byte[] inputPdf,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger? logger = null,
        CancellationToken ct = default)
    {
        return await PdfSmartCropper
            .CropAsync(inputPdf, cropSettings, optimizationSettings, logger, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<byte[]> CropAndMergeAsync(
        IEnumerable<byte[]> inputs,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger? logger = null,
        CancellationToken ct = default)
    {
        return await PdfSmartCropper
            .CropAndMergeAsync(inputs, cropSettings, optimizationSettings, logger, ct)
            .ConfigureAwait(false);
    }
}
