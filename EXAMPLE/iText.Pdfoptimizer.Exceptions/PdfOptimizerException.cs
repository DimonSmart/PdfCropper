using iText.Commons.Exceptions;

namespace iText.Pdfoptimizer.Exceptions;

public class PdfOptimizerException : ITextException
{
	public PdfOptimizerException(string message)
		: base(message)
	{
	}
}
