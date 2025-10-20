using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using iText.Kernel.Exceptions;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Utils;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Provides methods for intelligently cropping PDF documents to actual content bounds.
/// </summary>
public static class PdfSmartCropper
{

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

        return ProcessAsync(new[] { inputPdf }, settings, PdfOptimizationSettings.Default, logger, ct, "PDF processing");
    }

    /// <summary>
    /// Crops multiple PDF documents and merges them into a single output document using the specified settings and optimization parameters.
    /// </summary>
    /// <param name="inputs">The collection of input PDFs as byte arrays.</param>
    /// <param name="cropSettings">Cropping settings to apply to each document.</param>
    /// <param name="optimizationSettings">Optimization settings that control PDF serialization.</param>
    /// <param name="logger">Optional logger for cropping operations.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The merged cropped PDF as a byte array.</returns>
    public static Task<byte[]> CropAndMergeAsync(
        IEnumerable<byte[]> inputs,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger? logger = null,
        CancellationToken ct = default)
    {
        var inputList = MaterializeInputs(inputs, nameof(inputs));

        return ProcessAsync(inputList, cropSettings, optimizationSettings, logger, ct, "PDF merging");
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

        return ProcessAsync(new[] { inputPdf }, cropSettings, optimizationSettings, logger, ct, "PDF processing");
    }

    private static Task<byte[]> ProcessAsync(
        IReadOnlyList<byte[]> inputs,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger? logger,
        CancellationToken ct,
        string operationDescription)
    {
        if (optimizationSettings is null)
        {
            throw new ArgumentNullException(nameof(optimizationSettings));
        }

        logger ??= NullLogger.Instance;
        LogOptimizationSettings(logger, optimizationSettings, operationDescription);

        return Task.Run(() => Execute(inputs, cropSettings, optimizationSettings, logger, ct), ct);
    }

    private static byte[] Execute(
        IReadOnlyList<byte[]> inputs,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger logger,
        CancellationToken ct)
    {
        return inputs.Count == 1
            ? CropInternal(inputs[0], cropSettings, optimizationSettings, logger, ct)
            : CropAndMergeInternal(inputs, cropSettings, optimizationSettings, logger, ct);
    }

    private static IReadOnlyList<byte[]> MaterializeInputs(IEnumerable<byte[]> inputs, string parameterName)
    {
        if (inputs is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        var result = new List<byte[]>();

        foreach (var input in inputs)
        {
            if (input is null)
            {
                throw new ArgumentException("Input PDF cannot be null.", parameterName);
            }

            result.Add(input);
        }

        if (result.Count == 0)
        {
            throw new ArgumentException("At least one input PDF must be provided.", parameterName);
        }

        return result;
    }

    private static void LogOptimizationSettings(
        IPdfCropLogger logger,
        PdfOptimizationSettings optimizationSettings,
        string operationDescription)
    {
        logger.LogInfo($"Starting {operationDescription} with optimization settings:");

        if (optimizationSettings.CompressionLevel.HasValue)
        {
            logger.LogInfo($"  Compression level: {optimizationSettings.CompressionLevel.Value}");
        }
        else
        {
            logger.LogInfo("  Compression level: Default");
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

            CropPages(pdfDocument, inputPdf, cropSettings, logger, ct);

            ApplyFinalOptimizations(pdfDocument, optimizationSettings);

            pdfDocument.Close();
            totalStopwatch.Stop();
            logger.LogInfo("PDF cropping completed successfully");
            logger.LogInfo($"Total processing time: {FormatElapsed(totalStopwatch.Elapsed)}");

            var resultBytesBeforeXmp = outputStream.ToArray();
            logger.LogInfo($"Output PDF size before final optimization: {resultBytesBeforeXmp.Length:N0} bytes");

            var resultBytes = optimizationSettings.RemoveXmpMetadata
                ? PdfXmpCleaner.RemoveXmpMetadata(resultBytesBeforeXmp, optimizationSettings)
                : resultBytesBeforeXmp;

            if (optimizationSettings.RemoveXmpMetadata)
            {
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

    private static byte[] CropAndMergeInternal(
        IReadOnlyList<byte[]> inputs,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger logger,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var totalStopwatch = Stopwatch.StartNew();
        logger.LogInfo($"Starting PDF merging for {inputs.Count} document(s)");

        try
        {
            using var outputStream = new MemoryStream();
            var writerProps = CreateWriterProperties(optimizationSettings, logger);
            using var writer = new PdfWriter(outputStream, writerProps);
            if (optimizationSettings.EnableSmartMode)
            {
                writer.SetSmartMode(true);
            }

            using var outputDocument = new PdfDocument(writer);
            var merger = new PdfMerger(outputDocument);

            long totalInputSize = 0;

            foreach (var input in inputs)
            {
                ct.ThrowIfCancellationRequested();
                totalInputSize += input.LongLength;

                var croppedBytes = CropWithoutFinalOptimizations(input, cropSettings, logger, ct);

                using var croppedStream = new MemoryStream(croppedBytes, writable: false);
                using var reader = new PdfReader(croppedStream, new ReaderProperties());
                using var croppedDocument = new PdfDocument(reader);

                var existingPageCount = outputDocument.GetNumberOfPages();
                var pageCount = croppedDocument.GetNumberOfPages();

                merger.Merge(croppedDocument, 1, pageCount);

                for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    var targetPage = outputDocument.GetPage(existingPageCount + pageIndex + 1);
                    var sourcePage = croppedDocument.GetPage(pageIndex + 1);

                    var cropBox = sourcePage.GetCropBox();
                    if (cropBox != null)
                    {
                        targetPage.SetCropBox(new Rectangle(cropBox));
                    }

                    var trimBox = sourcePage.GetTrimBox();
                    if (trimBox != null)
                    {
                        targetPage.SetTrimBox(new Rectangle(trimBox));
                    }
                }
            }

            ApplyFinalOptimizations(outputDocument, optimizationSettings);

            outputDocument.Close();
            totalStopwatch.Stop();
            logger.LogInfo("PDF merging completed successfully");
            logger.LogInfo($"Total processing time: {FormatElapsed(totalStopwatch.Elapsed)}");

            var resultBytesBeforeXmp = outputStream.ToArray();
            logger.LogInfo($"Output PDF size before final optimization: {resultBytesBeforeXmp.Length:N0} bytes");

            var resultBytes = optimizationSettings.RemoveXmpMetadata
                ? PdfXmpCleaner.RemoveXmpMetadata(resultBytesBeforeXmp, optimizationSettings)
                : resultBytesBeforeXmp;

            if (optimizationSettings.RemoveXmpMetadata)
            {
                logger.LogInfo($"Output PDF size after XMP removal: {resultBytes.Length:N0} bytes");
            }

            if (totalInputSize > 0)
            {
                var sizeReduction = totalInputSize - resultBytes.LongLength;
                var percentReduction = totalInputSize > 0 ? (double)sizeReduction / totalInputSize * 100 : 0;

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
            }

            return resultBytes;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("PDF merging cancelled");
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

    private static byte[] CropWithoutFinalOptimizations(
        byte[] inputPdf,
        CropSettings cropSettings,
        IPdfCropLogger logger,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        logger.LogInfo($"Input PDF size: {inputPdf.Length:N0} bytes");

        using var inputStream = new MemoryStream(inputPdf, writable: false);
        using var outputStream = new MemoryStream();
        var readerProps = new ReaderProperties();

        using var reader = new PdfReader(inputStream, readerProps);
        var writerProps = CreateWriterProperties(PdfOptimizationSettings.Default, logger);
        using var writer = new PdfWriter(outputStream, writerProps);

        using var pdfDocument = new PdfDocument(reader, writer);

        CropPages(pdfDocument, inputPdf, cropSettings, logger, ct);

        pdfDocument.Close();

        return outputStream.ToArray();
    }

    private static void CropPages(
        PdfDocument pdfDocument,
        byte[] inputPdf,
        CropSettings cropSettings,
        IPdfCropLogger logger,
        CancellationToken ct)
    {
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

            Rectangle? cropRectangle = cropSettings.Method switch
            {
                CropMethod.ContentBased => ContentBasedCroppingStrategy.CropPage(
                    page,
                    logger,
                    pageIndex,
                    cropSettings.ExcludeEdgeTouchingObjects,
                    cropSettings.Margin,
                    cropSettings.EdgeExclusionTolerance,
                    ct),
                CropMethod.BitmapBased when BitmapBasedCroppingStrategy.IsSupportedOnCurrentPlatform() =>
#pragma warning disable CA1416 // Validate platform compatibility
                    BitmapBasedCroppingStrategy.CropPage(
                        inputPdf,
                        pageIndex,
                        pageSize,
                        logger,
                        cropSettings.Margin,
                        ct),
#pragma warning restore CA1416 // Validate platform compatibility
                CropMethod.BitmapBased => throw new PdfCropException(
                    PdfCropErrorCode.ProcessingError,
                    "Bitmap-based cropping is not supported on this platform. Use ContentBased instead."),
                _ => throw new ArgumentOutOfRangeException(nameof(cropSettings.Method), cropSettings.Method, "Unknown crop method"),
            };

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
    }

    private static void ApplyFinalOptimizations(PdfDocument pdfDocument, PdfOptimizationSettings optimizationSettings)
    {
        PdfDocumentInfoCleaner.Apply(pdfDocument, optimizationSettings);

        if (optimizationSettings.RemoveEmbeddedStandardFonts)
        {
            PdfStandardFontCleaner.RemoveEmbeddedStandardFonts(pdfDocument);
        }

        if (optimizationSettings.RemoveUnusedObjects)
        {
            pdfDocument.SetFlushUnusedObjects(true);
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
