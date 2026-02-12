using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Content-based cropping strategy that analyzes PDF content elements to determine crop boundaries.
/// </summary>
internal static class ContentBasedCroppingStrategy
{
    private const double BoundsQuantizationScale = 100d;

    public static PageContentAnalysis AnalyzePage(
        PdfPage page,
        bool excludeEdgeTouchingObjects,
        float edgeExclusionTolerance,
        CancellationToken ct,
        IReadOnlySet<ContentObjectKey>? ignoredObjects = null)
    {
        var collector = new ContentBoundingBoxCollector(
            page.GetPageSize(),
            excludeEdgeTouchingObjects,
            edgeExclusionTolerance,
            ignoredObjects,
            ct);
        var processor = new PdfCanvasProcessor(collector);
        processor.ProcessPageContent(page);

        return new PageContentAnalysis(collector.Objects);
    }

    public static BoundingBox? CalculateBounds(PageContentAnalysis analysis)
    {
        BoundingBox? bounds = null;

        foreach (var detectedObject in analysis.Objects)
        {
            if (!detectedObject.Bounds.IsValid)
            {
                continue;
            }

            bounds = bounds.HasValue
                ? bounds.Value.Include(detectedObject.Bounds)
                : detectedObject.Bounds;
        }

        return bounds;
    }

    /// <summary>
    /// Crops a page based on its content elements.
    /// </summary>
    /// <param name="page">The PDF page to crop.</param>
    /// <param name="logger">Logger for cropping operations.</param>
    /// <param name="pageIndex">The 1-based page index.</param>
    /// <param name="excludeEdgeTouchingObjects">Whether to exclude objects touching page edges.</param>
    /// <param name="margins">Margins to add around content bounds.</param>
    /// <param name="edgeExclusionTolerance">Tolerance for considering content as touching a page edge.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="ignoredObjects">Set of content keys to exclude from bounds calculation.</param>
    /// <returns>The crop rectangle, or null if no content found.</returns>
    public static async Task<Rectangle?> CropPageAsync(
        PdfPage page,
        IPdfCropLogger logger,
        int pageIndex,
        bool excludeEdgeTouchingObjects,
        CropMargins margins,
        float edgeExclusionTolerance,
        CancellationToken ct,
        IReadOnlySet<ContentObjectKey>? ignoredObjects = null)
    {
        var analysis = AnalyzePage(page, excludeEdgeTouchingObjects, edgeExclusionTolerance, ct, ignoredObjects);
        var bounds = CalculateBounds(analysis);
        if (!bounds.HasValue)
        {
            return null;
        }

        await logger.LogInfoAsync($"Page {pageIndex}: Content bounds = ({bounds.Value.MinX:F2}, {bounds.Value.MinY:F2}) to ({bounds.Value.MaxX:F2}, {bounds.Value.MaxY:F2})").ConfigureAwait(false);

        return bounds.Value.ToRectangle(page.GetPageSize(), margins);
    }

    internal readonly struct BoundingBox(double minX, double minY, double maxX, double maxY)
    {
        public double MinX { get; } = minX;

        public double MinY { get; } = minY;

        public double MaxX { get; } = maxX;

        public double MaxY { get; } = maxY;

        public bool IsValid =>
            IsFiniteNumber(MinX) &&
            IsFiniteNumber(MinY) &&
            IsFiniteNumber(MaxX) &&
            IsFiniteNumber(MaxY) &&
            MaxX >= MinX &&
            MaxY >= MinY;

        public BoundingBox Include(BoundingBox other)
        { 
            if (!IsValid) { return other; }
            if (!other.IsValid) { return this; }
            
            var minX = Math.Min(MinX, other.MinX);
            var minY = Math.Min(MinY, other.MinY);
            var maxX = Math.Max(MaxX, other.MaxX);
            var maxY = Math.Max(MaxY, other.MaxY);
            return new BoundingBox(minX, minY, maxX, maxY);
        }


        public Rectangle? ToRectangle(Rectangle pageBox, CropMargins margins)
        {
            if (!IsValid)
            {
                return null;
            }

            var left = Math.Max(pageBox.GetLeft(), MinX - margins.Left);
            var bottom = Math.Max(pageBox.GetBottom(), MinY - margins.Bottom);
            var right = Math.Min(pageBox.GetRight(), MaxX + margins.Right);
            var top = Math.Min(pageBox.GetTop(), MaxY + margins.Top);

            if (!IsFiniteNumber(left) || !IsFiniteNumber(bottom) || !IsFiniteNumber(right) || !IsFiniteNumber(top))
            {
                return null;
            }

            var width = right - left;
            var height = top - bottom;

            if (!IsFiniteNumber(width) || !IsFiniteNumber(height) || width <= 0 || height <= 0)
            {
                return null;
            }

            return new Rectangle((float)left, (float)bottom, (float)width, (float)height);
        }
    }

    internal sealed class PageContentAnalysis
    {
        public PageContentAnalysis(IReadOnlyList<DetectedContentObject> objects)
        {
            Objects = objects;
        }

        public IReadOnlyList<DetectedContentObject> Objects { get; }
    }

    internal readonly record struct ContentObjectKey(ContentObjectType Type, QuantizedBounds Bounds, string? Text, long? ImageResourceId, int? PathHash);

    internal enum ContentObjectType
    {
        Text,
        Image,
        Path
    }

    internal readonly record struct QuantizedBounds(long MinX, long MinY, long MaxX, long MaxY)
    {
        public static QuantizedBounds FromBoundingBox(BoundingBox bounds)
        {
            return new QuantizedBounds(
                Quantize(bounds.MinX),
                Quantize(bounds.MinY),
                Quantize(bounds.MaxX),
                Quantize(bounds.MaxY));
        }
    }

    internal readonly struct DetectedContentObject
    {
        public DetectedContentObject(BoundingBox bounds, ContentObjectKey key)
        {
            Bounds = bounds;
            Key = key;
        }

        public BoundingBox Bounds { get; }

        public ContentObjectKey Key { get; }
    }

    private sealed class ContentBoundingBoxCollector(
        Rectangle pageBox,
        bool excludeEdgeTouchingObjects,
        float edgeExclusionTolerance,
        IReadOnlySet<ContentObjectKey>? ignoredObjects,
        CancellationToken ct) : IEventListener
    {
        private static readonly ICollection<EventType> SupportedEvents = new[]
        {
            EventType.RENDER_TEXT,
            EventType.RENDER_IMAGE,
            EventType.RENDER_PATH
        };
        private readonly Rectangle _pageBox = pageBox;
        private readonly bool _excludeEdgeTouchingObjects = excludeEdgeTouchingObjects;
        private readonly double _edgeExclusionTolerance = edgeExclusionTolerance;
        private readonly IReadOnlySet<ContentObjectKey>? _ignoredObjects = ignoredObjects;
        private readonly List<DetectedContentObject> _objects = new();

        public IReadOnlyList<DetectedContentObject> Objects => _objects;

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

            var text = info.GetText();
            CommitBounds(builder, 0, 0, new ContentObjectMetadata(ContentObjectType.Text, text, null, null));
        }

        private void HandleImage(ImageRenderInfo info)
        {
            var image = info.GetImage();
            var matrix = info.GetImageCtm();
            if (matrix == null)
            {
                return;
            }

            var builder = new BoundsBuilder();
            // Image CTM is already scaled to user space by iText; use unit square to avoid double-scaling.
            builder.Include(TransformPoint(0, 0, matrix));
            builder.Include(TransformPoint(1, 0, matrix));
            builder.Include(TransformPoint(0, 1, matrix));
            builder.Include(TransformPoint(1, 1, matrix));

            long? imageObjectId = null;
            if (image != null)
            {
                imageObjectId = image.GetPdfObject()?.GetIndirectReference()?.GetObjNumber();
            }

            CommitBounds(builder, 0, 0, new ContentObjectMetadata(ContentObjectType.Image, null, imageObjectId, null));
        }

        private void HandlePath(PathRenderInfo info)
        {
            var path = info.GetPath();
            if (path == null)
            {
                return;
            }

            var operation = info.GetOperation();
            // Ignore clip-only paths; they don't produce visible content and should not affect cropping.
            if ((operation & (PathRenderInfo.STROKE | PathRenderInfo.FILL)) == 0)
            {
                return;
            }

            var matrix = info.GetCtm();
            var strokeExpandX = 0d;
            var strokeExpandY = 0d;

            if ((operation & PathRenderInfo.STROKE) != 0)
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

            var pathHash = CalculatePathSignature(path, matrix, operation);
            CommitBounds(builder, strokeExpandX, strokeExpandY, new ContentObjectMetadata(ContentObjectType.Path, null, null, pathHash));
        }

        private void CommitBounds(BoundsBuilder builder, double expandX, double expandY, ContentObjectMetadata metadata)
        {
            if (!builder.TryBuild(expandX, expandY, out var bounds))
            {
                return;
            }

            RegisterBounds(bounds, metadata);
        }

        private void RegisterBounds(BoundingBox bounds, ContentObjectMetadata metadata)
        {
            if (!bounds.IsValid)
            {
                return;
            }

            if (_excludeEdgeTouchingObjects && TouchesPageEdge(bounds))
            {
                return;
            }

            var key = CreateKey(bounds, metadata);
            if (_ignoredObjects != null && _ignoredObjects.Contains(key))
            {
                return;
            }
            _objects.Add(new DetectedContentObject(bounds, key));
        }

        private bool TouchesPageEdge(BoundingBox bounds)
        {
            var left = _pageBox.GetLeft();
            var right = _pageBox.GetRight();
            var bottom = _pageBox.GetBottom();
            var top = _pageBox.GetTop();

            return bounds.MinX <= left + _edgeExclusionTolerance ||
                   bounds.MinY <= bottom + _edgeExclusionTolerance ||
                   bounds.MaxX >= right - _edgeExclusionTolerance ||
                   bounds.MaxY >= top - _edgeExclusionTolerance;
        }

        private static ContentObjectKey CreateKey(BoundingBox bounds, ContentObjectMetadata metadata)
        {
            var quantizedBounds = QuantizedBounds.FromBoundingBox(bounds);
            return new ContentObjectKey(metadata.Type, quantizedBounds, metadata.Text, metadata.ImageResourceId, metadata.PathHash);
        }

        private static int? CalculatePathSignature(iText.Kernel.Geom.Path path, Matrix? matrix, int operation)
        {
            if (path == null)
            {
                return null;
            }

            var hash = new HashCode();
            hash.Add(operation);

            foreach (var subpath in path.GetSubpaths())
            {
                var startPoint = TransformPoint(subpath.GetStartPoint(), matrix);
                if (startPoint != null)
                {
                    AddVector(ref hash, startPoint);
                }

                foreach (var segment in subpath.GetSegments())
                {
                    foreach (var point in segment.GetBasePoints())
                    {
                        var transformed = TransformPoint(point, matrix);
                        if (transformed != null)
                        {
                            AddVector(ref hash, transformed);
                        }
                    }
                }
            }

            return hash.ToHashCode();
        }

        private static void AddVector(ref HashCode hash, Vector vector)
        {
            hash.Add(Quantize(vector.Get(Vector.I1)));
            hash.Add(Quantize(vector.Get(Vector.I2)));
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

                if (!IsFiniteNumber(x) || !IsFiniteNumber(y))
                {
                    return;
                }

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

                if (!IsFiniteNumber(expandX) || !IsFiniteNumber(expandY))
                {
                    bounds = default;
                    return false;
                }

                var minX = _minX.Value - expandX;
                var minY = _minY.Value - expandY;
                var maxX = _maxX.Value + expandX;
                var maxY = _maxY.Value + expandY;

                if (!IsFiniteNumber(minX) || !IsFiniteNumber(minY) || !IsFiniteNumber(maxX) || !IsFiniteNumber(maxY))
                {
                    bounds = default;
                    return false;
                }

                if (maxX < minX || maxY < minY)
                {
                    bounds = default;
                    return false;
                }

                bounds = new BoundingBox(
                    minX,
                    minY,
                    maxX,
                    maxY);
                return true;
            }
        }
    }

    private static bool IsFiniteNumber(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static long Quantize(double value)
    {
        if (double.IsNaN(value))
        {
            return 0;
        }

        if (double.IsInfinity(value))
        {
            return value > 0 ? long.MaxValue : long.MinValue;
        }

        var scaled = value * BoundsQuantizationScale;
        if (scaled >= long.MaxValue)
        {
            return long.MaxValue;
        }

        if (scaled <= long.MinValue)
        {
            return long.MinValue;
        }

        return (long)Math.Round(scaled, MidpointRounding.AwayFromZero);
    }

    private readonly record struct ContentObjectMetadata(ContentObjectType Type, string? Text, long? ImageResourceId, int? PathHash);
}
