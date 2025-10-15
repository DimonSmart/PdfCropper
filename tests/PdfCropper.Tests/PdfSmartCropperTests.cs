using System.Text;
using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using Xunit;

namespace PdfCropper.Tests;

public class PdfSmartCropperTests
{
    private const int Precision = 2;

    [Fact]
    public async Task CropAsync_WithTextContent_AdjustsCropAndTrimBoxes()
    {
        var input = CreatePdf(pdf =>
        {
            using var document = new Document(pdf, PageSize.A4);
            document.SetMargins(72, 72, 72, 72);
            document.Add(new Paragraph("Hello cropped world").SetFontSize(24));
        });

        var cropped = await PdfSmartCropper.CropAsync(input);

        using var result = new PdfDocument(new PdfReader(new MemoryStream(cropped)));
        var page = result.GetPage(1);
        var crop = page.GetCropBox();
        var trim = page.GetTrimBox();
        var media = page.GetMediaBox();

        Assert.Equal(crop.GetLeft(), trim.GetLeft(), Precision);
        Assert.Equal(crop.GetBottom(), trim.GetBottom(), Precision);
        Assert.Equal(crop.GetWidth(), trim.GetWidth(), Precision);
        Assert.Equal(crop.GetHeight(), trim.GetHeight(), Precision);
        Assert.True(crop.GetWidth() < media.GetWidth());
        Assert.True(crop.GetHeight() < media.GetHeight());
    }

    [Fact]
    public async Task CropAsync_EmptyPage_LeavesBoxesUntouched()
    {
        var input = CreatePdf(pdf =>
        {
            var page = pdf.AddNewPage(PageSize.A4);
            page.SetCropBox(new Rectangle(0, 0, PageSize.A4.GetWidth(), PageSize.A4.GetHeight()));
            page.SetTrimBox(page.GetCropBox());
        });

        var cropped = await PdfSmartCropper.CropAsync(input);

        using var result = new PdfDocument(new PdfReader(new MemoryStream(cropped)));
        var page = result.GetPage(1);
        var crop = page.GetCropBox();
        var trim = page.GetTrimBox();
        var media = page.GetMediaBox();

        Assert.Equal(media.GetWidth(), crop.GetWidth(), Precision);
        Assert.Equal(media.GetHeight(), crop.GetHeight(), Precision);
        Assert.Equal(media.GetLeft(), crop.GetLeft(), Precision);
        Assert.Equal(media.GetBottom(), crop.GetBottom(), Precision);
        Assert.Equal(crop.GetWidth(), trim.GetWidth(), Precision);
        Assert.Equal(crop.GetHeight(), trim.GetHeight(), Precision);
    }

    [Fact]
    public async Task CropAsync_RotatedPage_CalculatesCorrectBoundingBox()
    {
        var input = CreatePdf(pdf =>
        {
            var page = pdf.AddNewPage(PageSize.A4);
            page.SetRotation(90);
            var canvas = new PdfCanvas(page);
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
            canvas.BeginText();
            canvas.SetFontAndSize(font, 18);
            canvas.MoveText(40, 40);
            canvas.ShowText("Rotated content");
            canvas.EndText();
        });

        var cropped = await PdfSmartCropper.CropAsync(input);

        using var result = new PdfDocument(new PdfReader(new MemoryStream(cropped)));
        var page = result.GetPage(1);
        var crop = page.GetCropBox();
        var media = page.GetMediaBox();

        Assert.True(crop.GetWidth() < media.GetWidth());
        Assert.True(crop.GetHeight() < media.GetHeight());
        Assert.True(crop.GetLeft() >= media.GetLeft());
        Assert.True(crop.GetBottom() >= media.GetBottom());
    }

    [Fact]
    public async Task CropAsync_ImageContent_CropsToImageBounds()
    {
        var input = CreatePdf(pdf =>
        {
            var page = pdf.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);
            var imageData = ImageDataFactory.Create(SamplePng);
            canvas.AddImageFittedIntoRectangle(imageData, new Rectangle(150, 200, 64, 64), false);
        });

        var cropped = await PdfSmartCropper.CropAsync(input);

        using var result = new PdfDocument(new PdfReader(new MemoryStream(cropped)));
        var crop = result.GetPage(1).GetCropBox();

        Assert.InRange(crop.GetWidth(), 50, 200);
        Assert.InRange(crop.GetHeight(), 50, 200);
    }

    [Fact]
    public async Task CropAsync_VectorPaths_IncludesStrokeBounds()
    {
        var input = CreatePdf(pdf =>
        {
            var page = pdf.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);
            canvas.SetLineWidth(1);
            canvas.MoveTo(100, 100);
            canvas.LineTo(300, 300);
            canvas.Stroke();
        });

        var cropped = await PdfSmartCropper.CropAsync(input);

        using var result = new PdfDocument(new PdfReader(new MemoryStream(cropped)));
        var crop = result.GetPage(1).GetCropBox();

        Assert.True(crop.GetWidth() < PageSize.A4.GetWidth());
        Assert.True(crop.GetHeight() < PageSize.A4.GetHeight());
        Assert.True(crop.GetLeft() <= 100 + 1);
        Assert.True(crop.GetBottom() <= 100 + 1);
    }

    [Fact]
    public async Task CropAsync_LargeDocument_CompletesSuccessfully()
    {
        var input = CreatePdf(pdf =>
        {
            for (int i = 0; i < 1024; i++)
            {
                var page = pdf.AddNewPage(PageSize.A4);
                var canvas = new PdfCanvas(page);
                canvas.BeginText();
                var font = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.COURIER);
                canvas.SetFontAndSize(font, 10);
                canvas.MoveText(36, 36);
                canvas.ShowText($"Page {i + 1}");
                canvas.EndText();
            }
        });

        var cropped = await PdfSmartCropper.CropAsync(input);
        Assert.NotNull(cropped);
        Assert.True(cropped.Length > 0);
    }

    [Fact]
    public async Task CropAsync_EncryptedPdf_Throws()
    {
        var encrypted = CreateEncryptedPdf();

        var exception = await Assert.ThrowsAsync<PdfCropException>(() => PdfSmartCropper.CropAsync(encrypted));
        Assert.Equal(PdfCropErrorCode.EncryptedPdf, exception.Code);
    }

    private static byte[] CreatePdf(Action<PdfDocument> builder)
    {
        using var stream = new MemoryStream();
        using (var writer = new PdfWriter(stream))
        using (var pdf = new PdfDocument(writer))
        {
            builder(pdf);
        }

        return stream.ToArray();
    }

    private static byte[] CreateEncryptedPdf()
    {
        using var stream = new MemoryStream();
        var writerProps = new WriterProperties();
        var ownerPassword = Encoding.UTF8.GetBytes("secret");
        writerProps.SetStandardEncryption(null, ownerPassword, EncryptionConstants.ALLOW_PRINTING, EncryptionConstants.ENCRYPTION_AES_128);

        using (var writer = new PdfWriter(stream, writerProps))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);
            canvas.MoveTo(50, 50);
            canvas.LineTo(150, 150);
            canvas.Stroke();
        }

        return stream.ToArray();
    }

    private static readonly byte[] SamplePng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP8z/C/HwAF/gL+uQ0nSAAAAABJRU5ErkJggg==");
}
