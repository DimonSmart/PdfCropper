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
    /// <summary>
    /// Crops a page based on its content elements.
    /// </summary>
    /// <param name="page">The PDF page to crop.</param>
    /// <param name="logger">Logger for cropping operations.</param>
    /// <param name="pageIndex">The 1-based page index.</param>
    /// <param name="excludeEdgeTouchingObjects">Whether to exclude objects touching page edges.</param>
    /// <param name="margin">Margin to add around content bounds.</param>
    /// <param name="edgeExclusionTolerance">Tolerance for considering content as touching a page edge.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The crop rectangle, or null if no content found.</returns>
    public static Rectangle? CropPage(
        PdfPage page,
        IPdfCropLogger logger,
        int pageIndex,
        bool excludeEdgeTouchingObjects,
        float margin,
        float edgeExclusionTolerance,
        CancellationToken ct)
    {
        var collector = new ContentBoundingBoxCollector(page.GetPageSize(), excludeEdgeTouchingObjects, edgeExclusionTolerance, ct);
        var processor = new PdfCanvasProcessor(collector);
        processor.ProcessPageContent(page);

        var bounds = collector.Bounds;
        if (!bounds.HasValue)
        {
            return null;
        }

        logger.LogInfo($"Page {pageIndex}: Content bounds = ({bounds.Value.MinX:F2}, {bounds.Value.MinY:F2}) to ({bounds.Value.MaxX:F2}, {bounds.Value.MaxY:F2})");

        return bounds.Value.ToRectangle(page.GetPageSize(), margin);
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

    private sealed class ContentBoundingBoxCollector(Rectangle pageBox, bool excludeEdgeTouchingObjects, float edgeExclusionTolerance, CancellationToken ct) : IEventListener
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

            return bounds.MinX <= left + _edgeExclusionTolerance ||
                   bounds.MinY <= bottom + _edgeExclusionTolerance ||
                   bounds.MaxX >= right - _edgeExclusionTolerance ||
                   bounds.MaxY >= top - _edgeExclusionTolerance;
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