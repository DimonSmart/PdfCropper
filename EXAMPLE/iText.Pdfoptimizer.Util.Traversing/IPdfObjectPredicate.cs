using iText.Kernel.Pdf;

namespace iText.Pdfoptimizer.Util.Traversing;

public interface IPdfObjectPredicate
{
	bool Test(PdfObject @object);
}
