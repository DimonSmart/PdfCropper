using System.Collections.Generic;
using iText.IO.Font;
using iText.IO.Font.Otf;
using iText.IO.Util;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Fontduplication.Rules;
using iText.Pdfoptimizer.Handlers.Fontsubsetting;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Handlers.Fontmerging;

public sealed class Type0Merger
{
	private Type0Merger()
	{
	}

	public static PdfObject Merge(IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyphs, OptimizationSession session)
	{
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected O, but got Unknown
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		PdfFont anyFont = TrueTypeFontUtil.GetAnyFont(fontsToMergeWithGlyphs);
		if (!(anyFont.GetFontProgram() is DocTrueTypeFont))
		{
			return null;
		}
		PdfDictionary val = new PdfDictionary(((PdfObjectWrapper<PdfDictionary>)(object)anyFont).GetPdfObject());
		new RemoveSubsetPrefixRule().Update(val);
		new RemoveUniqueFontSubsetFieldsRule().Update(val);
		DocTrueTypeFont val2 = (DocTrueTypeFont)anyFont.GetFontProgram();
		string text = ((FontProgram)val2).GetFontNames().GetFontName();
		if (FontSubsetNameDetector.IsFontSubsetName(text))
		{
			text = text.Substring(7);
		}
		if (((object)PdfName.CIDFontType0).Equals((object)val2.GetSubtype()))
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Fonts merging is skipped for {0} because of unsupported font type.", text);
			return null;
		}
		return MergeType0Font(fontsToMergeWithGlyphs, val, text, session);
	}

	private static PdfObject MergeType0Font(IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyphs, PdfDictionary mergeFont, string fontName, OptimizationSession session)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		PdfStream val = TrueTypeFontUtil.MergeDocTrueTypeFonts(fontsToMergeWithGlyphs, fontName, session);
		if (val == null)
		{
			return null;
		}
		string text = FontUtil.AddRandomSubsetPrefixForFontName(fontName);
		TrueTypeFontUtil.UpdateFontNameWithSubsetPrefix(mergeFont, text);
		mergeFont.Put(PdfName.BaseFont, (PdfObject)new PdfName(text));
		PdfDictionary fontDescriptor = TrueTypeFontUtil.GetFontDescriptor(mergeFont);
		fontDescriptor.Put(PdfName.FontFile2, (PdfObject)(object)val);
		RemoveDeprecatedEntries(fontDescriptor);
		if (!MergeAndPutW(fontsToMergeWithGlyphs, mergeFont, fontName, session))
		{
			return null;
		}
		if (!TrueTypeFontUtil.MergeAndPutToUnicode(fontsToMergeWithGlyphs, mergeFont, fontName, session))
		{
			return null;
		}
		return (PdfObject)(object)mergeFont;
	}

	private static bool MergeAndPutW(IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyphs, PdfDictionary mergeFont, string fontName, OptimizationSession session)
	{
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Expected O, but got Unknown
		//IL_0156: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Expected O, but got Unknown
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Expected O, but got Unknown
		//IL_0179: Unknown result type (might be due to invalid IL or missing references)
		//IL_0183: Expected O, but got Unknown
		SortedDictionary<int, int?> sortedDictionary = new SortedDictionary<int, int?>();
		bool flag = false;
		bool flag2 = false;
		foreach (KeyValuePair<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyph in fontsToMergeWithGlyphs)
		{
			PdfArray asArray = ExtractCidFont(((PdfObjectWrapper<PdfDictionary>)(object)fontsToMergeWithGlyph.Key).GetPdfObject()).GetAsArray(PdfName.W);
			if (asArray == null)
			{
				flag2 = true;
				continue;
			}
			flag = true;
			IntHashtable val = FontUtil.ConvertCompositeWidthsArray(asArray);
			foreach (Glyph glyph in fontsToMergeWithGlyph.Value.GetGlyphs())
			{
				int code = glyph.GetCode();
				int num = val.Get(code);
				if (sortedDictionary.ContainsKey(code) && sortedDictionary.Get(code) != num)
				{
					session.RegisterEvent(SeverityLevel.WARNING, "Fonts merging is skipped for {0} because of incompatibility of W arrays.", fontName);
					return false;
				}
				sortedDictionary.Put(code, num);
			}
		}
		if (flag2 && flag)
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Fonts merging is skipped for {0} because of incompatibility of W arrays.", fontName);
			return false;
		}
		PdfArray val2 = new PdfArray();
		foreach (KeyValuePair<int, int?> item in sortedDictionary)
		{
			val2.Add((PdfObject)new PdfNumber(item.Key));
			PdfArray val3 = new PdfArray();
			val3.Add((PdfObject)new PdfNumber(item.Value.Value));
			val2.Add((PdfObject)(object)val3);
		}
		ExtractCidFont(mergeFont).Put(PdfName.W, (PdfObject)(object)val2);
		return true;
	}

	private static PdfDictionary ExtractCidFont(PdfDictionary font)
	{
		PdfArray asArray = font.GetAsArray(PdfName.DescendantFonts);
		if (asArray == null)
		{
			return null;
		}
		return asArray.GetAsDictionary(0);
	}

	private static void RemoveDeprecatedEntries(PdfDictionary fontDescriptor)
	{
		fontDescriptor.Remove(PdfName.CIDSet);
	}
}
