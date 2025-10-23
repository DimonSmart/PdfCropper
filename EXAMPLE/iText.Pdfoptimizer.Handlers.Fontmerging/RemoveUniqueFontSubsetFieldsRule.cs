using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Fontduplication.Rules;

namespace iText.Pdfoptimizer.Handlers.Fontmerging;

public class RemoveUniqueFontSubsetFieldsRule : IValueUpdateRule
{
	public virtual void Update(PdfDictionary pdfDictionary)
	{
		bool flag = ((object)PdfName.Type0).Equals((object)pdfDictionary.GetAsName(PdfName.Subtype));
		bool flag2 = ((object)PdfName.TrueType).Equals((object)pdfDictionary.GetAsName(PdfName.Subtype));
		if (flag || flag2)
		{
			pdfDictionary.Remove(PdfName.ToUnicode);
			pdfDictionary.Remove(PdfName.Name);
			if (flag)
			{
				UpdateType0(pdfDictionary);
			}
			else
			{
				UpdateTrueType(pdfDictionary);
			}
		}
	}

	private static void UpdateTrueType(PdfDictionary pdfDictionary)
	{
		UpdateFontDescriptor(pdfDictionary);
		pdfDictionary.Remove(PdfName.FirstChar);
		pdfDictionary.Remove(PdfName.LastChar);
		pdfDictionary.Remove(PdfName.Widths);
	}

	private static void UpdateType0(PdfDictionary pdfDictionary)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		PdfArray asArray = pdfDictionary.GetAsArray(PdfName.DescendantFonts);
		if (asArray == null)
		{
			return;
		}
		PdfArray val = new PdfArray(asArray);
		for (int i = 0; i < asArray.Size(); i++)
		{
			PdfDictionary asDictionary = asArray.GetAsDictionary(i);
			if (asDictionary != null)
			{
				PdfDictionary val2 = new PdfDictionary(asDictionary);
				val2.Remove(PdfName.W);
				UpdateFontDescriptor(val2);
				val.Set(i, (PdfObject)(object)val2);
			}
		}
		pdfDictionary.Put(PdfName.DescendantFonts, (PdfObject)(object)val);
	}

	private static void UpdateFontDescriptor(PdfDictionary pdfDictionary)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		PdfDictionary asDictionary = pdfDictionary.GetAsDictionary(PdfName.FontDescriptor);
		if (asDictionary != null)
		{
			PdfDictionary val = new PdfDictionary(asDictionary);
			val.Remove(PdfName.FontFile2);
			val.Remove(PdfName.CIDSet);
			pdfDictionary.Put(PdfName.FontDescriptor, (PdfObject)(object)val);
		}
	}
}
