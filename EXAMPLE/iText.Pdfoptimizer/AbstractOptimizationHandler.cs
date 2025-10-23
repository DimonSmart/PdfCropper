using iText.Kernel.Pdf;

namespace iText.Pdfoptimizer;

public abstract class AbstractOptimizationHandler
{
	protected internal abstract void OptimizePdf(PdfDocument document, OptimizationSession session);

	internal void PrepareAndRunOptimization(PdfDocument document, OptimizationSession session)
	{
		session.GetLocationStack().EnterLocation(GetType().Name);
		OptimizePdf(document, session);
		session.GetLocationStack().LeaveLocation();
	}
}
