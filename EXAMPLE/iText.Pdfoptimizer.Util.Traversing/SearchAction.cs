using System.Collections.Generic;
using iText.Kernel.Pdf;

namespace iText.Pdfoptimizer.Util.Traversing;

public class SearchAction : IAction
{
	private readonly IList<PdfObject> foundObjects = new List<PdfObject>();

	private readonly IPdfObjectPredicate predicate;

	public SearchAction(IPdfObjectPredicate predicate)
	{
		this.predicate = predicate;
	}

	public virtual IList<PdfObject> GetFoundObjects()
	{
		return foundObjects;
	}

	public virtual void ProcessIndirectObjectDefinition(PdfObject @object)
	{
		Search(@object);
	}

	public virtual PdfObject ProcessObject(PdfObject @object)
	{
		if (@object != null && !@object.IsIndirect())
		{
			Search(@object);
		}
		return @object;
	}

	private void Search(PdfObject @object)
	{
		if (predicate.Test(@object))
		{
			foundObjects.Add(@object);
		}
	}
}
