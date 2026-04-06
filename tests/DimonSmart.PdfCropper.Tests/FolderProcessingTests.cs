using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using iText.IO.Image;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Element;
using Xunit;
using Xunit.Abstractions;
using DimonSmart.PdfCropper;
using SystemPath = System.IO.Path;

namespace DimonSmart.PdfCropper.Tests;

public class FolderProcessingTests
{
    private readonly ITestOutputHelper _output;
    private const string DefaultTestFolder = @"C:\TestPdfs";
    private const float CropTolerance = 0.05f;

    public FolderProcessingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task ProcessPdfBatch_WithoutErrors()
    {
        string? temporaryFolder = null;

        try
        {
            var configuredFolderPath = Environment.GetEnvironmentVariable("PDF_TEST_FOLDER") ?? DefaultTestFolder;
            IReadOnlyDictionary<string, PdfBatchCase>? generatedCases = null;
            string[] files;

            if (Directory.Exists(configuredFolderPath))
            {
                files = Directory.GetFiles(configuredFolderPath, "*.pdf");
                if (files.Length > 0)
                {
                    _output.WriteLine($"Using external PDF batch from '{configuredFolderPath}'.");
                }
                else
                {
                    (temporaryFolder, generatedCases) = CreateRepresentativePdfBatch();
                    files = Directory.GetFiles(temporaryFolder, "*.pdf");
                    _output.WriteLine($"No PDFs found in '{configuredFolderPath}'. Using generated PDF batch in '{temporaryFolder}'.");
                }
            }
            else
            {
                (temporaryFolder, generatedCases) = CreateRepresentativePdfBatch();
                files = Directory.GetFiles(temporaryFolder, "*.pdf");
                _output.WriteLine($"Test folder '{configuredFolderPath}' not found. Using generated PDF batch in '{temporaryFolder}'.");
            }

            var contentSettings = new CropSettings(
                method: CropMethod.ContentBased,
                detectRepeatedObjects: true,
                repeatedObjectOccurrenceThreshold: 10,
                repeatedObjectMinimumPageCount: 3,
                margin: 10);

            var bitmapSettings = new CropSettings(
                method: CropMethod.BitmapBased,
                margin: 10);

            var optimizer = new PdfOptimizationSettings(
                compressionLevel: 9,
                removeUnusedObjects: true,
                mergeDuplicateFontSubsets: true);

            var logger = new XunitLogger(_output);

            foreach (var file in files.OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                PdfBatchCase? batchCase = null;
                if (generatedCases != null)
                {
                    generatedCases.TryGetValue(SystemPath.GetFileName(file), out batchCase);
                }

                await VerifyCropAsync(file, contentSettings, optimizer, logger, batchCase?.ExpectContentCropReduction);

                if (generatedCases != null && IsBitmapCroppingSupported())
                {
                    await VerifyCropAsync(file, bitmapSettings, optimizer, logger, batchCase?.ExpectBitmapCropReduction);
                }
            }
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryFolder) && Directory.Exists(temporaryFolder))
            {
                Directory.Delete(temporaryFolder, recursive: true);
            }
        }
    }

    private async Task VerifyCropAsync(
        string file,
        CropSettings settings,
        PdfOptimizationSettings optimizer,
        IPdfCropLogger logger,
        bool? expectCropReduction)
    {
        _output.WriteLine($"PROCESSING [{settings.Method}]: {file}");

        var bytes = await File.ReadAllBytesAsync(file);
        var result = await PdfSmartCropper.CropAsync(bytes, settings, optimizer, logger);

        Assert.NotNull(result);
        Assert.True(result.Length > 0);

        using var doc = new PdfDocument(new PdfReader(new MemoryStream(result)));
        Assert.True(doc.GetNumberOfPages() > 0);

        if (expectCropReduction.HasValue)
        {
            Assert.Equal(expectCropReduction.Value, HasReducedPageArea(doc));
        }

        _output.WriteLine($"SUCCESS [{settings.Method}]: {file} (Output: {result.Length} bytes)");
    }

    private static bool HasReducedPageArea(PdfDocument document)
    {
        for (var pageNumber = 1; pageNumber <= document.GetNumberOfPages(); pageNumber++)
        {
            var page = document.GetPage(pageNumber);
            var crop = page.GetCropBox();
            var media = page.GetMediaBox();

            if (crop.GetWidth() < media.GetWidth() - CropTolerance ||
                crop.GetHeight() < media.GetHeight() - CropTolerance ||
                Math.Abs(crop.GetLeft() - media.GetLeft()) > CropTolerance ||
                Math.Abs(crop.GetBottom() - media.GetBottom()) > CropTolerance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsBitmapCroppingSupported()
    {
        return OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();
    }

    private static (string FolderPath, IReadOnlyDictionary<string, PdfBatchCase> Cases) CreateRepresentativePdfBatch()
    {
        var cases = new[]
        {
            new PdfBatchCase(
                "text.pdf",
                ExpectContentCropReduction: true,
                ExpectBitmapCropReduction: true,
                CreateTextPdf),
            new PdfBatchCase(
                "image.pdf",
                ExpectContentCropReduction: true,
                ExpectBitmapCropReduction: true,
                CreateImagePdf),
            new PdfBatchCase(
                "tiny-dot.pdf",
                ExpectContentCropReduction: true,
                ExpectBitmapCropReduction: true,
                CreateTinyDotPdf),
            new PdfBatchCase(
                "empty.pdf",
                ExpectContentCropReduction: false,
                ExpectBitmapCropReduction: false,
                CreateEmptyPdf)
        };

        var folderPath = SystemPath.Combine(SystemPath.GetTempPath(), "PdfCropperTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folderPath);

        foreach (var testCase in cases)
        {
            File.WriteAllBytes(SystemPath.Combine(folderPath, testCase.FileName), testCase.CreatePdf());
        }

        return (folderPath, cases.ToDictionary(static item => item.FileName, StringComparer.OrdinalIgnoreCase));
    }

    private static byte[] CreateTextPdf()
    {
        using var stream = new MemoryStream();
        using (var writer = new PdfWriter(stream))
        using (var pdf = new PdfDocument(writer))
        using (var document = new Document(pdf, PageSize.A4))
        {
            document.SetMargins(96, 96, 96, 96);
            document.Add(new Paragraph("Representative text content").SetFontSize(24));
        }

        return stream.ToArray();
    }

    private static byte[] CreateImagePdf()
    {
        using var stream = new MemoryStream();
        using (var writer = new PdfWriter(stream))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);
            var imageData = ImageDataFactory.Create(SamplePng);
            canvas.AddImageFittedIntoRectangle(imageData, new Rectangle(180, 260, 72, 72), false);
        }

        return stream.ToArray();
    }

    private static byte[] CreateTinyDotPdf()
    {
        using var stream = new MemoryStream();
        using (var writer = new PdfWriter(stream))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.A4);
            var canvas = new PdfCanvas(page);
            canvas.Rectangle(140, 220, 1, 1);
            canvas.Fill();
        }

        return stream.ToArray();
    }

    private static byte[] CreateEmptyPdf()
    {
        using var stream = new MemoryStream();
        using (var writer = new PdfWriter(stream))
        using (var pdf = new PdfDocument(writer))
        {
            var page = pdf.AddNewPage(PageSize.A4);
            page.SetCropBox(new Rectangle(0, 0, PageSize.A4.GetWidth(), PageSize.A4.GetHeight()));
            page.SetTrimBox(page.GetCropBox());
        }

        return stream.ToArray();
    }

    private sealed record PdfBatchCase(
        string FileName,
        bool ExpectContentCropReduction,
        bool ExpectBitmapCropReduction,
        Func<byte[]> CreatePdf);

    private static readonly byte[] SamplePng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR4nGP8z/C/HwAF/gL+uQ0nSAAAAABJRU5ErkJggg==");

    private class XunitLogger : IPdfCropLogger
    {
        private readonly ITestOutputHelper _output;

        public XunitLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public Task LogInfoAsync(string message)
        {
            _output.WriteLine($"[INFO] {message}");
            return Task.CompletedTask;
        }

        public Task LogWarningAsync(string message)
        {
            _output.WriteLine($"[WARN] {message}");
            return Task.CompletedTask;
        }

        public Task LogErrorAsync(string message)
        {
            _output.WriteLine($"[ERROR] {message}");
            return Task.CompletedTask;
        }
    }
}
