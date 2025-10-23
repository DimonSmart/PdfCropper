using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;

namespace iText.Pdfoptimizer.Handlers.Converters;

public interface ICsConverter
{
	PdfImageXObject ConvertImageCs(PdfImageXObject imageToConvert, OptimizationSession session);

	PdfStream ConvertContentStream(PdfStream contentStream, PdfResources externalResources, OptimizationSession session);

	void ConvertStoredResources(OptimizationSession session);

	PdfArray ConvertAnnotationIcArray(PdfArray icArray);

	void AttemptToConvertTransparencyGroup(PdfObject groupEntryHolder, OptimizationSession session);

	CsConverterProperties GetConverterProperties();
}
