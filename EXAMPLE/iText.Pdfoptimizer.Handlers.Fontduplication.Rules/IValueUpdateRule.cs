using iText.Kernel.Pdf;

namespace iText.Pdfoptimizer.Handlers.Fontduplication.Rules;

public interface IValueUpdateRule
{
	void Update(PdfDictionary pdfDictionary);
}
