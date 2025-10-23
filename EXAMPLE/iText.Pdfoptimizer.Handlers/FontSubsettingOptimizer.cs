using System;
using System.Collections.Generic;
using iText.IO.Font.Otf;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Fontsubsetting;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Handlers;

public class FontSubsettingOptimizer : AbstractOptimizationHandler
{
	protected internal override void OptimizePdf(PdfDocument document, OptimizationSession session)
	{
		PdfConformance conformance = document.GetConformance();
		if (conformance.IsPdfA() && ((object)PdfAConformance.PDF_A_1A).Equals((object)conformance.GetAConformance()))
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Unable to subset fonts for PDF/A-1 document. Font subsetting will not be applied to optimize the document.");
			return;
		}
		try
		{
			IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> dictionary = new UsedGlyphsFinder().FindUsedGlyphsInFonts(document, session);
			ICollection<PdfIndirectReference> drFonts = FindAcroformDrFonts(document);
			ICollection<PdfIndirectReference> sharedFontPrograms = FindSharedFontPrograms(dictionary.Keys);
			foreach (KeyValuePair<PdfFont, UsedGlyphsFinder.FontGlyphs> item in dictionary)
			{
				UsedGlyphsFinder.FontGlyphs value = item.Value;
				PdfFont key = item.Key;
				string fontName = key.GetFontProgram().GetFontNames().GetFontName();
				bool num = IsAllGlyphsDecoded(value, session, fontName);
				bool flag = IsFontUsed(value, session, fontName);
				bool flag2 = IsFontProgramUnique(sharedFontPrograms, key, session, fontName);
				bool flag3 = IsFontProgramNotUsedInFormsDr(drFonts, key, session, fontName);
				if (num && flag && flag2 && flag3)
				{
					SubsetFont(key, value.GetGlyphs(), session);
				}
			}
		}
		catch (Exception)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Unable to subset document fonts");
		}
	}

	private void SubsetFont(PdfFont font, ICollection<Glyph> glyphs, OptimizationSession session)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		try
		{
			if (font is PdfTrueTypeFont)
			{
				TrueTypeSubsetter.Subset((PdfTrueTypeFont)font, glyphs);
				return;
			}
			if (font is PdfType0Font)
			{
				Type0Subsetter.Subset((PdfType0Font)font, glyphs);
				return;
			}
			session.RegisterEvent(SeverityLevel.WARNING, "Font subset creation is skipped for {0}: {1}", "unsupported font type", ((object)font).GetType().Name);
		}
		catch (Exception)
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Unable to subset document font: {0}", font.GetFontProgram().GetFontNames());
		}
	}

	private ICollection<PdfIndirectReference> FindAcroformDrFonts(PdfDocument document)
	{
		ICollection<PdfIndirectReference> collection = new HashSet<PdfIndirectReference>();
		PdfDictionary asDictionary = ((PdfObjectWrapper<PdfDictionary>)(object)document.GetCatalog()).GetPdfObject().GetAsDictionary(PdfName.AcroForm);
		if (asDictionary != null)
		{
			PdfDictionary asDictionary2 = asDictionary.GetAsDictionary(PdfName.DR);
			if (asDictionary2 != null && asDictionary2.GetAsDictionary(PdfName.Font) != null)
			{
				foreach (PdfName item in asDictionary2.GetAsDictionary(PdfName.Font).KeySet())
				{
					PdfDictionary asDictionary3 = asDictionary2.GetAsDictionary(PdfName.Font).GetAsDictionary(item);
					if (asDictionary3 != null && ((PdfObject)asDictionary3).GetIndirectReference() != null)
					{
						collection.Add(((PdfObject)asDictionary3).GetIndirectReference());
					}
				}
			}
		}
		return collection;
	}

	private ICollection<PdfIndirectReference> FindSharedFontPrograms(ICollection<PdfFont> usedFonts)
	{
		ICollection<PdfIndirectReference> collection = new HashSet<PdfIndirectReference>();
		ICollection<PdfIndirectReference> collection2 = new HashSet<PdfIndirectReference>();
		foreach (PdfFont usedFont in usedFonts)
		{
			PdfDictionary asDictionary = ((PdfObjectWrapper<PdfDictionary>)(object)usedFont).GetPdfObject().GetAsDictionary(PdfName.FontDescriptor);
			if (asDictionary != null && ((PdfObject)asDictionary).GetIndirectReference() != null)
			{
				if (collection.Contains(((PdfObject)asDictionary).GetIndirectReference()))
				{
					collection2.Add(((PdfObject)asDictionary).GetIndirectReference());
				}
				else
				{
					collection.Add(((PdfObject)asDictionary).GetIndirectReference());
				}
			}
		}
		return collection2;
	}

	private bool IsAllGlyphsDecoded(UsedGlyphsFinder.FontGlyphs fontGlyphs, OptimizationSession session, string fontName)
	{
		if (fontGlyphs.IsAnyGlyphsDecodingFailed())
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Unable to subset document font, not all used glyphs were decoded: {0}", fontName);
			return false;
		}
		return true;
	}

	private bool IsFontUsed(UsedGlyphsFinder.FontGlyphs fontGlyphs, OptimizationSession session, string fontName)
	{
		if (fontGlyphs.GetGlyphs().IsEmpty())
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Unable to subset document font, no used glyphs were found: {0}", fontName);
			return false;
		}
		return true;
	}

	private bool IsFontProgramUnique(ICollection<PdfIndirectReference> sharedFontPrograms, PdfFont font, OptimizationSession session, string fontName)
	{
		PdfDictionary asDictionary = ((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject().GetAsDictionary(PdfName.FontDescriptor);
		if (asDictionary != null && sharedFontPrograms.Contains(((PdfObject)asDictionary).GetIndirectReference()))
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Unable to subset document font, its font descriptor is shared with other fonts: {0}", fontName);
			return false;
		}
		return true;
	}

	private bool IsFontProgramNotUsedInFormsDr(ICollection<PdfIndirectReference> drFonts, PdfFont font, OptimizationSession session, string fontName)
	{
		if (drFonts.Contains(((PdfObject)((PdfObjectWrapper<PdfDictionary>)(object)font).GetPdfObject()).GetIndirectReference()))
		{
			session.RegisterEvent(SeverityLevel.INFO, "Font subset creation is skipped for {0}: {1}", fontName, "font is used in AcroForm default resources.");
			return false;
		}
		return true;
	}
}
