using iText.Kernel.Exceptions;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace PdfCropper;

public static class PdfSmartCropper
{
    private const float SafetyMargin = 0.5f;

    public static Task<byte[]> CropAsync(byte[] inputPdf, CancellationToken ct = default)
    {
        if (inputPdf is null)
        {
            throw new ArgumentNullException(nameof(inputPdf));
        }

        return Task.Run(() => CropInternal(inputPdf, ct), ct);
    }

    private static byte[] CropInternal(byte[] inputPdf, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            using var inputStream = new MemoryStream(inputPdf, writable: false);
            using var outputStream = new MemoryStream();
            var readerProps = new ReaderProperties();

            using var reader = new PdfReader(inputStream, readerProps);
            using var writer = new PdfWriter(outputStream, new WriterProperties());
            using var pdfDocument = new PdfDocument(reader, writer);

            int pageCount = pdfDocument.GetNumberOfPages();
            for (int pageIndex = 1; pageIndex <= pageCount; pageIndex++)
            {
                ct.ThrowIfCancellationRequested();
                var page = pdfDocument.GetPage(pageIndex);

                if (IsPageEmpty(page, ct))
                {
                    continue;
                }

                var collector = new ContentBoundingBoxCollector(ct);
                var processor = new PdfCanvasProcessor(collector);
                processor.ProcessPageContent(page);

                var bounds = collector.Bounds;
                if (!bounds.HasValue)
                {
                    continue;
                }

                var cropRectangle = bounds.Value.ToRectangle(page.GetPageSize(), SafetyMargin);
                if (cropRectangle == null)
                {
                    continue;
                }

                page.SetCropBox(cropRectangle);
                page.SetTrimBox(cropRectangle);
            }

            pdfDocument.Close();
            return outputStream.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BadPasswordException ex)
        {
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
                throw new PdfCropException(PdfCropErrorCode.EncryptedPdf, ex.Message, ex);
            }

            throw new PdfCropException(PdfCropErrorCode.InvalidPdf, ex.Message, ex);
        }
        catch (IOException ex)
        {
            throw new PdfCropException(PdfCropErrorCode.InvalidPdf, ex.Message, ex);
        }
        catch (Exception ex)
        {
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

    private readonly struct BoundingBox
    {
        public BoundingBox(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public double MinX { get; }

        public double MinY { get; }

        public double MaxX { get; }

        public double MaxY { get; }

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

    private sealed class ContentBoundingBoxCollector : IEventListener
    {
        private static readonly ICollection<EventType> SupportedEvents = new[]
        {
            EventType.RENDER_TEXT,
            EventType.RENDER_IMAGE,
            EventType.RENDER_PATH
        };

        private readonly CancellationToken _ct;
        private double? _minX;
        private double? _minY;
        private double? _maxX;
        private double? _maxY;

        public ContentBoundingBoxCollector(CancellationToken ct)
        {
            _ct = ct;
        }

        public BoundingBox? Bounds => _minX.HasValue && _minY.HasValue && _maxX.HasValue && _maxY.HasValue
            ? new BoundingBox(_minX.Value, _minY.Value, _maxX.Value, _maxY.Value)
            : null;

        public void EventOccurred(IEventData data, EventType type)
        {
            _ct.ThrowIfCancellationRequested();

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
            IncludeLineEndpoints(info.GetAscentLine());
            IncludeLineEndpoints(info.GetDescentLine());

            foreach (var characterInfo in info.GetCharacterRenderInfos())
            {
                IncludeLineEndpoints(characterInfo.GetAscentLine());
                IncludeLineEndpoints(characterInfo.GetDescentLine());
            }
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

            IncludeTransformedPoint(matrix, 0, 0);
            IncludeTransformedPoint(matrix, width, 0);
            IncludeTransformedPoint(matrix, 0, height);
            IncludeTransformedPoint(matrix, width, height);
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

            foreach (var subpath in path.GetSubpaths())
            {
                var startPoint = subpath.GetStartPoint();
                if (startPoint != null)
                {
                    IncludePoint(TransformPoint(startPoint, matrix), strokeExpandX, strokeExpandY);
                }

                foreach (var segment in subpath.GetSegments())
                {
                    foreach (var point in segment.GetBasePoints())
                    {
                        IncludePoint(TransformPoint(point, matrix), strokeExpandX, strokeExpandY);
                    }
                }
            }
        }

        private void IncludeLineEndpoints(LineSegment? segment)
        {
            if (segment == null)
            {
                return;
            }

            IncludePoint(segment.GetStartPoint());
            IncludePoint(segment.GetEndPoint());
        }

        private void IncludeRectangle(Rectangle? rectangle)
        {
            if (rectangle == null)
            {
                return;
            }

            var normalized = NormalizeRectangle(rectangle);
            if (normalized == null)
            {
                return;
            }

            IncludePoint(new Vector(normalized.GetLeft(), normalized.GetBottom(), 1));
            IncludePoint(new Vector(normalized.GetRight(), normalized.GetTop(), 1));
        }

        private void IncludeTransformedPoint(Matrix matrix, double x, double y)
        {
            var vector = new Vector((float)x, (float)y, 1);
            IncludePoint(vector.Cross(matrix));
        }

        private static Rectangle? NormalizeRectangle(Rectangle rectangle)
        {
            var left = Math.Min(rectangle.GetLeft(), rectangle.GetRight());
            var right = Math.Max(rectangle.GetLeft(), rectangle.GetRight());
            var bottom = Math.Min(rectangle.GetBottom(), rectangle.GetTop());
            var top = Math.Max(rectangle.GetBottom(), rectangle.GetTop());

            var width = right - left;
            var height = top - bottom;

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            return new Rectangle((float)left, (float)bottom, (float)width, (float)height);
        }

        private void IncludePoint(Vector? point, double expandX = 0, double expandY = 0)
        {
            if (point == null)
            {
                return;
            }

            var x = point.Get(Vector.I1);
            var y = point.Get(Vector.I2);

            var minX = x - expandX;
            var minY = y - expandY;
            var maxX = x + expandX;
            var maxY = y + expandY;

            if (!_minX.HasValue || minX < _minX)
            {
                _minX = minX;
            }

            if (!_minY.HasValue || minY < _minY)
            {
                _minY = minY;
            }

            if (!_maxX.HasValue || maxX > _maxX)
            {
                _maxX = maxX;
            }

            if (!_maxY.HasValue || maxY > _maxY)
            {
                _maxY = maxY;
            }
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

        private static Vector TransformDisplacement(Matrix matrix, double x, double y)
        {
            var vector = new Vector((float)x, (float)y, 0);
            return vector.Cross(matrix);
        }
    }
}
