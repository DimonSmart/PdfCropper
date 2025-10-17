using System.Threading;
using System.Threading.Tasks;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Defines the contract for PDF cropping services.
/// </summary>
public interface IPdfCropper
{
    /// <summary>
    /// Crops the provided PDF using the supplied settings and optimization parameters.
    /// </summary>
    /// <param name="inputPdf">Input PDF bytes.</param>
    /// <param name="cropSettings">Cropping configuration.</param>
    /// <param name="optimizationSettings">Optimization parameters for the output.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Bytes of the cropped PDF.</returns>
    Task<byte[]> CropAsync(
        byte[] inputPdf,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger? logger = null,
        CancellationToken ct = default);
}
