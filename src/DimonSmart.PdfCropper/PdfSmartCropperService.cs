using System.Threading;
using System.Threading.Tasks;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Default implementation of <see cref="IPdfCropper"/> based on <see cref="PdfSmartCropper"/>.
/// </summary>
public sealed class PdfSmartCropperService : IPdfCropper
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
