using System.Diagnostics;
using iText.Kernel.Exceptions;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using PDFtoImage;
using SkiaSharp;

namespace PdfCropper;

/// <summary>
/// Provides methods for intelligently cropping PDF documents to actual content bounds.
/// </summary>
public static class PdfSmartCropper
{
    private const float SafetyMargin = 0.5f;

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

                var cropRectangle = settings.Method switch
                {
                    CropMethod.ContentBased => CropPageContentBased(page, logger, pageIndex, settings.ExcludeEdgeTouchingObjects, ct),
                    CropMethod.BitmapBased => CropPageBitmapBased(inputPdf, pageIndex, pageSize, logger, ct),
                    _ => throw new ArgumentOutOfRangeException(nameof(settings.Method), settings.Method, "Unknown crop method")
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

    private static Rectangle? CropPageContentBased(PdfPage page, IPdfCropLogger logger, int pageIndex, bool excludeEdgeTouchingObjects, CancellationToken ct)
    {
        var collector = new ContentBoundingBoxCollector(page.GetPageSize(), excludeEdgeTouchingObjects, ct);
        var processor = new PdfCanvasProcessor(collector);
        processor.ProcessPageContent(page);

        var bounds = collector.Bounds;
        if (!bounds.HasValue)
        {
            return null;
        }

        logger.LogInfo($"Page {pageIndex}: Content bounds = ({bounds.Value.MinX:F2}, {bounds.Value.MinY:F2}) to ({bounds.Value.MaxX:F2}, {bounds.Value.MaxY:F2})");

        return bounds.Value.ToRectangle(page.GetPageSize(), SafetyMargin);
    }

    private static Rectangle? CropPageBitmapBased(byte[] inputPdf, int pageIndex, Rectangle pageSize, IPdfCropLogger logger, CancellationToken ct)
    {
        const byte threshold = 250;

        try
        {
            logger.LogInfo($"Page {pageIndex}: Rendering to bitmap");

            using var bitmap = Conversion.ToImage(inputPdf, page: pageIndex - 1);

            logger.LogInfo($"Page {pageIndex}: Bitmap size = {bitmap.Width} x {bitmap.Height} pixels");

            var (minX, minY, maxX, maxY) = FindContentBoundsInBitmap(bitmap, threshold, ct);

            if (minX >= maxX || minY >= maxY)
            {
                logger.LogWarning($"Page {pageIndex}: No content found in bitmap");
                return null;
            }

            logger.LogInfo($"Page {pageIndex}: Content pixels = ({minX}, {minY}) to ({maxX}, {maxY})");
            var scaleX = pageSize.GetWidth() / bitmap.Width;
            var scaleY = pageSize.GetHeight() / bitmap.Height;

            var left = minX * scaleX - SafetyMargin;
            var bottom = pageSize.GetHeight() - (maxY * scaleY) - SafetyMargin;
            var right = maxX * scaleX + SafetyMargin;
            var top = pageSize.GetHeight() - (minY * scaleY) + SafetyMargin;

            left = Math.Max(pageSize.GetLeft(), left);
            bottom = Math.Max(pageSize.GetBottom(), bottom);
            right = Math.Min(pageSize.GetRight(), right);
            top = Math.Min(pageSize.GetTop(), top);

            var width = right - left;
            var height = top - bottom;

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            logger.LogInfo($"Page {pageIndex}: PDF coordinates = ({left:F2}, {bottom:F2}, {width:F2}, {height:F2})");

            return new Rectangle((float)left, (float)bottom, (float)width, (float)height);
        }
        catch (Exception ex)
        {
            logger.LogError($"Page {pageIndex}: Bitmap rendering failed: {ex.Message}");
            throw;
        }
    }

    private static (int minX, int minY, int maxX, int maxY) FindContentBoundsInBitmap(SKBitmap bitmap, byte threshold, CancellationToken ct)
    {
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = 0;
        var maxY = 0;

        var pixels = bitmap.Bytes;
        var bytesPerPixel = bitmap.BytesPerPixel;

        for (var y = 0; y < bitmap.Height; y++)
        {
            ct.ThrowIfCancellationRequested();

            for (var x = 0; x < bitmap.Width; x++)
            {
                var offset = (y * bitmap.RowBytes) + (x * bytesPerPixel);

                var b = pixels[offset];
                var g = pixels[offset + 1];
                var r = pixels[offset + 2];

                var luminance = (byte)(0.299 * r + 0.587 * g + 0.114 * b);

                if (luminance < threshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        return (minX, minY, maxX, maxY);
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

    private readonly struct BoundingBox(double minX, double minY, double maxX, double maxY)
    {
        public double MinX { get; } = minX;

        public double MinY { get; } = minY;

        public double MaxX { get; } = maxX;

        public double MaxY { get; } = maxY;

        public Rectangle? ToRectangle(Rectangle pageBox, float margin)
        {
            var left = (float)Math.Max(pageBox.GetLeft(), MinX - margin);
            var bottom = (float)Math.Max(pageBox.GetBottom(), MinY - margin);
            var right = (float)Math.Min(pageBox.GetRight(), MaxX + margin);
            var top = (float)Math.Min(pageBox.GetTop(), MaxY + margin);

            var width = right - left;
            var height = top - bottom;

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            return new Rectangle(left, bottom, width, height);
        }
    }

    private sealed class ContentBoundingBoxCollector(Rectangle pageBox, bool excludeEdgeTouchingObjects, CancellationToken ct) : IEventListener
    {
        private const double EdgeExclusionDelta = 1.0;
        private static readonly ICollection<EventType> SupportedEvents = new[]
        {
            EventType.RENDER_TEXT,
            EventType.RENDER_IMAGE,
            EventType.RENDER_PATH
        };
        private readonly Rectangle _pageBox = pageBox;
        private readonly bool _excludeEdgeTouchingObjects = excludeEdgeTouchingObjects;
        private double? _minX;
        private double? _minY;
        private double? _maxX;
        private double? _maxY;

        public BoundingBox? Bounds => _minX.HasValue && _minY.HasValue && _maxX.HasValue && _maxY.HasValue
            ? new BoundingBox(_minX.Value, _minY.Value, _maxX.Value, _maxY.Value)
            : null;

        public void EventOccurred(IEventData data, EventType type)
        {
            ct.ThrowIfCancellationRequested();

            switch (type)
            {
                case EventType.RENDER_TEXT:
                    HandleText((TextRenderInfo)data);
                    break;
                case EventType.RENDER_IMAGE:
                    HandleImage((ImageRenderInfo)data);
                    break;
                case EventType.RENDER_PATH:
                    HandlePath((PathRenderInfo)data);
                    break;
            }
        }

        public ICollection<EventType> GetSupportedEvents() => SupportedEvents;

        private void HandleText(TextRenderInfo info)
        {
            var builder = new BoundsBuilder();
            builder.Include(info.GetAscentLine());
            builder.Include(info.GetDescentLine());

            foreach (var characterInfo in info.GetCharacterRenderInfos())
            {
                builder.Include(characterInfo.GetAscentLine());
                builder.Include(characterInfo.GetDescentLine());
            }

            CommitBounds(builder, 0, 0);
        }

        private void HandleImage(ImageRenderInfo info)
        {
            var image = info.GetImage();
            var matrix = info.GetImageCtm();
            if (matrix == null)
            {
                return;
            }

            var width = image?.GetWidth() ?? 1;
            var height = image?.GetHeight() ?? 1;

            var builder = new BoundsBuilder();
            builder.Include(TransformPoint(0, 0, matrix));
            builder.Include(TransformPoint(width, 0, matrix));
            builder.Include(TransformPoint(0, height, matrix));
            builder.Include(TransformPoint(width, height, matrix));

            CommitBounds(builder, 0, 0);
        }

        private void HandlePath(PathRenderInfo info)
        {
            var path = info.GetPath();
            if (path == null)
            {
                return;
            }

            var matrix = info.GetCtm();
            var strokeExpandX = 0d;
            var strokeExpandY = 0d;

            if ((info.GetOperation() & PathRenderInfo.STROKE) != 0)
            {
                var lineWidth = info.GetLineWidth();
                if (lineWidth > 0)
                {
                    var halfWidth = lineWidth / 2d;
                    if (matrix == null)
                    {
                        strokeExpandX = halfWidth;
                        strokeExpandY = halfWidth;
                    }
                    else
                    {
                        var unitX = TransformDisplacement(matrix, 1, 0);
                        var unitY = TransformDisplacement(matrix, 0, 1);

                        var a = unitX.Get(Vector.I1);
                        var c = unitX.Get(Vector.I2);
                        var b = unitY.Get(Vector.I1);
                        var d = unitY.Get(Vector.I2);

                        strokeExpandX = halfWidth * Math.Sqrt(a * a + b * b);
                        strokeExpandY = halfWidth * Math.Sqrt(c * c + d * d);
                    }
                }
            }

            var builder = new BoundsBuilder();
            foreach (var subpath in path.GetSubpaths())
            {
                var startPoint = subpath.GetStartPoint();
                builder.Include(TransformPoint(startPoint, matrix));

                foreach (var segment in subpath.GetSegments())
                {
                    foreach (var point in segment.GetBasePoints())
                    {
                        builder.Include(TransformPoint(point, matrix));
                    }
                }
            }

            CommitBounds(builder, strokeExpandX, strokeExpandY);
        }

        private void CommitBounds(BoundsBuilder builder, double expandX, double expandY)
        {
            if (!builder.TryBuild(expandX, expandY, out var bounds))
            {
                return;
            }

            RegisterBounds(bounds);
        }

        private void RegisterBounds(BoundingBox bounds)
        {
            if (_excludeEdgeTouchingObjects && TouchesPageEdge(bounds))
            {
                return;
            }

            if (!_minX.HasValue || bounds.MinX < _minX)
            {
                _minX = bounds.MinX;
            }

            if (!_minY.HasValue || bounds.MinY < _minY)
            {
                _minY = bounds.MinY;
            }

            if (!_maxX.HasValue || bounds.MaxX > _maxX)
            {
                _maxX = bounds.MaxX;
            }

            if (!_maxY.HasValue || bounds.MaxY > _maxY)
            {
                _maxY = bounds.MaxY;
            }
        }

        private bool TouchesPageEdge(BoundingBox bounds)
        {
            var left = _pageBox.GetLeft();
            var right = _pageBox.GetRight();
            var bottom = _pageBox.GetBottom();
            var top = _pageBox.GetTop();

            return bounds.MinX <= left + EdgeExclusionDelta ||
                   bounds.MinY <= bottom + EdgeExclusionDelta ||
                   bounds.MaxX >= right - EdgeExclusionDelta ||
                   bounds.MaxY >= top - EdgeExclusionDelta;
        }

        private static Vector? TransformPoint(Point? point, Matrix? matrix)
        {
            if (point == null)
            {
                return null;
            }

            var vector = new Vector((float)point.GetX(), (float)point.GetY(), 1);

            return matrix == null ? vector : vector.Cross(matrix);
        }

        private static Vector? TransformPoint(double x, double y, Matrix matrix)
        {
            var vector = new Vector((float)x, (float)y, 1);
            return vector.Cross(matrix);
        }

        private static Vector TransformDisplacement(Matrix matrix, double x, double y)
        {
            var vector = new Vector((float)x, (float)y, 0);
            return vector.Cross(matrix);
        }

        private sealed class BoundsBuilder
        {
            private double? _minX;
            private double? _minY;
            private double? _maxX;
            private double? _maxY;

            public void Include(LineSegment? segment)
            {
                if (segment == null)
                {
                    return;
                }

                Include(segment.GetStartPoint());
                Include(segment.GetEndPoint());
            }

            public void Include(Vector? point)
            {
                if (point == null)
                {
                    return;
                }

                var x = point.Get(Vector.I1);
                var y = point.Get(Vector.I2);

                if (!_minX.HasValue || x < _minX)
                {
                    _minX = x;
                }

                if (!_minY.HasValue || y < _minY)
                {
                    _minY = y;
                }

                if (!_maxX.HasValue || x > _maxX)
                {
                    _maxX = x;
                }

                if (!_maxY.HasValue || y > _maxY)
                {
                    _maxY = y;
                }
            }

            public bool TryBuild(double expandX, double expandY, out BoundingBox bounds)
            {
                if (!_minX.HasValue || !_minY.HasValue || !_maxX.HasValue || !_maxY.HasValue)
                {
                    bounds = default;
                    return false;
                }

                bounds = new BoundingBox(
                    _minX.Value - expandX,
                    _minY.Value - expandY,
                    _maxX.Value + expandX,
                    _maxY.Value + expandY);
                return true;
            }
        }
    }
}
