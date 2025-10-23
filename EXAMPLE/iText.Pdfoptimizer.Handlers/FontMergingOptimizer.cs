using System;
using System.Collections.Generic;
using iText.Commons.Utils;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Fontduplication;
using iText.Pdfoptimizer.Handlers.Fontduplication.Predicates;
using iText.Pdfoptimizer.Handlers.Fontduplication.Rules;
using iText.Pdfoptimizer.Handlers.Fontmerging;
using iText.Pdfoptimizer.Handlers.Fontsubsetting;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Report.Message;
using iText.Pdfoptimizer.Util;
using iText.Pdfoptimizer.Util.Traversing;

namespace iText.Pdfoptimizer.Handlers;

public class FontMergingOptimizer : AbstractOptimizationHandler
{
	private static readonly IPdfObjectPredicate PREDICATE = new FontDictionaryPredicate();

	protected internal override void OptimizePdf(PdfDocument document, OptimizationSession session)
	{
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Expected O, but got Unknown
		PdfConformance conformance = document.GetConformance();
		if (conformance.IsPdfA() && (((object)PdfAConformance.PDF_A_1A).Equals((object)conformance.GetAConformance()) || ((object)PdfAConformance.PDF_A_1B).Equals((object)conformance.GetAConformance())))
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Unable to merge fonts for PDF/A-1 document. Font merging will not be applied to optimize the document.");
			return;
		}
		IList<IList<PdfObject>> similarDictionariesList = DocumentStructureUtils.GetSimilarDictionariesList(DocumentStructureUtils.Search(document, PREDICATE), GetFontEqualityCalculator());
		IList<IList<PdfFont>> list = new List<IList<PdfFont>>();
		foreach (IList<PdfObject> item in similarDictionariesList)
		{
			IList<PdfFont> list2 = new List<PdfFont>();
			foreach (PdfDictionary item2 in item)
			{
				PdfDictionary val = item2;
				try
				{
					list2.Add(PdfFontFactory.CreateFont(val));
				}
				catch (Exception)
				{
					session.RegisterEvent(SeverityLevel.WARNING, "An exception occurred while font parsing.");
				}
			}
			if (!list2.IsEmpty())
			{
				list.Add(list2);
			}
		}
		if (list.IsEmpty())
		{
			session.RegisterEvent(SeverityLevel.INFO, "Fonts for merging are not found.");
			return;
		}
		session.RegisterEvent(SeverityLevel.INFO, "Amount of found fonts groups to merge: {0}", list.Count);
		IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> dictionary = new UsedGlyphsFinder().FindUsedGlyphsInFonts(document, session);
		IDictionary<PdfIndirectReference, UsedGlyphsFinder.FontGlyphs> dictionary2 = new Dictionary<PdfIndirectReference, UsedGlyphsFinder.FontGlyphs>(dictionary.Count);
		foreach (KeyValuePair<PdfFont, UsedGlyphsFinder.FontGlyphs> item3 in dictionary)
		{
			dictionary2.Put(((PdfObject)((PdfObjectWrapper<PdfDictionary>)(object)item3.Key).GetPdfObject()).GetIndirectReference(), item3.Value);
		}
		IDictionary<PdfObject, PdfObject> schema = MergeListOfFonts(list, dictionary2, session, document);
		DocumentStructureUtils.Traverse(document, new ReplaceObjectsAction(schema));
	}

	private static IDictionary<PdfObject, PdfObject> MergeListOfFonts(IList<IList<PdfFont>> listOfFontsToMerge, IDictionary<PdfIndirectReference, UsedGlyphsFinder.FontGlyphs> usedGlyphsInFonts, OptimizationSession session, PdfDocument document)
	{
		IDictionary<PdfObject, PdfObject> dictionary = new Dictionary<PdfObject, PdfObject>();
		foreach (IList<PdfFont> item in listOfFontsToMerge)
		{
			IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> dictionary2 = (IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs>)new LinkedDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs>();
			foreach (PdfFont item2 in item)
			{
				UsedGlyphsFinder.FontGlyphs fontGlyphs = usedGlyphsInFonts.Get(((PdfObject)((PdfObjectWrapper<PdfDictionary>)(object)item2).GetPdfObject()).GetIndirectReference());
				if (fontGlyphs != null && !fontGlyphs.GetGlyphs().IsEmpty())
				{
					dictionary2.Put(item2, fontGlyphs);
				}
			}
			if (dictionary2.Count <= 1)
			{
				continue;
			}
			PdfObject val = MergeFonts(dictionary2, session);
			if (val == null)
			{
				continue;
			}
			val.MakeIndirect(document);
			foreach (PdfFont item3 in item)
			{
				dictionary.Put((PdfObject)(object)((PdfObjectWrapper<PdfDictionary>)(object)item3).GetPdfObject(), val);
			}
		}
		return dictionary;
	}

	private static PdfObject MergeFonts(IDictionary<PdfFont, UsedGlyphsFinder.FontGlyphs> fontsToMergeWithGlyphs, OptimizationSession session)
	{
		PdfFont anyFont = TrueTypeFontUtil.GetAnyFont(fontsToMergeWithGlyphs);
		string fontName = anyFont.GetFontProgram().GetFontNames().GetFontName();
		try
		{
			if (anyFont is PdfTrueTypeFont)
			{
				return TrueTypeMerger.Merge(fontsToMergeWithGlyphs, session);
			}
			if (anyFont is PdfType0Font)
			{
				return Type0Merger.Merge(fontsToMergeWithGlyphs, session);
			}
			session.RegisterEvent(SeverityLevel.WARNING, "Fonts merging is skipped for {0} because of unsupported font type.", fontName);
		}
		catch (Exception)
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Unable to merge document fonts: {0}", fontName);
		}
		return null;
	}

	private static PdfDictionaryEqualityCalculator GetFontEqualityCalculator()
	{
		return new PdfDictionaryEqualityCalculator(new List<IValueUpdateRule>
		{
			(IValueUpdateRule)new RemoveSubsetPrefixRule(),
			(IValueUpdateRule)new RemoveUniqueFontSubsetFieldsRule()
		});
	}
}
