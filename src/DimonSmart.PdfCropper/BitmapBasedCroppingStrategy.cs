using System.Runtime.Versioning;
using iText.Kernel.Geom;
using PDFtoImage;
using SkiaSharp;

namespace DimonSmart.PdfCropper;

/// <summary>
/// Bitmap-based cropping strategy that renders PDF pages to bitmaps and analyzes pixel content.
/// </summary>
internal static class BitmapBasedCroppingStrategy
{
    /// <summary>
    /// Validates if bitmap-based cropping is supported on the current platform.
    /// </summary>
    /// <returns>True if supported, false otherwise.</returns>
    public static bool IsSupportedOnCurrentPlatform()
    {
        return PlatformGuards.IsWindows || PlatformGuards.IsLinux || PlatformGuards.IsMacOS ||
               PlatformGuards.IsAndroid31Plus || PlatformGuards.IsIOS136Plus || PlatformGuards.IsMacCatalyst135Plus;
    }

    /// <summary>
    /// Crops a page by rendering it to a bitmap and analyzing pixel content.
    /// </summary>
    /// <param name="inputPdf">The entire PDF as a byte array.</param>
    /// <param name="pageIndex">The 1-based page index.</param>
    /// <param name="pageSize">The original page size.</param>
    /// <param name="logger">Logger for cropping operations.</param>
    /// <param name="margins">Margins to add around content bounds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The crop rectangle, or null if no content found.</returns>
    [SupportedOSPlatform("windows")]
    [SupportedOSPlatform("linux")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("android31.0")]
    [SupportedOSPlatform("ios13.6")]
    [SupportedOSPlatform("maccatalyst13.5")]
    public static async Task<Rectangle?> CropPageAsync(byte[] inputPdf, int pageIndex, Rectangle pageSize, IPdfCropLogger logger, CropMargins margins, CancellationToken ct)
    {
        const byte threshold = 250;

        try
        {
            await logger.LogInfoAsync($"Page {pageIndex}: Rendering to bitmap").ConfigureAwait(false);

            using var bitmap = Conversion.ToImage(inputPdf, page: pageIndex - 1);

            await logger.LogInfoAsync($"Page {pageIndex}: Bitmap size = {bitmap.Width} x {bitmap.Height} pixels").ConfigureAwait(false);

            var (minX, minY, maxX, maxY) = FindContentBoundsInBitmap(bitmap, threshold, ct);

            if (minX >= maxX || minY >= maxY)
            {
                await logger.LogWarningAsync($"Page {pageIndex}: No content found in bitmap").ConfigureAwait(false);
                return null;
            }

            await logger.LogInfoAsync($"Page {pageIndex}: Content pixels = ({minX}, {minY}) to ({maxX}, {maxY})").ConfigureAwait(false);
            var scaleX = pageSize.GetWidth() / bitmap.Width;
            var scaleY = pageSize.GetHeight() / bitmap.Height;

            var left = minX * scaleX - margins.Left;
            var bottom = pageSize.GetHeight() - (maxY * scaleY) - margins.Bottom;
            var right = maxX * scaleX + margins.Right;
            var top = pageSize.GetHeight() - (minY * scaleY) + margins.Top;

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

            await logger.LogInfoAsync($"Page {pageIndex}: PDF coordinates = ({left:F2}, {bottom:F2}, {width:F2}, {height:F2})").ConfigureAwait(false);

            return new Rectangle((float)left, (float)bottom, (float)width, (float)height);
        }
        catch (Exception ex)
        {
            await logger.LogErrorAsync($"Page {pageIndex}: Bitmap rendering failed: {ex.Message}").ConfigureAwait(false);
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
}
