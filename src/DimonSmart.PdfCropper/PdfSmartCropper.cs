using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using iText.Kernel.Exceptions;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using DimonSmart.PdfCropper.PdfFontSubsetMerger;
using FontSubsetMerger = DimonSmart.PdfCropper.PdfFontSubsetMerger.PdfFontSubsetMerger;

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
    public static async Task<byte[]> CropAsync(
        byte[] inputPdf,
        CropSettings settings,
        IPdfCropLogger? logger = null,
        CancellationToken ct = default)
    {
        if (inputPdf is null)
        {
            throw new ArgumentNullException(nameof(inputPdf));
        }

        return await ProcessAsync(new[] { inputPdf }, settings, PdfOptimizationSettings.Default, logger, ct, "PDF processing")
            .ConfigureAwait(false);
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
    public static async Task<byte[]> CropAndMergeAsync(
        IEnumerable<byte[]> inputs,
        CropSettings cropSettings,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger? logger = null,
        CancellationToken ct = default)
    {
        var inputList = inputs.ToList();

        return await ProcessAsync(inputList, cropSettings, optimizationSettings, logger, ct, "PDF merging")
            .ConfigureAwait(false);
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
    public static async Task<byte[]> CropAsync(
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

        return await ProcessAsync(new[] { inputPdf }, cropSettings, optimizationSettings, logger, ct, "PDF processing")
            .ConfigureAwait(false);
    }

    private static async Task<byte[]> ProcessAsync(
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
        await LogOptimizationSettingsAsync(logger, optimizationSettings, operationDescription).ConfigureAwait(false);

        return await ExecuteAsync(inputs, cropSettings, optimizationSettings, logger, ct).ConfigureAwait(false);
    }

    private static async Task<byte[]> ExecuteAsync(
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
        
        var message = inputs.Count == 1 
            ? $"Input PDF size: {inputs[0].Length:N0} bytes"
            : $"Starting PDF merging for {inputs.Count} document(s)";
        await logger.LogInfoAsync(message).ConfigureAwait(false);
        await Task.Yield();

        try
        {
            var resultBytes = inputs.Count == 1
                ? await ProcessSingleDocumentAsync(inputs[0], cropSettings, optimizationSettings, logger, ct).ConfigureAwait(false)
                : await ProcessMultipleDocumentsAsync(inputs, cropSettings, optimizationSettings, logger, ct).ConfigureAwait(false);

            totalStopwatch.Stop();
            var completionMessage = $"{operationName} completed successfully";
            await logger.LogInfoAsync(completionMessage).ConfigureAwait(false);
            await Task.Yield();
            
            var timeMessage = $"Total processing time: {FormatElapsed(totalStopwatch.Elapsed)}";
            await logger.LogInfoAsync(timeMessage).ConfigureAwait(false);
            await Task.Yield();

            var finalResult = await ApplyXmpOptimizationsAsync(resultBytes, optimizationSettings, logger).ConfigureAwait(false);
            
            await LogSizeComparisonAsync(totalInputSize, finalResult.Length, logger).ConfigureAwait(false);

            return finalResult;
        }
        catch (OperationCanceledException)
        {
            await HandleCancellationAsync(logger, $"{operationName} cancelled").ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            await HandleProcessingExceptionAsync(ex, logger).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task LogOptimizationSettingsAsync(
        IPdfCropLogger logger,
        PdfOptimizationSettings optimizationSettings,
        string operationDescription)
    {
        await logger.LogInfoAsync($"Starting {operationDescription} with optimization settings:").ConfigureAwait(false);

        if (optimizationSettings.CompressionLevel.HasValue)
        {
            await logger.LogInfoAsync($"  Compression level: {optimizationSettings.CompressionLevel.Value}").ConfigureAwait(false);
        }
        else
        {
            await logger.LogInfoAsync("  Compression level: Default").ConfigureAwait(false);
        }

        if (optimizationSettings.TargetPdfVersion != null)
        {
            await logger.LogInfoAsync($"  Target PDF version: {optimizationSettings.TargetPdfVersion.Value.ToVersionString()}").ConfigureAwait(false);
        }
        else
        {
            await logger.LogInfoAsync("  Target PDF version: Original").ConfigureAwait(false);
        }

        await logger.LogInfoAsync($"  Full compression: {optimizationSettings.EnableFullCompression}").ConfigureAwait(false);
        await logger.LogInfoAsync($"  Smart mode: {optimizationSettings.EnableSmartMode}").ConfigureAwait(false);
        await logger.LogInfoAsync($"  Remove unused objects: {optimizationSettings.RemoveUnusedObjects}").ConfigureAwait(false);
        await logger.LogInfoAsync($"  Merge duplicate font subsets: {optimizationSettings.MergeDuplicateFontSubsets}").ConfigureAwait(false);
    }

    private static async Task<byte[]> ProcessSingleDocumentAsync(
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

        await CropPagesAsync(pdfDocument, inputPdf, cropSettings, logger, ct).ConfigureAwait(false);
        await ApplyFinalOptimizationsAsync(pdfDocument, optimizationSettings, logger).ConfigureAwait(false);
        pdfDocument.Close();

        return outputStream.ToArray();
    }

    private static async Task<byte[]> ProcessMultipleDocumentsAsync(
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

        var documentIndex = 0;
        foreach (var input in inputs)
        {
            ct.ThrowIfCancellationRequested();
            documentIndex++;
            
            var docMessage = $"Processing document {documentIndex}/{inputs.Count}";
            await logger.LogInfoAsync(docMessage).ConfigureAwait(false);
            await Task.Yield();
            
            var croppedBytes = await CropWithoutFinalOptimizationsAsync(input, cropSettings, logger, ct).ConfigureAwait(false);
            
            using var croppedStream = new MemoryStream(croppedBytes, writable: false);
            using var reader = new PdfReader(croppedStream, new ReaderProperties());
            using var croppedDocument = new PdfDocument(reader);

            var existingPageCount = outputDocument.GetNumberOfPages();
            var pageCount = croppedDocument.GetNumberOfPages();

            merger.Merge(croppedDocument, 1, pageCount);

            CopyPageBoxes(outputDocument, croppedDocument, existingPageCount, pageCount);
            
            await Task.Yield();
        }

        await ApplyFinalOptimizationsAsync(outputDocument, optimizationSettings, logger).ConfigureAwait(false);
        outputDocument.Close();

        return outputStream.ToArray();
    }

    private static async Task<byte[]> CropWithoutFinalOptimizationsAsync(
        byte[] inputPdf,
        CropSettings cropSettings,
        IPdfCropLogger logger,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await logger.LogInfoAsync($"Input PDF size: {inputPdf.Length:N0} bytes").ConfigureAwait(false);

        using var inputStream = new MemoryStream(inputPdf, writable: false);
        using var outputStream = new MemoryStream();
        var readerProps = new ReaderProperties();

        using var reader = new PdfReader(inputStream, readerProps);
        using var writer = CreatePdfWriter(outputStream, PdfOptimizationSettings.Default);

        using var pdfDocument = new PdfDocument(reader, writer);

        await CropPagesAsync(pdfDocument, inputPdf, cropSettings, logger, ct).ConfigureAwait(false);
        pdfDocument.Close();

        return outputStream.ToArray();
    }

    private static async Task CropPagesAsync(
        PdfDocument pdfDocument,
        byte[] inputPdf,
        CropSettings cropSettings,
        IPdfCropLogger logger,
        CancellationToken ct)
    {
        var pageCount = pdfDocument.GetNumberOfPages();
        var startMessage = $"Processing PDF with {pageCount} page(s) using {cropSettings.Method} method";
        await logger.LogInfoAsync(startMessage).ConfigureAwait(false);
        await Task.Yield();

        if (cropSettings.Method == CropMethod.ContentBased && cropSettings.ExcludeEdgeTouchingObjects)
        {
            await logger.LogInfoAsync($"Edge-touching content within {cropSettings.EdgeExclusionTolerance:F2} pt of the page boundary will be ignored during bounds detection").ConfigureAwait(false);
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

            var sizeMessage = $"Page {pageIndex}/{pageCount}: Original size = {pageSize.GetWidth():F2} x {pageSize.GetHeight():F2} pts";
            await logger.LogInfoAsync(sizeMessage).ConfigureAwait(false);
            
            await Task.Yield();

            if (IsPageEmpty(page, ct))
            {
                await logger.LogWarningAsync($"Page {pageIndex}: Skipped (empty page)").ConfigureAwait(false);
                pageStopwatch.Stop();
                var elapsed = pageStopwatch.Elapsed;
                pageDurations[pageIndex - 1] = elapsed;
                await logger.LogInfoAsync($"Page {pageIndex}: Processing time = {FormatElapsed(elapsed)}").ConfigureAwait(false);
                skippedPages[pageIndex - 1] = true;
                
                await Task.Yield();
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
            
            await Task.Yield();
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
                var detectionMessage = $"Identified {detected.Count} repeated content object(s) across {analyzedPages} analyzed page(s)";
                await logger.LogInfoAsync(detectionMessage).ConfigureAwait(false);
                await Task.Yield();
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
                    await BitmapBasedCroppingStrategy.CropPageAsync(
                        inputPdf,
                        pageIndex,
                        pageSize,
                        logger,
                        cropSettings.Margin,
                        ct).ConfigureAwait(false),
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
                    await logger.LogInfoAsync($"Page {pageIndex}: Content bounds = ({bounds.Value.MinX:F2}, {bounds.Value.MinY:F2}) to ({bounds.Value.MaxX:F2}, {bounds.Value.MaxY:F2})").ConfigureAwait(false);
                    cropRectangle = bounds.Value.ToRectangle(pageSize, cropSettings.Margin);
                }
            }

            if (cropRectangle == null)
            {
                await logger.LogWarningAsync($"Page {pageIndex}: No crop applied (no content bounds found)").ConfigureAwait(false);
                var totalTime = pageDurations[pageIndex - 1] + pageStopwatch.Elapsed;
                await logger.LogInfoAsync($"Page {pageIndex}: Processing time = {FormatElapsed(totalTime)}").ConfigureAwait(false);
                
                await Task.Yield();
                continue;
            }

            await logger.LogInfoAsync($"Page {pageIndex}: Crop box = ({cropRectangle.GetLeft():F2}, {cropRectangle.GetBottom():F2}, {cropRectangle.GetWidth():F2}, {cropRectangle.GetHeight():F2})").ConfigureAwait(false);

            page.SetCropBox(cropRectangle);
            page.SetTrimBox(cropRectangle);

            var croppedMessage = $"Page {pageIndex}: Cropped size = {cropRectangle.GetWidth():F2} x {cropRectangle.GetHeight():F2} pts";
            await logger.LogInfoAsync(croppedMessage).ConfigureAwait(false);

            pageStopwatch.Stop();
            var totalDuration = pageDurations[pageIndex - 1] + pageStopwatch.Elapsed;
            var timeMessage = $"Page {pageIndex}: Processing time = {FormatElapsed(totalDuration)}";
            await logger.LogInfoAsync(timeMessage).ConfigureAwait(false);
            
            await Task.Yield();
        }
    }

    private static async Task ApplyFinalOptimizationsAsync(
        PdfDocument pdfDocument,
        PdfOptimizationSettings optimizationSettings,
        IPdfCropLogger logger)
    {
        if (optimizationSettings.MergeDuplicateFontSubsets)
        {
            await FontSubsetMerger.MergeDuplicateSubsetsAsync(pdfDocument, FontSubsetMergeOptions.CreateDefault(), logger).ConfigureAwait(false);
        }

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

    private static async Task HandleCancellationAsync(IPdfCropLogger logger, string operationName)
    {
        await logger.LogWarningAsync(operationName).ConfigureAwait(false);
    }

    private static async Task LogSizeComparisonAsync(long originalSize, long newSize, IPdfCropLogger logger)
    {
        var sizeReduction = originalSize - newSize;
        var percentReduction = originalSize > 0 ? (double)sizeReduction / originalSize * 100 : 0;

        if (sizeReduction > 0)
        {
            await logger.LogInfoAsync($"Size reduction: {sizeReduction:N0} bytes ({percentReduction:F1}%)").ConfigureAwait(false);
        }
        else if (sizeReduction < 0)
        {
            await logger.LogInfoAsync($"Size increase: {-sizeReduction:N0} bytes ({-percentReduction:F1}%)").ConfigureAwait(false);
        }
        else
        {
            await logger.LogInfoAsync("No size change").ConfigureAwait(false);
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

    private static async Task<byte[]> ApplyXmpOptimizationsAsync(byte[] inputBytes, PdfOptimizationSettings optimizationSettings, IPdfCropLogger logger)
    {
        await logger.LogInfoAsync($"Output PDF size before final optimization: {inputBytes.Length:N0} bytes").ConfigureAwait(false);

        var resultBytes = optimizationSettings.RemoveXmpMetadata
            ? PdfXmpCleaner.RemoveXmpMetadata(inputBytes, optimizationSettings)
            : inputBytes;

        if (optimizationSettings.RemoveXmpMetadata)
        {
            await logger.LogInfoAsync($"Output PDF size after XMP removal: {resultBytes.Length:N0} bytes").ConfigureAwait(false);
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

    private static async Task HandleProcessingExceptionAsync(Exception ex, IPdfCropLogger logger)
    {
        switch (ex)
        {
            case BadPasswordException:
                await logger.LogErrorAsync($"PDF is encrypted: {ex.Message}").ConfigureAwait(false);
                throw new PdfCropException(PdfCropErrorCode.EncryptedPdf, ex.Message, ex);
            
            case PdfCropException:
                return;
            
            case PdfException when IsEncryptionError((PdfException)ex):
                await logger.LogErrorAsync($"PDF encryption error: {ex.Message}").ConfigureAwait(false);
                throw new PdfCropException(PdfCropErrorCode.EncryptedPdf, ex.Message, ex);
            
            case PdfException:
                await logger.LogErrorAsync($"Invalid PDF: {ex.Message}").ConfigureAwait(false);
                throw new PdfCropException(PdfCropErrorCode.InvalidPdf, ex.Message, ex);
            
            case IOException:
                await logger.LogErrorAsync($"I/O error: {ex.Message}").ConfigureAwait(false);
                throw new PdfCropException(PdfCropErrorCode.InvalidPdf, ex.Message, ex);
            
            default:
                await logger.LogErrorAsync($"Processing error: {ex.Message}").ConfigureAwait(false);
                throw new PdfCropException(PdfCropErrorCode.ProcessingError, ex.Message, ex);
        }
    }

    internal static WriterProperties CreateWriterProperties(PdfOptimizationSettings optimizationSettings)
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

    internal static async Task<WriterProperties> CreateWriterPropertiesAsync(PdfOptimizationSettings optimizationSettings, IPdfCropLogger? logger = null)
    {
        var props = new WriterProperties();

        if (optimizationSettings.CompressionLevel.HasValue)
        {
            var level = optimizationSettings.CompressionLevel.Value;
            props.SetCompressionLevel(level);
            if (logger != null)
                await logger.LogInfoAsync($"Setting compression level to: {level}").ConfigureAwait(false);
        }
        else
        {
            if (logger != null)
                await logger.LogInfoAsync("Using default compression level").ConfigureAwait(false);
        }

        if (optimizationSettings.TargetPdfVersion != null)
        {
            props.SetPdfVersion(optimizationSettings.TargetPdfVersion.Value.ToPdfVersion());
            if (logger != null)
                await logger.LogInfoAsync($"Setting target PDF version to: {optimizationSettings.TargetPdfVersion.Value.ToVersionString()}").ConfigureAwait(false);
        }
        else
        {
            if (logger != null)
                await logger.LogInfoAsync("Preserving original PDF version").ConfigureAwait(false);
        }

        if (optimizationSettings.EnableFullCompression)
        {
            props.SetFullCompressionMode(true);
            if (logger != null)
                await logger.LogInfoAsync("Full compression mode enabled").ConfigureAwait(false);
        }

        return props;
    }
}
