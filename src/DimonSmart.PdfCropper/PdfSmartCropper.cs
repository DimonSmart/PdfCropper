using System;
using System.Diagnostics;
using System.IO;
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
        return CropAsync(inputPdf, settings, PdfOptimizationSettings.Default, logger, ct);
    }

    /// <summary>
    /// Crops a PDF document using the specified settings and optimization parameters.
    /// </summary>
    /// <param name="inputPdf">The input PDF as a byte array.</param>
    /// <param name="cropSettings">Cropping settings to apply.</param>
    /// <param name="optimizationSettings">Optimization settings that control PDF serialization.</param>
    /// <param name="logger">Optional logger for cropping operations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The cropped PDF as a byte array.</returns>
    public static Task<byte[]> CropAsync(
        byte[] inputPdf,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger? logger = null,
        CancellationToken ct = default)
    {
        if (inputPdf is null)
        {
            throw new ArgumentNullException(nameof(inputPdf));
        }

        if (optimizationSettings is null)
        {
            throw new ArgumentNullException(nameof(optimizationSettings));
        }

        logger ??= NullLogger.Instance;
        logger.LogInfo($"Starting PDF processing with optimization settings:");
        
        if (optimizationSettings.CompressionLevel.HasValue)
        {
            logger.LogInfo($"  Compression level: {optimizationSettings.CompressionLevel.Value}");
        }
        else
        {
            logger.LogInfo($"  Compression level: Default");
        }

        if (optimizationSettings.TargetPdfVersion != null)
        {
            logger.LogInfo($"  Target PDF version: {optimizationSettings.TargetPdfVersion.Value.ToVersionString()}");
        }
        else
        {
            logger.LogInfo("  Target PDF version: Original");
        }

        logger.LogInfo($"  Full compression: {optimizationSettings.EnableFullCompression}");
        logger.LogInfo($"  Smart mode: {optimizationSettings.EnableSmartMode}");
        logger.LogInfo($"  Remove unused objects: {optimizationSettings.RemoveUnusedObjects}");

        return Task.Run(() => CropInternal(inputPdf, cropSettings, optimizationSettings, logger, ct), ct);
    }

    private static byte[] CropInternal(
        byte[] inputPdf,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger logger,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var totalStopwatch = Stopwatch.StartNew();
        logger.LogInfo($"Input PDF size: {inputPdf.Length:N0} bytes");

        try
        {
            using var inputStream = new MemoryStream(inputPdf, writable: false);
            using var outputStream = new MemoryStream();
            var readerProps = new ReaderProperties();

            using var reader = new PdfReader(inputStream, readerProps);
            var writerProps = CreateWriterProperties(optimizationSettings, logger);
            using var writer = new PdfWriter(outputStream, writerProps);
            if (optimizationSettings.EnableSmartMode)
            {
                writer.SetSmartMode(true);
            }
            using var pdfDocument = new PdfDocument(reader, writer);

            var pageCount = pdfDocument.GetNumberOfPages();
            logger.LogInfo($"Processing PDF with {pageCount} page(s) using {cropSettings.Method} method");

            if (cropSettings.Method == CropMethod.ContentBased && cropSettings.ExcludeEdgeTouchingObjects)
            {
                logger.LogInfo($"Edge-touching content within {cropSettings.EdgeExclusionTolerance:F2} pt of the page boundary will be ignored during bounds detection");
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
                switch (cropSettings.Method)
                {
                    case CropMethod.ContentBased:
                        cropRectangle = ContentBasedCroppingStrategy.CropPage(
                            page,
                            logger,
                            pageIndex,
                            cropSettings.ExcludeEdgeTouchingObjects,
                            cropSettings.Margin,
                            cropSettings.EdgeExclusionTolerance,
                            ct);
                        break;

                    case CropMethod.BitmapBased:
                        if (BitmapBasedCroppingStrategy.IsSupportedOnCurrentPlatform())
                        {
#pragma warning disable CA1416 // Validate platform compatibility
                            cropRectangle = BitmapBasedCroppingStrategy.CropPage(
                                inputPdf,
                                pageIndex,
                                pageSize,
                                logger,
                                cropSettings.Margin,
                                ct);
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
                        throw new ArgumentOutOfRangeException(nameof(cropSettings.Method), cropSettings.Method, "Unknown crop method");
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

            PdfDocumentInfoCleaner.Apply(pdfDocument, optimizationSettings);

            if (optimizationSettings.RemoveEmbeddedStandardFonts)
            {
                PdfStandardFontCleaner.RemoveEmbeddedStandardFonts(pdfDocument);
            }

            if (optimizationSettings.RemoveUnusedObjects)
            {
                pdfDocument.SetFlushUnusedObjects(true);
            }

            pdfDocument.Close();
            totalStopwatch.Stop();
            logger.LogInfo("PDF cropping completed successfully");
            logger.LogInfo($"Total processing time: {FormatElapsed(totalStopwatch.Elapsed)}");

            var resultBytes = outputStream.ToArray();
            logger.LogInfo($"Output PDF size before final optimization: {resultBytes.Length:N0} bytes");
            
            if (optimizationSettings.RemoveXmpMetadata)
            {
                resultBytes = PdfXmpCleaner.RemoveXmpMetadata(resultBytes, optimizationSettings);
                logger.LogInfo($"Output PDF size after XMP removal: {resultBytes.Length:N0} bytes");
            }

            var sizeReduction = inputPdf.Length - resultBytes.Length;
            var percentReduction = inputPdf.Length > 0 ? (double)sizeReduction / inputPdf.Length * 100 : 0;
            
            if (sizeReduction > 0)
            {
                logger.LogInfo($"Size reduction: {sizeReduction:N0} bytes ({percentReduction:F1}%)");
            }
            else if (sizeReduction < 0)
            {
                logger.LogInfo($"Size increase: {-sizeReduction:N0} bytes ({-percentReduction:F1}%)");
            }
            else
            {
                logger.LogInfo("No size change");
            }

            return resultBytes;
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

    internal static WriterProperties CreateWriterProperties(PdfOptimizationSettings optimizationSettings, IPdfCropLogger? logger = null)
    {
        var props = new WriterProperties();

        if (optimizationSettings.CompressionLevel.HasValue)
        {
            var level = optimizationSettings.CompressionLevel.Value;
            props.SetCompressionLevel(level);
            logger?.LogInfo($"Setting compression level to: {level}");
        }
        else
        {
            logger?.LogInfo("Using default compression level");
        }

        if (optimizationSettings.TargetPdfVersion != null)
        {
            props.SetPdfVersion(optimizationSettings.TargetPdfVersion.Value.ToPdfVersion());
            logger?.LogInfo($"Setting target PDF version to: {optimizationSettings.TargetPdfVersion.Value.ToVersionString()}");
        }
        else
        {
            logger?.LogInfo("Preserving original PDF version");
        }

        if (optimizationSettings.EnableFullCompression)
        {
            props.SetFullCompressionMode(true);
            logger?.LogInfo("Full compression mode enabled");
        }

        return props;
    }
}
