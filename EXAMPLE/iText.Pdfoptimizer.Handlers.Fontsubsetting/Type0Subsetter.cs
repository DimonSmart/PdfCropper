using System;
using System.Collections.Generic;
using iText.Commons.Utils;
using iText.IO.Font;
using iText.IO.Font.Otf;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Util;

namespace iText.Pdfoptimizer.Handlers.Fontsubsetting;

public sealed class Type0Subsetter
{
	private Type0Subsetter()
	{
	}

	public static void Subset(PdfType0Font font, ICollection<Glyph> glyphs)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected O, but got Unknown
		if (((PdfFont)font).GetFontProgram() is DocTrueTypeFont && ((PdfFont)font).GetFontProgram().CountOfGlyphs() != glyphs.Count && !glyphs.IsEmpty())
		{
			DocTrueTypeFont val = (DocTrueTypeFont)((PdfFont)font).GetFontProgram();
			if (((object)PdfName.CIDFontType0).Equals((object)val.GetSubtype()))
			{
				SubsetCIDFontType0(font, glyphs, val);
			}
			else
			{
				SubsetType0Font(font, glyphs, val);
			}
		}
	}

	private static void SubsetCIDFontType0(PdfType0Font font, ICollection<Glyph> glyphs, DocTrueTypeFont trueTypeFont)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		byte[] cffBytes = GetCffBytes(font);
		if (cffBytes != null)
		{
			SortedSet<int> fontGids = TrueTypeFontUtil.GetFontGids(glyphs, isPdfTrueType: false, null);
			PdfStream val = TrueTypeFontUtil.CreatePdfFontStream(new CFFFontSubset(cffBytes, (ICollection<int>)fontGids).Process());
			((PdfDictionary)val).Put(PdfName.Subtype, (PdfObject)new PdfName("CIDFontType0C"));
			PdfDictionary fontDescriptor = TrueTypeFontUtil.GetFontDescriptor(((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject());
			UpdateCffName(trueTypeFont, font);
			fontDescriptor.Put(PdfName.FontFile3, (PdfObject)(object)val);
			RemoveDeprecatedEntries(fontDescriptor);
		}
	}

	private static byte[] GetCffBytes(PdfType0Font font)
	{
		try
		{
			return TrueTypeFontUtil.GetFontDescriptor(((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject()).GetAsStream(PdfName.FontFile3).GetBytes();
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static void RemoveDeprecatedEntries(PdfDictionary fontDescriptor)
	{
		fontDescriptor.Remove(PdfName.CIDSet);
	}

	private static void UpdateCffName(DocTrueTypeFont trueTypeFont, PdfType0Font font)
	{
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Expected O, but got Unknown
		string fontName = ((FontProgram)trueTypeFont).GetFontNames().GetFontName();
		if (!FontSubsetNameDetector.IsFontSubsetName(fontName))
		{
			fontName = FontUtil.AddRandomSubsetPrefixForFontName(fontName);
			((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject().Put(PdfName.BaseFont, (PdfObject)new PdfName(MessageFormatUtil.Format("{0}-{1}", new object[2]
			{
				fontName,
				font.GetCmap().GetCmapName()
			})));
			TrueTypeFontUtil.UpdateFontNameWithSubsetPrefix(((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject(), fontName);
		}
	}

	private static void SubsetType0Font(PdfType0Font font, ICollection<Glyph> glyphs, DocTrueTypeFont trueTypeFont)
	{
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		bool flag = FontSubsetNameDetector.IsFontSubsetName(((FontProgram)trueTypeFont).GetFontNames().GetFontName());
		TrueTypeFont val;
		try
		{
			val = TrueTypeFontUtil.CreateFontWithParser(trueTypeFont);
		}
		catch (Exception)
		{
			if (flag)
			{
				return;
			}
			throw;
		}
		if (val == null)
		{
			return;
		}
		SortedSet<int> fontGids = TrueTypeFontUtil.GetFontGids(glyphs, isPdfTrueType: false, null);
		if (!val.IsCff())
		{
			PdfStream val2 = TrueTypeFontUtil.CreatePdfFontStream(val.GetSubset((ICollection<int>)fontGids, true));
			string text = FontUtil.AddRandomSubsetPrefixForFontName(((FontProgram)trueTypeFont).GetFontNames().GetFontName());
			if (!flag)
			{
				TrueTypeFontUtil.UpdateFontNameWithSubsetPrefix(((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject(), text);
				((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject().Put(PdfName.BaseFont, (PdfObject)new PdfName(text));
			}
			PdfDictionary fontDescriptor = TrueTypeFontUtil.GetFontDescriptor(((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject());
			fontDescriptor.Put(PdfName.FontFile2, (PdfObject)(object)val2);
			RemoveDeprecatedEntries(fontDescriptor);
		}
	}
}
