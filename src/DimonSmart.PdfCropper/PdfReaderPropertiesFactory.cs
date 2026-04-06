using iText.Kernel.Pdf;

namespace DimonSmart.PdfCropper;

internal static class PdfReaderPropertiesFactory
{
    public static ReaderProperties Create(long documentSize)
    {
        var memoryLimitsAwareHandler = documentSize > 0
            ? new MemoryLimitsAwareHandler(documentSize)
            : new MemoryLimitsAwareHandler();

        return new ReaderProperties()
            .SetMemoryLimitsAwareHandler(memoryLimitsAwareHandler);
    }
}
