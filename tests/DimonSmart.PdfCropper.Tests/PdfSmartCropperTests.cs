using System.Text;
using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using Xunit;
using DimonSmart.PdfCropper;

namespace DimonSmart.PdfCropper.Tests;

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

        var cropped = await PdfSmartCropper.CropAsync(input, CropSettings.Default);

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

        var cropped = await PdfSmartCropper.CropAsync(input, CropSettings.Default);

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

        var cropped = await PdfSmartCropper.CropAsync(input, CropSettings.Default);

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

        var cropped = await PdfSmartCropper.CropAsync(input, CropSettings.Default);

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

        var cropped = await PdfSmartCropper.CropAsync(input, CropSettings.Default);

        using var result = new PdfDocument(new PdfReader(new MemoryStream(cropped)));
        var crop = result.GetPage(1).GetCropBox();

        Assert.True(crop.GetWidth() < PageSize.A4.GetWidth());
        Assert.True(crop.GetHeight() < PageSize.A4.GetHeight());
        Assert.True(crop.GetLeft() <= 100 + 1);
        Assert.True(crop.GetBottom() <= 100 + 1);
    }

    [Fact]
    public async Task CropAsync_ThickOrthogonalLines_KeepsFullStroke()
    {
        const float lineWidth = 40f;
        const float verticalX = 250f;
        const float verticalStartY = 100f;
        const float verticalEndY = 400f;
        const float horizontalY = 260f;
        const float horizontalStartX = 120f;
        const float horizontalEndX = 380f;

        var input = CreatePdf(pdf =>
        {
            var verticalPage = pdf.AddNewPage(PageSize.A4);
            var verticalCanvas = new PdfCanvas(verticalPage);
            verticalCanvas.SetLineWidth(lineWidth);
            verticalCanvas.MoveTo(verticalX, verticalStartY);
            verticalCanvas.LineTo(verticalX, verticalEndY);
            verticalCanvas.Stroke();

            var horizontalPage = pdf.AddNewPage(PageSize.A4);
            var horizontalCanvas = new PdfCanvas(horizontalPage);
            horizontalCanvas.SetLineWidth(lineWidth);
            horizontalCanvas.MoveTo(horizontalStartX, horizontalY);
            horizontalCanvas.LineTo(horizontalEndX, horizontalY);
            horizontalCanvas.Stroke();
        });

        var cropped = await PdfSmartCropper.CropAsync(input, CropSettings.Default);

        using var result = new PdfDocument(new PdfReader(new MemoryStream(cropped)));

        var verticalPage = result.GetPage(1);
        var verticalCrop = verticalPage.GetCropBox();
        var verticalMedia = verticalPage.GetMediaBox();

        var horizontalPage = result.GetPage(2);
        var horizontalCrop = horizontalPage.GetCropBox();
        var horizontalMedia = horizontalPage.GetMediaBox();

        var halfWidth = lineWidth / 2f;

        Assert.True(verticalCrop.GetWidth() < verticalMedia.GetWidth());
        Assert.True(verticalCrop.GetWidth() >= lineWidth - 1);
        Assert.True(verticalCrop.GetLeft() <= verticalX - halfWidth);
        Assert.True(verticalCrop.GetRight() >= verticalX + halfWidth);
        Assert.True(verticalCrop.GetBottom() <= verticalStartY);
        Assert.True(verticalCrop.GetTop() >= verticalEndY);

        Assert.True(horizontalCrop.GetHeight() < horizontalMedia.GetHeight());
        Assert.True(horizontalCrop.GetHeight() >= lineWidth - 1);
        Assert.True(horizontalCrop.GetBottom() <= horizontalY - halfWidth);
        Assert.True(horizontalCrop.GetTop() >= horizontalY + halfWidth);
        Assert.True(horizontalCrop.GetLeft() <= horizontalStartX);
        Assert.True(horizontalCrop.GetRight() >= horizontalEndX);
    }

    [Fact]
    public async Task CropAsync_WithCustomMargin_AppliesCorrectMargin()
    {
        var customMargin = 5.0f;
        var input = CreatePdf(pdf =>
        {
            using var document = new Document(pdf, PageSize.A4);
            document.SetMargins(100, 100, 100, 100);
            document.Add(new Paragraph("Test content with custom margin").SetFontSize(12));
        });

        var settingsDefault = new CropSettings(CropMethod.ContentBased);
        var settingsCustom = new CropSettings(CropMethod.ContentBased, margin: customMargin);

        var croppedDefault = await PdfSmartCropper.CropAsync(input, settingsDefault);
        var croppedCustom = await PdfSmartCropper.CropAsync(input, settingsCustom);

        using var resultDefault = new PdfDocument(new PdfReader(new MemoryStream(croppedDefault)));
        using var resultCustom = new PdfDocument(new PdfReader(new MemoryStream(croppedCustom)));

        var cropDefault = resultDefault.GetPage(1).GetCropBox();
        var cropCustom = resultCustom.GetPage(1).GetCropBox();

        // Custom margin should result in a larger crop box (more padding around content)
        Assert.True(cropCustom.GetWidth() > cropDefault.GetWidth());
        Assert.True(cropCustom.GetHeight() > cropDefault.GetHeight());

        // The difference should be approximately 2 * (custom margin - default margin) for each dimension
        var defaultMargin = CropSettings.Default.Margins.Left;
        var expectedWidthDiff = 2 * (customMargin - defaultMargin);
        var expectedHeightDiff = 2 * (customMargin - defaultMargin);

        var actualWidthDiff = cropCustom.GetWidth() - cropDefault.GetWidth();
        var actualHeightDiff = cropCustom.GetHeight() - cropDefault.GetHeight();

        Assert.True(Math.Abs(actualWidthDiff - expectedWidthDiff) < 1.0f);
        Assert.True(Math.Abs(actualHeightDiff - expectedHeightDiff) < 1.0f);
    }

    [Fact]
    public async Task CropAsync_WithPerSideMargins_AppliesEachSide()
    {
        var margins = new CropMargins(left: 6.0f, bottom: 2.5f, right: 9.0f, top: 4.0f);
        var input = CreatePdf(pdf =>
        {
            using var document = new Document(pdf, PageSize.A4);
            document.SetMargins(100, 100, 100, 100);
            document.Add(new Paragraph("Test content with per-side margins").SetFontSize(12));
        });

        var settingsDefault = new CropSettings(CropMethod.ContentBased);
        var settingsCustom = new CropSettings(CropMethod.ContentBased, margins: margins);

        var croppedDefault = await PdfSmartCropper.CropAsync(input, settingsDefault);
        var croppedCustom = await PdfSmartCropper.CropAsync(input, settingsCustom);

        using var resultDefault = new PdfDocument(new PdfReader(new MemoryStream(croppedDefault)));
        using var resultCustom = new PdfDocument(new PdfReader(new MemoryStream(croppedCustom)));

        var cropDefault = resultDefault.GetPage(1).GetCropBox();
        var cropCustom = resultCustom.GetPage(1).GetCropBox();

        var defaultMargin = CropSettings.Default.Margins.Left;
        var expectedLeftDiff = margins.Left - defaultMargin;
        var expectedBottomDiff = margins.Bottom - defaultMargin;
        var expectedRightDiff = margins.Right - defaultMargin;
        var expectedTopDiff = margins.Top - defaultMargin;

        var actualLeftDiff = cropDefault.GetLeft() - cropCustom.GetLeft();
        var actualBottomDiff = cropDefault.GetBottom() - cropCustom.GetBottom();
        var actualRightDiff = cropCustom.GetRight() - cropDefault.GetRight();
        var actualTopDiff = cropCustom.GetTop() - cropDefault.GetTop();

        Assert.True(Math.Abs(actualLeftDiff - expectedLeftDiff) < 1.0f);
        Assert.True(Math.Abs(actualBottomDiff - expectedBottomDiff) < 1.0f);
        Assert.True(Math.Abs(actualRightDiff - expectedRightDiff) < 1.0f);
        Assert.True(Math.Abs(actualTopDiff - expectedTopDiff) < 1.0f);
    }

    [Fact]
    public async Task CropAsync_LargeDocument_CompletesSuccessfully()
    {
        var input = CreatePdf(pdf =>
        {
            for (var i = 0; i < 1024; i++)
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

        var cropped = await PdfSmartCropper.CropAsync(input, CropSettings.Default);
        Assert.NotNull(cropped);
        Assert.True(cropped.Length > 0);
    }

    [Fact]
    public async Task CropAsync_MetadataCleanup_RemovesConfiguredEntries()
    {
        var input = CreatePdfWithMetadata();
        var cropSettings = new CropSettings(CropMethod.ContentBased);
        var optimizationSettings = new PdfOptimizationSettings(
            removeXmpMetadata: true,
            documentInfoKeysToRemove: new[] { "CustomKey" });

        var cropped = await PdfSmartCropper.CropAsync(input, cropSettings, optimizationSettings);

        using var result = new PdfDocument(new PdfReader(new MemoryStream(cropped)));

        Assert.True(string.IsNullOrEmpty(result.GetDocumentInfo().GetMoreInfo("CustomKey")));

        var metadataStream = result.GetCatalog().GetPdfObject().GetAsStream(PdfName.Metadata);
        if (metadataStream != null)
        {
            var metadataBytes = metadataStream.GetBytes(true);
            var metadataContent = Encoding.UTF8.GetString(metadataBytes);
            Assert.DoesNotContain("<Test>Value</Test>", metadataContent);
        }
    }

    [Fact]
    public async Task CropAsync_TargetPdfVersion_OverridesOutputCompatibility()
    {
        var input = CreatePdfWithVersion(PdfVersion.PDF_1_2);
        var cropSettings = new CropSettings(CropMethod.ContentBased);
        var optimizationSettings = new PdfOptimizationSettings(targetPdfVersion: PdfCompatibilityLevel.Pdf17);

        var cropped = await PdfSmartCropper.CropAsync(input, cropSettings, optimizationSettings);

        using var result = new PdfDocument(new PdfReader(new MemoryStream(cropped)));
        Assert.True(result.GetPdfVersion().Equals(PdfVersion.PDF_1_7));
    }

    [Fact]
    public async Task CropAsync_RemoveEmbeddedStandardFonts_DropsFontStreams()
    {
        var input = CreatePdf(pdf =>
        {
            var page = pdf.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(
                iText.IO.Font.Constants.StandardFonts.HELVETICA,
                iText.IO.Font.PdfEncodings.WINANSI);

            canvas.BeginText();
            canvas.SetFontAndSize(font, 12);
            canvas.MoveText(72, 720);
            canvas.ShowText("Embedded Helvetica");
            canvas.EndText();

            var resources = page.GetResources();
            var fonts = resources.GetResource(PdfName.Font) as PdfDictionary;
            if (fonts == null)
            {
                return;
            }

            foreach (var name in fonts.KeySet())
            {
                var fontDictionary = fonts.GetAsDictionary(name);
                if (fontDictionary == null)
                {
                    continue;
                }

                var descriptor = fontDictionary.GetAsDictionary(PdfName.FontDescriptor);
                if (descriptor == null)
                {
                    descriptor = new PdfDictionary();
                    fontDictionary.Put(PdfName.FontDescriptor, descriptor);
                }

                descriptor.Put(PdfName.FontName, fontDictionary.GetAsName(PdfName.BaseFont) ?? new PdfName("Helvetica"));
                descriptor.Put(PdfName.FontFile, new PdfStream(new byte[] { 0x00 }));
            }
        });

        var cropSettings = new CropSettings(CropMethod.ContentBased);
        var optimizationSettings = new PdfOptimizationSettings(removeEmbeddedStandardFonts: true);

        var cropped = await PdfSmartCropper.CropAsync(input, cropSettings, optimizationSettings);

        using var result = new PdfDocument(new PdfReader(new MemoryStream(cropped)));
        var page = result.GetPage(1);
        var fonts = page.GetResources().GetResource(PdfName.Font) as PdfDictionary;
        Assert.NotNull(fonts);

        foreach (var name in fonts.KeySet())
        {
            var fontDictionary = fonts.GetAsDictionary(name);
            var descriptor = fontDictionary?.GetAsDictionary(PdfName.FontDescriptor);
            if (descriptor == null)
            {
                continue;
            }

            Assert.Null(descriptor.Get(PdfName.FontFile));
            Assert.Null(descriptor.Get(PdfName.FontFile2));
            Assert.Null(descriptor.Get(PdfName.FontFile3));
        }
    }

    [Fact]
    public async Task CropAsync_EncryptedPdf_Throws()
    {
        var encrypted = CreateEncryptedPdf();

        var exception = await Assert.ThrowsAsync<PdfCropException>(() => PdfSmartCropper.CropAsync(encrypted, CropSettings.Default));
        Assert.Equal(PdfCropErrorCode.EncryptedPdf, exception.Code);
    }

    [Fact]
    public async Task CropAndMergeAsync_MergesInputsAndAppliesOptimizations()
    {
        var inputOne = CreatePdf(pdf =>
        {
            pdf.GetDocumentInfo().SetMoreInfo("CustomKey", "One");
            var metadataBytes = Encoding.UTF8.GetBytes("<meta>one</meta>");
            var metadataStream = new PdfStream(metadataBytes);
            metadataStream.Put(PdfName.Type, PdfName.Metadata);
            metadataStream.Put(PdfName.Subtype, PdfName.XML);
            pdf.GetCatalog().Put(PdfName.Metadata, metadataStream);

            var page = pdf.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
            canvas.BeginText();
            canvas.SetFontAndSize(font, 18);
            canvas.MoveText(120, 220);
            canvas.ShowText("First document");
            canvas.EndText();
        });

        var inputTwo = CreatePdf(pdf =>
        {
            pdf.GetDocumentInfo().SetMoreInfo("CustomKey", "Two");
            var metadataBytes = Encoding.UTF8.GetBytes("<meta>two</meta>");
            var metadataStream = new PdfStream(metadataBytes);
            metadataStream.Put(PdfName.Type, PdfName.Metadata);
            metadataStream.Put(PdfName.Subtype, PdfName.XML);
            pdf.GetCatalog().Put(PdfName.Metadata, metadataStream);

            var page = pdf.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
            canvas.BeginText();
            canvas.SetFontAndSize(font, 18);
            canvas.MoveText(360, 480);
            canvas.ShowText("Second document");
            canvas.EndText();
        });

        var cropSettings = new CropSettings(CropMethod.ContentBased);
        var optimizationSettings = new PdfOptimizationSettings(
            removeXmpMetadata: true,
            clearDocumentInfo: true,
            removeUnusedObjects: true,
            targetPdfVersion: PdfCompatibilityLevel.Pdf17);

        var merged = await PdfSmartCropper.CropAndMergeAsync(
            new[] { inputOne, inputTwo },
            cropSettings,
            optimizationSettings);

        using var result = new PdfDocument(new PdfReader(new MemoryStream(merged)));

        Assert.Equal(2, result.GetNumberOfPages());
        Assert.Equal(PdfVersion.PDF_1_7, result.GetPdfVersion());

        var firstPage = result.GetPage(1);
        var secondPage = result.GetPage(2);

        Assert.True(firstPage.GetCropBox().GetWidth() < firstPage.GetMediaBox().GetWidth());
        Assert.True(secondPage.GetCropBox().GetWidth() < secondPage.GetMediaBox().GetWidth());
        Assert.Equal(firstPage.GetCropBox().GetLeft(), firstPage.GetTrimBox().GetLeft(), Precision);
        Assert.Equal(secondPage.GetCropBox().GetLeft(), secondPage.GetTrimBox().GetLeft(), Precision);
        Assert.True(firstPage.GetCropBox().GetLeft() < secondPage.GetCropBox().GetLeft());

        Assert.True(string.IsNullOrEmpty(result.GetDocumentInfo().GetMoreInfo("CustomKey")));

        var metadata = result.GetCatalog().GetPdfObject().GetAsStream(PdfName.Metadata);
        Assert.Null(metadata);
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

    private static byte[] CreatePdfWithVersion(PdfVersion version)
    {
        using var stream = new MemoryStream();
        var writerProps = new WriterProperties().SetPdfVersion(version);

        using (var writer = new PdfWriter(stream, writerProps))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);
            canvas.MoveTo(50, 50);
            canvas.LineTo(200, 200);
            canvas.Stroke();
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

    private static byte[] CreatePdfWithMetadata()
    {
        using var stream = new MemoryStream();
        using (var writer = new PdfWriter(stream))
        using (var pdf = new PdfDocument(writer))
        {
            pdf.GetDocumentInfo().SetMoreInfo("CustomKey", "UnitTest");
            var metadataBytes = Encoding.UTF8.GetBytes("<xmpmeta><Test>Value</Test></xmpmeta>");
            var metadataStream = new PdfStream(metadataBytes);
            metadataStream.Put(PdfName.Type, PdfName.Metadata);
            metadataStream.Put(PdfName.Subtype, PdfName.XML);
            pdf.GetCatalog().Put(PdfName.Metadata, metadataStream);

            var page = pdf.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);
            canvas.MoveTo(50, 50);
            canvas.LineTo(200, 200);
            canvas.Stroke();
        }

        return stream.ToArray();
    }

    private static readonly byte[] SamplePng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP8z/C/HwAF/gL+uQ0nSAAAAABJRU5ErkJggg==");
}
