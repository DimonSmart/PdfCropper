using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using DimonSmart.PdfCropper;
using iText.Kernel.Pdf;
using iText.Kernel.Geom;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.XMP;

namespace DimonSmart.PdfCropper.Tests
{
    public class InfoCleanerCrashTests
    {
        [Fact]
        public async Task RemoveModDate_WithoutRemovingXmp_ShouldNotCrash()
        {
            // 1. Create a PDF with Metadata and XMP
            using var ms = new MemoryStream();
            using (var writer = new PdfWriter(ms))
            using (var pdf = new PdfDocument(writer))
            {
                pdf.GetDocumentInfo().SetTitle("Test PDF");
                pdf.GetDocumentInfo().SetCreator("Creator");

                // Trigger XMP creation
                pdf.SetTagged();
                var doc = new Document(pdf);
                doc.Add(new Paragraph("Hello World"));
                doc.Close();
            }

            var inputBytes = ms.ToArray();

            // 2. Configure settings to remove ModDate but KEEP XMP
            var cropSettings = new CropSettings(CropMethod.ContentBased);

            var optimizationSettings = new PdfOptimizationSettings(
                removeXmpMetadata: false, // KEEP XMP
                documentInfoKeysToRemove: new[] { "ModDate", "Creator" }
            );

            // 3. Process
            var logger = new SimpleTestLogger();

            // This should not throw
            var result = await PdfSmartCropper.CropAsync(inputBytes, cropSettings, optimizationSettings, logger);

            Assert.NotNull(result);
            Assert.True(result.Length > 0);
        }

        private class SimpleTestLogger : IPdfCropLogger
        {
            public Task LogInfoAsync(string message) => Task.CompletedTask;
            public Task LogWarningAsync(string message) => Task.CompletedTask;
            public Task LogErrorAsync(string message) => Task.CompletedTask;
        }
    }
}
