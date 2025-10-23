using iText.Kernel.Pdf.Xobject;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors;

public interface IImageProcessor
{
	PdfImageXObject ProcessImage(PdfImageXObject objectToProcess, OptimizationSession session);
}
