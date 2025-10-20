using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        var inputList = inputs.ToList();

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
        ct.ThrowIfCancellationRequested();

        var totalStopwatch = Stopwatch.StartNew();
        var operationName = inputs.Count == 1 ? "PDF cropping" : "PDF merging";
        var totalInputSize = inputs.Sum(input => input.LongLength);
        
        logger.LogInfo(inputs.Count == 1 
            ? $"Input PDF size: {inputs[0].Length:N0} bytes"
            : $"Starting PDF merging for {inputs.Count} document(s)");

        try
        {
            var resultBytes = inputs.Count == 1
                ? ProcessSingleDocument(inputs[0], cropSettings, optimizationSettings, logger, ct)
                : ProcessMultipleDocuments(inputs, cropSettings, optimizationSettings, logger, ct);

            totalStopwatch.Stop();
            logger.LogInfo($"{operationName} completed successfully");
            logger.LogInfo($"Total processing time: {FormatElapsed(totalStopwatch.Elapsed)}");

            var finalResult = ApplyXmpOptimizations(resultBytes, optimizationSettings, logger);
            
            LogSizeComparison(totalInputSize, finalResult.Length, logger);

            return finalResult;
        }
        catch (OperationCanceledException)
        {
            HandleCancellation(logger, $"{operationName} cancelled");
            throw;
        }
        catch (Exception ex)
        {
            HandleProcessingException(ex, logger);
            throw;
        }
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

    private static byte[] ProcessSingleDocument(
        byte[] inputPdf,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger logger,
        CancellationToken ct)
    {
        using var inputStream = new MemoryStream(inputPdf, writable: false);
        using var outputStream = new MemoryStream();
        
        using var reader = new PdfReader(inputStream, new ReaderProperties());
        using var writer = CreatePdfWriter(outputStream, optimizationSettings);
        using var pdfDocument = new PdfDocument(reader, writer);

        CropPages(pdfDocument, inputPdf, cropSettings, logger, ct);
        ApplyFinalOptimizations(pdfDocument, optimizationSettings);
        pdfDocument.Close();

        return outputStream.ToArray();
    }

    private static byte[] ProcessMultipleDocuments(
        IReadOnlyList<byte[]> inputs,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger logger,
        CancellationToken ct)
    {
        using var outputStream = new MemoryStream();
        using var writer = CreatePdfWriter(outputStream, optimizationSettings);
        using var outputDocument = new PdfDocument(writer);
        
        var merger = new PdfMerger(outputDocument);

        foreach (var input in inputs)
        {
            ct.ThrowIfCancellationRequested();
            
            var croppedBytes = CropWithoutFinalOptimizations(input, cropSettings, logger, ct);
            
            using var croppedStream = new MemoryStream(croppedBytes, writable: false);
            using var reader = new PdfReader(croppedStream, new ReaderProperties());
            using var croppedDocument = new PdfDocument(reader);

            var existingPageCount = outputDocument.GetNumberOfPages();
            var pageCount = croppedDocument.GetNumberOfPages();

            merger.Merge(croppedDocument, 1, pageCount);

            CopyPageBoxes(outputDocument, croppedDocument, existingPageCount, pageCount);
        }

        ApplyFinalOptimizations(outputDocument, optimizationSettings);
        outputDocument.Close();

        return outputStream.ToArray();
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
        using var writer = CreatePdfWriter(outputStream, PdfOptimizationSettings.Default);

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

        var shouldDetectRepeatedObjects =
            cropSettings.Method == CropMethod.ContentBased &&
            cropSettings.DetectRepeatedObjects &&
            pageCount >= cropSettings.RepeatedObjectMinimumPageCount;

        var repeatedDetectionAnalyses = shouldDetectRepeatedObjects
            ? new ContentBasedCroppingStrategy.PageContentAnalysis?[pageCount]
            : null;
        var pageDurations = new TimeSpan[pageCount];
        var skippedPages = new bool[pageCount];

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
                var elapsed = pageStopwatch.Elapsed;
                pageDurations[pageIndex - 1] = elapsed;
                logger.LogInfo($"Page {pageIndex}: Processing time = {FormatElapsed(elapsed)}");
                skippedPages[pageIndex - 1] = true;
                continue;
            }

            if (shouldDetectRepeatedObjects)
            {
                repeatedDetectionAnalyses![pageIndex - 1] = ContentBasedCroppingStrategy.AnalyzePage(
                    page,
                    cropSettings.ExcludeEdgeTouchingObjects,
                    cropSettings.EdgeExclusionTolerance,
                    ct);
            }

            pageStopwatch.Stop();
            pageDurations[pageIndex - 1] = pageStopwatch.Elapsed;
        }

        IReadOnlySet<ContentBasedCroppingStrategy.ContentObjectKey>? repeatedObjects = null;
        if (shouldDetectRepeatedObjects)
        {
            var detected = RepeatedContentDetector.Detect(
                repeatedDetectionAnalyses!,
                cropSettings.RepeatedObjectOccurrenceThreshold,
                ct);
            if (detected.Count > 0)
            {
                repeatedObjects = detected;
                var analyzedPages = repeatedDetectionAnalyses!.Count(static analysis => analysis != null);
                logger.LogInfo($"Identified {detected.Count} repeated content object(s) across {analyzedPages} analyzed page(s)");
            }
        }

        for (var pageIndex = 1; pageIndex <= pageCount; pageIndex++)
        {
            if (skippedPages[pageIndex - 1])
            {
                continue;
            }

            ct.ThrowIfCancellationRequested();
            var page = pdfDocument.GetPage(pageIndex);
            var pageStopwatch = Stopwatch.StartNew();
            var pageSize = page.GetPageSize();

            Rectangle? cropRectangle = cropSettings.Method switch
            {
                CropMethod.ContentBased => null,
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

            if (cropSettings.Method == CropMethod.ContentBased)
            {
                var analysis = ContentBasedCroppingStrategy.AnalyzePage(
                    page,
                    cropSettings.ExcludeEdgeTouchingObjects,
                    cropSettings.EdgeExclusionTolerance,
                    ct,
                    repeatedObjects);
                var bounds = ContentBasedCroppingStrategy.CalculateBounds(analysis);
                if (bounds.HasValue)
                {
                    logger.LogInfo($"Page {pageIndex}: Content bounds = ({bounds.Value.MinX:F2}, {bounds.Value.MinY:F2}) to ({bounds.Value.MaxX:F2}, {bounds.Value.MaxY:F2})");
                    cropRectangle = bounds.Value.ToRectangle(pageSize, cropSettings.Margin);
                }
            }

            if (cropRectangle == null)
            {
                logger.LogWarning($"Page {pageIndex}: No crop applied (no content bounds found)");
                var totalTime = pageDurations[pageIndex - 1] + pageStopwatch.Elapsed;
                logger.LogInfo($"Page {pageIndex}: Processing time = {FormatElapsed(totalTime)}");
                continue;
            }

            logger.LogInfo($"Page {pageIndex}: Crop box = ({cropRectangle.GetLeft():F2}, {cropRectangle.GetBottom():F2}, {cropRectangle.GetWidth():F2}, {cropRectangle.GetHeight():F2})");

            page.SetCropBox(cropRectangle);
            page.SetTrimBox(cropRectangle);

            logger.LogInfo($"Page {pageIndex}: Cropped size = {cropRectangle.GetWidth():F2} x {cropRectangle.GetHeight():F2} pts");

            pageStopwatch.Stop();
            var totalDuration = pageDurations[pageIndex - 1] + pageStopwatch.Elapsed;
            logger.LogInfo($"Page {pageIndex}: Processing time = {FormatElapsed(totalDuration)}");
        }
    }

    private static void ApplyFinalOptimizations(PdfDocument pdfDocument, PdfOptimizationSettings optimizationSettings)
    {
        PdfDocumentInfoCleaner.Apply(pdfDocument, optimizationSettings);

        if (optimizationSettings.RemoveEmbeddedStandardFonts)
        {
            PdfStandardFontCleaner.RemoveEmbeddedStandardFonts(pdfDocument);
        }

        if (ShouldRecompressDocumentStreams(optimizationSettings))
        {
            var targetCompressionLevel = optimizationSettings.CompressionLevel ?? CompressionConstants.BEST_COMPRESSION;
            RecompressDocumentStreams(pdfDocument, targetCompressionLevel);
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

    private static bool ShouldRecompressDocumentStreams(PdfOptimizationSettings optimizationSettings)
    {
        return optimizationSettings.CompressionLevel.HasValue || optimizationSettings.EnableFullCompression;
    }

    private static void RecompressDocumentStreams(PdfDocument pdfDocument, int compressionLevel)
    {
        var objectCount = pdfDocument.GetNumberOfPdfObjects();

        for (var index = 1; index <= objectCount; index++)
        {
            var pdfObject = pdfDocument.GetPdfObject(index);
            if (pdfObject is not PdfStream stream)
            {
                continue;
            }

            if (stream.IsFlushed())
            {
                continue;
            }

            var subtype = stream.GetAsName(PdfName.Subtype);
            if (PdfName.Image.Equals(subtype))
            {
                continue;
            }

            try
            {
                var decodedBytes = stream.GetBytes(true);
                if (decodedBytes is null)
                {
                    continue;
                }

                stream.Remove(PdfName.Filter);
                stream.Remove(PdfName.DecodeParms);
                stream.SetData(decodedBytes);
                stream.SetCompressionLevel(compressionLevel);
            }
            catch
            {
                // Skip streams that cannot be decoded.
            }
        }
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

    private static void HandleCancellation(IPdfCropLogger logger, string operationName)
    {
        logger.LogWarning(operationName);
    }

    private static void LogSizeComparison(long originalSize, long newSize, IPdfCropLogger logger)
    {
        var sizeReduction = originalSize - newSize;
        var percentReduction = originalSize > 0 ? (double)sizeReduction / originalSize * 100 : 0;

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

    private static PdfWriter CreatePdfWriter(MemoryStream outputStream, PdfOptimizationSettings optimizationSettings)
    {
        var writerProps = CreateWriterProperties(optimizationSettings);
        var writer = new PdfWriter(outputStream, writerProps);
        
        if (optimizationSettings.EnableSmartMode)
        {
            writer.SetSmartMode(true);
        }
        
        return writer;
    }

    private static byte[] ApplyXmpOptimizations(byte[] inputBytes, PdfOptimizationSettings optimizationSettings, IPdfCropLogger logger)
    {
        logger.LogInfo($"Output PDF size before final optimization: {inputBytes.Length:N0} bytes");

        var resultBytes = optimizationSettings.RemoveXmpMetadata
            ? PdfXmpCleaner.RemoveXmpMetadata(inputBytes, optimizationSettings)
            : inputBytes;

        if (optimizationSettings.RemoveXmpMetadata)
        {
            logger.LogInfo($"Output PDF size after XMP removal: {resultBytes.Length:N0} bytes");
        }

        return resultBytes;
    }

    private static void CopyPageBoxes(PdfDocument targetDocument, PdfDocument sourceDocument, int targetStartIndex, int pageCount)
    {
        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            var targetPage = targetDocument.GetPage(targetStartIndex + pageIndex + 1);
            var sourcePage = sourceDocument.GetPage(pageIndex + 1);

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

    private static void HandleProcessingException(Exception ex, IPdfCropLogger logger)
    {
        switch (ex)
        {
            case BadPasswordException:
                logger.LogError($"PDF is encrypted: {ex.Message}");
                throw new PdfCropException(PdfCropErrorCode.EncryptedPdf, ex.Message, ex);
            
            case PdfCropException:
                return;
            
            case PdfException when IsEncryptionError((PdfException)ex):
                logger.LogError($"PDF encryption error: {ex.Message}");
                throw new PdfCropException(PdfCropErrorCode.EncryptedPdf, ex.Message, ex);
            
            case PdfException:
                logger.LogError($"Invalid PDF: {ex.Message}");
                throw new PdfCropException(PdfCropErrorCode.InvalidPdf, ex.Message, ex);
            
            case IOException:
                logger.LogError($"I/O error: {ex.Message}");
                throw new PdfCropException(PdfCropErrorCode.InvalidPdf, ex.Message, ex);
            
            default:
                logger.LogError($"Processing error: {ex.Message}");
                throw new PdfCropException(PdfCropErrorCode.ProcessingError, ex.Message, ex);
        }
    }

    private static WriterProperties CreateWriterProperties(PdfOptimizationSettings optimizationSettings)
    {
        var props = new WriterProperties();

        if (optimizationSettings.CompressionLevel.HasValue)
        {
            props.SetCompressionLevel(optimizationSettings.CompressionLevel.Value);
        }

        if (optimizationSettings.TargetPdfVersion != null)
        {
            props.SetPdfVersion(optimizationSettings.TargetPdfVersion.Value.ToPdfVersion());
        }

        if (optimizationSettings.EnableFullCompression)
        {
            props.SetFullCompressionMode(true);
        }

        return props;
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
