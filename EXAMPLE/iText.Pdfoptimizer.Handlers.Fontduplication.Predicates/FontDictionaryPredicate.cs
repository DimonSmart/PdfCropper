using System.Collections.Generic;
using iText.Commons.Utils;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Util.Traversing;

namespace iText.Pdfoptimizer.Handlers.Fontduplication.Predicates;

public class FontDictionaryPredicate : IPdfObjectPredicate
{
	private static readonly ICollection<PdfName> FONTS_SUBTYPES = JavaCollectionsUtil.UnmodifiableSet<PdfName>((ISet<PdfName>)new HashSet<PdfName>(JavaUtil.ArraysAsList<PdfName>((PdfName[])(object)new PdfName[5]
	{
		PdfName.Type1,
		PdfName.Type0,
		PdfName.TrueType,
		PdfName.Type3,
		PdfName.MMType1
	})));

	public virtual bool Test(PdfObject @object)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		if (@object != null && @object.GetObjectType() == 3)
		{
			PdfDictionary val = (PdfDictionary)@object;
			PdfName asName = val.GetAsName(PdfName.Subtype);
			if (((object)PdfName.Font).Equals((object)val.Get(PdfName.Type)) && asName != null)
			{
				return FONTS_SUBTYPES.Contains(asName);
			}
			return false;
		}
		return false;
	}
}
