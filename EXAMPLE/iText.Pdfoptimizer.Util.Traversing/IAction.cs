using iText.Kernel.Pdf;

namespace iText.Pdfoptimizer.Util.Traversing;

public interface IAction
{
	void ProcessIndirectObjectDefinition(PdfObject @object);

	PdfObject ProcessObject(PdfObject @object);
}
