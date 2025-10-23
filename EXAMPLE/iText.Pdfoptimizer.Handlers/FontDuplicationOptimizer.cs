using System.Collections.Generic;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Fontduplication;
using iText.Pdfoptimizer.Handlers.Fontduplication.Predicates;
using iText.Pdfoptimizer.Handlers.Fontduplication.Rules;
using iText.Pdfoptimizer.Report.Message;
using iText.Pdfoptimizer.Util;
using iText.Pdfoptimizer.Util.Traversing;

namespace iText.Pdfoptimizer.Handlers;

public class FontDuplicationOptimizer : AbstractOptimizationHandler
{
	private static readonly IPdfObjectPredicate PREDICATE = new FontDictionaryPredicate();

	protected internal override void OptimizePdf(PdfDocument document, OptimizationSession session)
	{
		IList<PdfObject> objects = DocumentStructureUtils.Search(document, PREDICATE);
		IDictionary<PdfObject, PdfObject> similarDictionaries = DocumentStructureUtils.GetSimilarDictionaries(document, objects, GetPdfDictionaryEqualityCalculator());
		if (similarDictionaries.IsEmpty())
		{
			session.RegisterEvent(SeverityLevel.INFO, "No font duplication found");
			return;
		}
		session.RegisterEvent(SeverityLevel.INFO, "Amount of found font duplications: {0}", similarDictionaries.Count);
		DocumentStructureUtils.Traverse(document, new ReplaceObjectsAction(similarDictionaries));
	}

	private static PdfDictionaryEqualityCalculator GetPdfDictionaryEqualityCalculator()
	{
		return new PdfDictionaryEqualityCalculator(new List<IValueUpdateRule> { (IValueUpdateRule)new RemoveSubsetPrefixRule() });
	}
}
