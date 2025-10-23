using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Util.Traversing;

namespace iText.Pdfoptimizer.Handlers.Util;

public class PdfFormXObjectPredicate : IPdfObjectPredicate
{
	public bool Test(PdfObject @object)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		if (@object != null && @object.GetObjectType() == 9)
		{
			PdfStream val = (PdfStream)@object;
			if (((PdfDictionary)val).Get(PdfName.Type) == null || ((object)PdfName.XObject).Equals((object)((PdfDictionary)val).Get(PdfName.Type)))
			{
				return ((object)PdfName.Form).Equals((object)((PdfDictionary)val).Get(PdfName.Subtype));
			}
			return false;
		}
		return false;
	}
}
