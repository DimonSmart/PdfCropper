using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Util.Traversing;

namespace iText.Pdfoptimizer.Handlers.Util;

public class PdfImageXObjectPredicate : IPdfObjectPredicate
{
	public bool Test(PdfObject @object)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		if (@object != null && @object.GetObjectType() == 9)
		{
			PdfStream val = (PdfStream)@object;
			if ((((PdfDictionary)val).Get(PdfName.Type) == null || ((object)PdfName.XObject).Equals((object)((PdfDictionary)val).Get(PdfName.Type))) && ((object)PdfName.Image).Equals((object)((PdfDictionary)val).Get(PdfName.Subtype)))
			{
				return CustomCondition(@object);
			}
			return false;
		}
		return false;
	}

	public virtual bool CustomCondition(PdfObject @object)
	{
		return true;
	}
}
