using System.Collections.Generic;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Fontduplication;
using iText.Pdfoptimizer.Handlers.Fontduplication.Rules;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Report.Message;
using iText.Pdfoptimizer.Util;
using iText.Pdfoptimizer.Util.Traversing;

namespace iText.Pdfoptimizer.Handlers;

public class PdfXObjectDuplicationOptimizer : AbstractOptimizationHandler
{
	private sealed class _IPdfObjectPredicate_24 : IPdfObjectPredicate
	{
		private readonly PdfImageXObjectPredicate first;

		private readonly PdfFormXObjectPredicate second;

		public _IPdfObjectPredicate_24()
		{
			first = new PdfImageXObjectPredicate();
			second = new PdfFormXObjectPredicate();
		}

		public bool Test(PdfObject @object)
		{
			if (!first.Test(@object))
			{
				return second.Test(@object);
			}
			return true;
		}
	}

	private static readonly IPdfObjectPredicate PREDICATE = new _IPdfObjectPredicate_24();

	protected internal override void OptimizePdf(PdfDocument document, OptimizationSession session)
	{
		IList<PdfObject> objects = DocumentStructureUtils.Search(document, PREDICATE);
		IDictionary<PdfObject, PdfObject> similarDictionaries = DocumentStructureUtils.GetSimilarDictionaries(document, objects, new PdfDictionaryEqualityCalculator(new List<IValueUpdateRule>()));
		if (similarDictionaries.IsEmpty())
		{
			session.RegisterEvent(SeverityLevel.INFO, "No xObject duplication found");
			return;
		}
		session.RegisterEvent(SeverityLevel.INFO, "Amount of found xObject duplications: {0}", similarDictionaries.Count);
		DocumentStructureUtils.Traverse(document, new ReplaceObjectsAction(similarDictionaries));
	}
}
