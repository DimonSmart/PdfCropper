using iText.Kernel.Pdf.Xobject;
using iText.Pdfoptimizer.Handlers.Imagequality.Processors.Scaling;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors;

public class BitmapCompressor : IImageProcessor
{
	private readonly CombinedImageProcessor processor;

	public BitmapCompressor(double scaling, double compression)
		: this(scaling, new AverageCalculationAlgorithm(), compression)
	{
	}

	public BitmapCompressor(double scaling, IScalingAlgorithm algorithm, double compression)
	{
		processor = new CombinedImageProcessor().AddProcessor(new BitmapDeindexer()).AddProcessor(new BitmapScalingProcessor(scaling, algorithm)).AddProcessor(new BitmapCmykToRgbConverter())
			.AddProcessor(new JpegCompressor(compression))
			.AddProcessor(new BitmapIndexer());
	}

	public virtual PdfImageXObject ProcessImage(PdfImageXObject objectToProcess, OptimizationSession session)
	{
		return processor.ProcessImage(objectToProcess, session);
	}
}
