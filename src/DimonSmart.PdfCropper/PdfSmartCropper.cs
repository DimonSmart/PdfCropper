using System.Diagnostics;
using iText.Kernel.Exceptions;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Provides methods for intelligently cropping PDF documents to actual content bounds.
/// </summary>
public static class PdfSmartCropper
{

    /// <summary>
    /// Crops a PDF document using the specified method.
    /// </summary>
    /// <param name="inputPdf">The input PDF as a byte array.</param>
    /// <param name="method">The cropping method to use.</param>
    /// <param name="logger">Optional logger for cropping operations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cropped PDF as a byte array.</returns>
    public static Task<byte[]> CropAsync(
        byte[] inputPdf,
        CropMethod method = CropMethod.ContentBased,
        IPdfCropLogger? logger = null,
        CancellationToken ct = default)
    {
        return CropAsync(inputPdf, new CropSettings(method), logger, ct);
    }

    /// <summary>
    /// Crops a PDF document using the specified settings.
    /// </summary>
    /// <param name="inputPdf">The input PDF as a byte array.</param>
    /// <param name="settings">Cropping settings to apply.</param>
    /// <param name="logger">Optional logger for cropping operations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cropped PDF as a byte array.</returns>
    public static Task<byte[]> CropAsync(
        byte[] inputPdf,
        CropSettings settings,
        IPdfCropLogger? logger = null,
        CancellationToken ct = default)
    {
        if (inputPdf is null)
        {
            throw new ArgumentNullException(nameof(inputPdf));
        }

        logger ??= NullLogger.Instance;
        return Task.Run(() => CropInternal(inputPdf, settings, logger, ct), ct);
    }

    private static byte[] CropInternal(byte[] inputPdf, CropSettings settings, IPdfCropLogger logger, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var totalStopwatch = Stopwatch.StartNew();

        try
        {
            using var inputStream = new MemoryStream(inputPdf, writable: false);
            using var outputStream = new MemoryStream();
            var readerProps = new ReaderProperties();

            using var reader = new PdfReader(inputStream, readerProps);
            using var writer = new PdfWriter(outputStream, new WriterProperties());
            using var pdfDocument = new PdfDocument(reader, writer);

            var pageCount = pdfDocument.GetNumberOfPages();
            logger.LogInfo($"Processing PDF with {pageCount} page(s) using {settings.Method} method");

            if (settings.Method == CropMethod.ContentBased && settings.ExcludeEdgeTouchingObjects)
            {
                logger.LogInfo("Edge-touching content will be ignored during bounds detection");
            }

            for (var pageIndex = 1; pageIndex <= pageCount; pageIndex++)
            {
                ct.ThrowIfCancellationRequested();
                var page = pdfDocument.GetPage(pageIndex);
                var pageStopwatch = Stopwatch.StartNew();
                var pageSize = page.GetPageSize();

                logger.LogInfo($"Page {pageIndex}/{pageCount}: Original size = {pageSize.GetWidth():F2} x {pageSize.GetHeight():F2} pts");

                if (IsPageEmpty(page, ct))
                {
                    logger.LogWarning($"Page {pageIndex}: Skipped (empty page)");
                    pageStopwatch.Stop();
                    logger.LogInfo($"Page {pageIndex}: Processing time = {FormatElapsed(pageStopwatch.Elapsed)}");
                    continue;
                }

                Rectangle? cropRectangle;
                switch (settings.Method)
                {
                    case CropMethod.ContentBased:
                        cropRectangle = ContentBasedCroppingStrategy.CropPage(page, logger, pageIndex, settings.ExcludeEdgeTouchingObjects, settings.Margin, ct);
                        break;

                    case CropMethod.BitmapBased:
                        if (BitmapBasedCroppingStrategy.IsSupportedOnCurrentPlatform())
                        {
#pragma warning disable CA1416 // Validate platform compatibility
                            cropRectangle = BitmapBasedCroppingStrategy.CropPage(inputPdf, pageIndex, pageSize, logger, settings.Margin, ct);
#pragma warning restore CA1416 // Validate platform compatibility
                        }
                        else
                        {
                            throw new PdfCropException(
                                PdfCropErrorCode.ProcessingError,
                                "Bitmap-based cropping is not supported on this platform. Use ContentBased instead.");
                        }
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(settings.Method), settings.Method, "Unknown crop method");
                }

                if (cropRectangle == null)
                {
                    logger.LogWarning($"Page {pageIndex}: No crop applied (no content bounds found)");
                    pageStopwatch.Stop();
                    logger.LogInfo($"Page {pageIndex}: Processing time = {FormatElapsed(pageStopwatch.Elapsed)}");
                    continue;
                }

                logger.LogInfo($"Page {pageIndex}: Crop box = ({cropRectangle.GetLeft():F2}, {cropRectangle.GetBottom():F2}, {cropRectangle.GetWidth():F2}, {cropRectangle.GetHeight():F2})");

                page.SetCropBox(cropRectangle);
                page.SetTrimBox(cropRectangle);

                logger.LogInfo($"Page {pageIndex}: Cropped size = {cropRectangle.GetWidth():F2} x {cropRectangle.GetHeight():F2} pts");

                pageStopwatch.Stop();
                logger.LogInfo($"Page {pageIndex}: Processing time = {FormatElapsed(pageStopwatch.Elapsed)}");
            }

            pdfDocument.Close();
            totalStopwatch.Stop();
            logger.LogInfo("PDF cropping completed successfully");
            logger.LogInfo($"Total processing time: {FormatElapsed(totalStopwatch.Elapsed)}");
            return outputStream.ToArray();
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("PDF cropping cancelled");
            throw;
        }
        catch (BadPasswordException ex)
        {
            logger.LogError($"PDF is encrypted: {ex.Message}");
            throw new PdfCropException(PdfCropErrorCode.EncryptedPdf, ex.Message, ex);
        }
        catch (PdfCropException)
        {
            throw;
        }
        catch (PdfException ex)
        {
            if (IsEncryptionError(ex))
            {
                logger.LogError($"PDF encryption error: {ex.Message}");
                throw new PdfCropException(PdfCropErrorCode.EncryptedPdf, ex.Message, ex);
            }

            logger.LogError($"Invalid PDF: {ex.Message}");
            throw new PdfCropException(PdfCropErrorCode.InvalidPdf, ex.Message, ex);
        }
        catch (IOException ex)
        {
            logger.LogError($"I/O error: {ex.Message}");
            throw new PdfCropException(PdfCropErrorCode.InvalidPdf, ex.Message, ex);
        }
        catch (Exception ex)
        {
            logger.LogError($"Processing error: {ex.Message}");
            throw new PdfCropException(PdfCropErrorCode.ProcessingError, ex.Message, ex);
        }
    }



    private static bool IsPageEmpty(PdfPage page, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var contentBytes = page.GetContentBytes();
        return contentBytes == null || contentBytes.Length == 0;
    }

    private static bool IsEncryptionError(PdfException exception)
    {
        var message = exception.Message ?? string.Empty;
        return message.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("password", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalMilliseconds < 1000
            ? $"{elapsed.TotalMilliseconds:F2} ms"
            : $"{elapsed.TotalSeconds:F2} s";
    }


}
