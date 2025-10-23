using System.Collections.Generic;
using iText.IO.Font;
using iText.IO.Font.Otf;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Fontduplication.Rules;
using iText.Pdfoptimizer.Handlers.Fontsubsetting;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Handlers.Fontmerging;

public sealed class TrueTypeMerger
{
	private TrueTypeMerger()
	{
	}

	public static PdfObject Merge(IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyphs, OptimizationSession session)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Expected O, but got Unknown
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Expected O, but got Unknown
		PdfFont anyFont = TrueTypeFontUtil.GetAnyFont(fontsToMergeWithGlyphs);
		if (!(anyFont.GetFontProgram() is DocTrueTypeFont))
		{
			return null;
		}
		string text = ((FontProgram)(DocTrueTypeFont)anyFont.GetFontProgram()).GetFontNames().GetFontName();
		if (FontSubsetNameDetector.IsFontSubsetName(text))
		{
			text = text.Substring(7);
		}
		PdfDictionary val = new PdfDictionary(((PdfObjectWrapper<PdfDictionary>)(object)anyFont).GetPdfObject());
		new RemoveSubsetPrefixRule().Update(val);
		new RemoveUniqueFontSubsetFieldsRule().Update(val);
		PdfStream val2 = TrueTypeFontUtil.MergeDocTrueTypeFonts(fontsToMergeWithGlyphs, text, session);
		if (val2 == null)
		{
			return null;
		}
		string text2 = FontUtil.AddRandomSubsetPrefixForFontName(text);
		PdfDictionary asDictionary = val.GetAsDictionary(PdfName.FontDescriptor);
		asDictionary.Put(PdfName.FontName, (PdfObject)new PdfName(text2));
		val.Put(PdfName.BaseFont, (PdfObject)new PdfName(text2));
		asDictionary.Put(PdfName.FontFile2, (PdfObject)(object)val2);
		RemoveDeprecatedEntries(asDictionary);
		if (!MergeAndPutWidths(fontsToMergeWithGlyphs, val, text, session))
		{
			return null;
		}
		if (!TrueTypeFontUtil.MergeAndPutToUnicode(fontsToMergeWithGlyphs, val, text, session))
		{
			return null;
		}
		return (PdfObject)(object)val;
	}

	private static bool MergeAndPutWidths(IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyphs, PdfDictionary mergeFont, string fontName, OptimizationSession session)
	{
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0130: Expected O, but got Unknown
		//IL_01a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Expected O, but got Unknown
		//IL_01b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c0: Expected O, but got Unknown
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Expected O, but got Unknown
		SortedSet<int> sortedSet = new SortedSet<int>();
		IDictionary<int, int?> dictionary = new Dictionary<int, int?>();
		foreach (KeyValuePair<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyph in fontsToMergeWithGlyphs)
		{
			PdfFont key = fontsToMergeWithGlyph.Key;
			PdfArray asArray = ((PdfObjectWrapper<PdfDictionary>)(object)key).GetPdfObject().GetAsArray(PdfName.Widths);
			int value = ((PdfObjectWrapper<PdfDictionary>)(object)key).GetPdfObject().GetAsInt(PdfName.FirstChar).Value;
			foreach (Glyph glyph in fontsToMergeWithGlyph.Value.GetGlyphs())
			{
				int code = glyph.GetCode();
				sortedSet.Add(code);
				int num = asArray.GetAsNumber(code - value).IntValue();
				if (dictionary.ContainsKey(code) && dictionary.Get(code) != num)
				{
					session.RegisterEvent(SeverityLevel.WARNING, "Fonts merging is skipped for {0} because of incompatibility of Widths arrays.", fontName);
					return false;
				}
				dictionary.Put(code, num);
			}
		}
		PdfArray val = new PdfArray();
		int num2 = ((!sortedSet.IsEmpty()) ? sortedSet.Min : 0);
		int num3 = ((!sortedSet.IsEmpty()) ? sortedSet.Max : 0);
		for (int i = num2; i <= num3; i++)
		{
			val.Add((PdfObject)new PdfNumber(dictionary.ContainsKey(i) ? dictionary.Get(i).Value : 0));
		}
		mergeFont.Put(PdfName.Widths, (PdfObject)(object)val);
		mergeFont.Put(PdfName.FirstChar, (PdfObject)new PdfNumber(num2));
		mergeFont.Put(PdfName.LastChar, (PdfObject)new PdfNumber(num3));
		return true;
	}

	private static void RemoveDeprecatedEntries(PdfDictionary fontDescriptor)
	{
		fontDescriptor.Remove(PdfName.CharSet);
	}
}
