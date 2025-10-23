using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Handlers;

public class CompressionOptimizer : AbstractOptimizationHandler
{
	protected internal override void OptimizePdf(PdfDocument document, OptimizationSession session)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		object storedValue = session.GetStoredValue("writer-properties-key");
		if (storedValue is WriterProperties)
		{
			((WriterProperties)storedValue).SetCompressionLevel(9).SetFullCompressionMode(true);
		}
		else
		{
			session.RegisterEvent(SeverityLevel.ERROR, "WriterProperties are not accessible!");
		}
	}
}
