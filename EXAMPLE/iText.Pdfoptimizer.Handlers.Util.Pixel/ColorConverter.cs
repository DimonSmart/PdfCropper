using iText.Kernel.Pdf;

namespace iText.Pdfoptimizer.Handlers.Util.Pixel;

public interface ColorConverter
{
	double[] ConvertColor(double[] original);

	PdfName GetSourceColorspace();

	int GetSourceNumberOfComponents();

	PdfName GetTargetColorspace();

	int GetTargetNumberOfComponents();
}
