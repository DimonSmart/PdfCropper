using System;
using System.Collections.Generic;
using iText.IO.Font;
using iText.IO.Font.Otf;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Util;

namespace iText.Pdfoptimizer.Handlers.Fontsubsetting;

public sealed class TrueTypeSubsetter
{
	private TrueTypeSubsetter()
	{
	}

	public static void Subset(PdfTrueTypeFont font, ICollection<Glyph> glyphs)
	{
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected O, but got Unknown
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		if (((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject().GetAsDictionary(PdfName.FontDescriptor) == null || !(((PdfFont)font).GetFontProgram() is DocTrueTypeFont) || ((PdfFont)font).GetFontProgram().CountOfGlyphs() == glyphs.Count || glyphs.IsEmpty())
		{
			return;
		}
		bool flag = IsFontNameHasSubsetPrefix((DocTrueTypeFont)((PdfFont)font).GetFontProgram());
		TrueTypeFont val;
		try
		{
			val = TrueTypeFontUtil.CreateFontWithParser((DocTrueTypeFont)((PdfFont)font).GetFontProgram());
		}
		catch (Exception)
		{
			if (flag)
			{
				return;
			}
			throw;
		}
		if (val != null)
		{
			SortedSet<int> fontGids = TrueTypeFontUtil.GetFontGids(glyphs, isPdfTrueType: true, val);
			PdfStream fontStream = TrueTypeFontUtil.CreatePdfFontStream(val.GetSubset((ICollection<int>)fontGids, true));
			if (!flag)
			{
				UpdateFontNameWithSubsetPrefix(font);
			}
			SaveFontProgramStream(font, fontStream);
			RemoveDeprecatedEntries(font);
		}
	}

	private static bool IsFontNameHasSubsetPrefix(DocTrueTypeFont trueTypeFont)
	{
		return FontSubsetNameDetector.IsFontSubsetName(((FontProgram)trueTypeFont).GetFontNames().GetFontName());
	}

	private static void SaveFontProgramStream(PdfTrueTypeFont font, PdfStream fontStream)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		PdfDictionary val = (PdfDictionary)((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject().Get(PdfName.FontDescriptor);
		if (val != null && val.ContainsKey(PdfName.FontFile2))
		{
			val.Put(PdfName.FontFile2, (PdfObject)(object)fontStream);
		}
	}

	private static void UpdateFontNameWithSubsetPrefix(PdfTrueTypeFont font)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Expected O, but got Unknown
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		string text = FontUtil.AddRandomSubsetPrefixForFontName(((PdfFont)font).GetFontProgram().GetFontNames().GetFontName());
		((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject().Put(PdfName.BaseFont, (PdfObject)new PdfName(text));
		((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject().GetAsDictionary(PdfName.FontDescriptor).Put(PdfName.FontName, (PdfObject)new PdfName(text));
	}

	private static void RemoveDeprecatedEntries(PdfTrueTypeFont font)
	{
		((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject().GetAsDictionary(PdfName.FontDescriptor).Remove(PdfName.CharSet);
	}
}
