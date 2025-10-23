using System;
using System.Collections.Generic;
using iText.Commons.Utils;
using iText.IO.Exceptions;
using iText.IO.Font;
using iText.IO.Font.Cmap;
using iText.IO.Font.Otf;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Fontsubsetting;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Handlers.Util;

public sealed class TrueTypeFontUtil
{
	private TrueTypeFontUtil()
	{
	}

	public static TrueTypeFont CreateFontWithParser(DocTrueTypeFont docTrueTypeFont)
	{
		PdfStream fontFile = docTrueTypeFont.GetFontFile();
		if (fontFile == null)
		{
			return null;
		}
		return FontProgramFactory.CreateTrueTypeFont(fontFile.GetBytes(), true);
	}

	public static SortedSet<int> GetFontGids(ICollection<Glyph> glyphs, bool isPdfTrueType, TrueTypeFont font)
	{
		SortedSet<int> sortedSet = new SortedSet<int>();
		foreach (Glyph glyph in glyphs)
		{
			if (isPdfTrueType)
			{
				sortedSet.Add(((FontProgram)font).GetGlyph(glyph.GetUnicode()).GetCode());
			}
			else
			{
				sortedSet.Add(glyph.GetCode());
			}
		}
		return sortedSet;
	}

	public static void UpdateFontNameWithSubsetPrefix(PdfDictionary font, string fontName)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		GetFontDescriptor(font).Put(PdfName.FontName, (PdfObject)new PdfName(fontName));
		PdfArray asArray = font.GetAsArray(PdfName.DescendantFonts);
		if (asArray.GetAsDictionary(0).ContainsKey(PdfName.BaseFont))
		{
			asArray.GetAsDictionary(0).Put(PdfName.BaseFont, (PdfObject)new PdfName(fontName));
		}
	}

	public static PdfDictionary GetFontDescriptor(PdfDictionary font)
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Expected O, but got Unknown
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		foreach (PdfObject item in font.GetAsArray(PdfName.DescendantFonts))
		{
			if (item.IsDictionary() && ((PdfDictionary)item).ContainsKey(PdfName.FontDescriptor))
			{
				return ((PdfDictionary)item).GetAsDictionary(PdfName.FontDescriptor);
			}
		}
		return (PdfDictionary)font.Get(PdfName.FontDescriptor);
	}

	public static PdfStream CreatePdfFontStream(byte[] fontStreamBytes)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		//IL_001b: Expected O, but got Unknown
		PdfStream val = new PdfStream(fontStreamBytes);
		((PdfDictionary)val).Put(PdfName.Length1, (PdfObject)new PdfNumber(fontStreamBytes.Length));
		return val;
	}

	public static PdfStream MergeDocTrueTypeFonts(IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyphs, string fontName, OptimizationSession session)
	{
		//IL_00f2: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		IDictionary<DocTrueTypeFont, UsedGlyphsFinder.FontGlyphs> dictionary = (IDictionary<DocTrueTypeFont, UsedGlyphsFinder.FontGlyphs>)new LinkedDictionary<DocTrueTypeFont, UsedGlyphsFinder.FontGlyphs>();
		foreach (KeyValuePair<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyph in fontsToMergeWithGlyphs)
		{
			dictionary.Put((DocTrueTypeFont)fontsToMergeWithGlyph.Key.GetFontProgram(), fontsToMergeWithGlyph.Value);
		}
		bool isPdfTrueType = GetAnyFont(fontsToMergeWithGlyphs) is PdfTrueTypeFont;
		IDictionary<TrueTypeFont, ICollection<int>> dictionary2 = (IDictionary<TrueTypeFont, ICollection<int>>)new LinkedDictionary<TrueTypeFont, ICollection<int>>();
		foreach (KeyValuePair<DocTrueTypeFont, UsedGlyphsFinder.FontGlyphs> item in dictionary)
		{
			TrueTypeFont val = CreateFontWithParser(item.Key);
			if (val.IsCff())
			{
				session.RegisterEvent(SeverityLevel.WARNING, "Fonts merging is skipped for {0} because of unsupported font type.", fontName);
				return null;
			}
			SortedSet<int> fontGids = GetFontGids(item.Value.GetGlyphs(), isPdfTrueType, val);
			dictionary2.Put(val, fontGids);
		}
		byte[] fontStreamBytes;
		try
		{
			fontStreamBytes = TrueTypeFont.Merge(dictionary2, fontName);
		}
		catch (IOException ex)
		{
			IOException ex2 = ex;
			session.RegisterEvent(SeverityLevel.WARNING, "Fonts merging is skipped for {0} because of: {1}", fontName, ((Exception)(object)ex2).Message);
			return null;
		}
		return CreatePdfFontStream(fontStreamBytes);
	}

	public static PdfFont GetAnyFont(IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> fonts)
	{
		using (IEnumerator<KeyValuePair<PdfFont, UsedGlyphsFinder.FontGlyphs>> enumerator = fonts.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				return enumerator.Current.Key;
			}
		}
		return null;
	}

	public static bool MergeAndPutToUnicode(IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyphs, PdfDictionary mergeFont, string fontName, OptimizationSession session)
	{
		IDictionary<PdfStream, UsedGlyphsFinder.FontGlyphs> dictionary = new Dictionary<PdfStream, UsedGlyphsFinder.FontGlyphs>();
		bool flag = false;
		bool flag2 = false;
		foreach (KeyValuePair<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyph in fontsToMergeWithGlyphs)
		{
			PdfStream asStream = ((PdfObjectWrapper<PdfDictionary>)(object)fontsToMergeWithGlyph.Key).GetPdfObject().GetAsStream(PdfName.ToUnicode);
			if (asStream == null)
			{
				flag = true;
				continue;
			}
			flag2 = true;
			dictionary.Put(asStream, fontsToMergeWithGlyph.Value);
		}
		if (flag && flag2)
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Fonts merging is skipped for {0} because of incompatibility of ToUnicode streams.", fontName);
			return false;
		}
		if (dictionary.IsEmpty())
		{
			return true;
		}
		PdfStream val = MergeToUnicodeStreams(dictionary, fontName, session);
		if (val == null)
		{
			return false;
		}
		mergeFont.Put(PdfName.ToUnicode, (PdfObject)(object)val);
		return true;
	}

	private static PdfStream MergeToUnicodeStreams(IDictionary<PdfStream, UsedGlyphsFinder.FontGlyphs> toUnicodeToMerge, string fontName, OptimizationSession session)
	{
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Expected O, but got Unknown
		SortedDictionary<int, Glyph> sortedDictionary = new SortedDictionary<int, Glyph>();
		foreach (KeyValuePair<PdfStream, UsedGlyphsFinder.FontGlyphs> item in toUnicodeToMerge)
		{
			CMapToUnicode val = FontUtil.ProcessToUnicode((PdfObject)(object)item.Key);
			foreach (Glyph glyph in item.Value.GetGlyphs())
			{
				int code = glyph.GetCode();
				char[] array = val.Lookup(code);
				Glyph val2 = new Glyph(code, 0, array);
				if (sortedDictionary.ContainsKey(code) && !((object)val2).Equals((object)sortedDictionary.Get(code)))
				{
					session.RegisterEvent(SeverityLevel.WARNING, "Fonts merging is skipped for {0} because of incompatibility of ToUnicode streams.", fontName);
					return null;
				}
				sortedDictionary.Put(code, val2);
			}
		}
		return FontUtil.GetToUnicodeStream((ICollection<Glyph>)new LinkedHashSet<Glyph>((ICollection<Glyph>)sortedDictionary.Values));
	}
}
