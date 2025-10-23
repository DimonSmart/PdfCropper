using System.Collections.Generic;
using iText.Kernel.Pdf.Xobject;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors;

public class CombinedImageProcessor : IImageProcessor
{
	private readonly IList<IImageProcessor> processors = new List<IImageProcessor>();

	public virtual CombinedImageProcessor AddProcessor(IImageProcessor processor)
	{
		processors.Add(processor);
		return this;
	}

	public virtual PdfImageXObject ProcessImage(PdfImageXObject objectToProcess, OptimizationSession session)
	{
		PdfImageXObject val = objectToProcess;
		foreach (IImageProcessor processor in processors)
		{
			val = processor.ProcessImage(val, session);
		}
		return val;
	}
}
