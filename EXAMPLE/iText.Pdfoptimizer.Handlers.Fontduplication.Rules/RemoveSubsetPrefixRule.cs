using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Util;

namespace iText.Pdfoptimizer.Handlers.Fontduplication.Rules;

public class RemoveSubsetPrefixRule : IValueUpdateRule
{
	private const int SUBSET_PREFIX_LENGTH = 7;

	public virtual void Update(PdfDictionary pdfDictionary)
	{
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Expected O, but got Unknown
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Expected O, but got Unknown
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Expected O, but got Unknown
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cb: Expected O, but got Unknown
		PdfName asName = pdfDictionary.GetAsName(PdfName.BaseFont);
		if (asName != null && FontSubsetNameDetector.IsFontSubsetName(asName.GetValue()))
		{
			pdfDictionary.Put(PdfName.BaseFont, (PdfObject)new PdfName(asName.GetValue().Substring(7)));
		}
		PdfDictionary asDictionary = pdfDictionary.GetAsDictionary(PdfName.FontDescriptor);
		if (asDictionary != null)
		{
			PdfName asName2 = asDictionary.GetAsName(PdfName.FontName);
			if (asName2 != null && FontSubsetNameDetector.IsFontSubsetName(asName2.GetValue()))
			{
				PdfDictionary val = new PdfDictionary(asDictionary);
				val.Put(PdfName.FontName, (PdfObject)new PdfName(asName2.GetValue().Substring(7)));
				pdfDictionary.Put(PdfName.FontDescriptor, (PdfObject)(object)val);
			}
		}
		PdfArray asArray = pdfDictionary.GetAsArray(PdfName.DescendantFonts);
		if (asArray == null)
		{
			return;
		}
		PdfArray val2 = new PdfArray(asArray);
		for (int i = 0; i < asArray.Size(); i++)
		{
			PdfDictionary asDictionary2 = asArray.GetAsDictionary(i);
			if (asDictionary2 != null)
			{
				PdfDictionary val3 = new PdfDictionary(asDictionary2);
				Update(val3);
				val2.Set(i, (PdfObject)(object)val3);
			}
		}
		pdfDictionary.Put(PdfName.DescendantFonts, (PdfObject)(object)val2);
	}
}
