using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using DimonSmart.PdfCropper;
using DimonSmart.PdfCropper.Cli;

namespace DimonSmart.PdfCropper.Tests;

public class FolderProcessingTests
{
    private readonly ITestOutputHelper _output;

    public FolderProcessingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // Set this path to the folder containing PDFs you want to test
    // You can override this via environment variable PDF_TEST_FOLDER
    private const string DefaultTestFolder = @"C:\TestPdfs";

    [Fact]
    public async Task ProcessAllPdfsInFolder_WithRepeatedObjectDetection()
    {
        var folderPath = Environment.GetEnvironmentVariable("PDF_TEST_FOLDER") ?? DefaultTestFolder;

        if (!Directory.Exists(folderPath))
        {
            _output.WriteLine($"Test folder '{folderPath}' not found. Skipping folder test.");
            return;
        }

        var files = Directory.GetFiles(folderPath, "*.pdf");
        if (files.Length == 0)
        {
            _output.WriteLine($"No PDF files found in '{folderPath}'. Skipping.");
            return;
        }

        var settings = new CropSettings(
            method: CropMethod.ContentBased,
            detectRepeatedObjects: true,
            repeatedObjectOccurrenceThreshold: 10,
            repeatedObjectMinimumPageCount: 3,
            margin: 10
        );

        var optimizer = new PdfOptimizationSettings(
            compressionLevel: 9,
            removeUnusedObjects: true,
            mergeDuplicateFontSubsets: true
        );

        var logger = new XunitLogger(_output);

        foreach (var file in files)
        {
            _output.WriteLine($"PROCESSING: {file}");
            try
            {
                var bytes = await File.ReadAllBytesAsync(file);

                // Assert that it doesn't throw
                var result = await PdfSmartCropper.CropAsync(bytes, settings, optimizer, logger);

                Assert.NotNull(result);
                Assert.True(result.Length > 0);

                // Validating that the result is a valid PDF
                using var doc = new iText.Kernel.Pdf.PdfDocument(new iText.Kernel.Pdf.PdfReader(new MemoryStream(result)));
                Assert.True(doc.GetNumberOfPages() > 0);

                _output.WriteLine($"SUCCESS: {file} (Output: {result.Length} bytes)");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"FAIL: {file} - {ex.Message}");
                // We rethrow to fail the test, but we might want to continue processing others?
                // The user asked to "verify processing passes without error", so any failure should fail the test.
                throw;
            }
        }
    }

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
