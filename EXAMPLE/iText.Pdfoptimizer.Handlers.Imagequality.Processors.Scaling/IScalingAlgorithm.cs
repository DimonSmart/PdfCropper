using iText.Pdfoptimizer.Handlers.Util;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors.Scaling;

public interface IScalingAlgorithm
{
	BitmapImagePixels Scale(BitmapImagePixels original, double scaling);
}
