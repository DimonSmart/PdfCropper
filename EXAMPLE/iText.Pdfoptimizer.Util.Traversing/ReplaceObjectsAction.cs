using System.Collections.Generic;
using iText.Kernel.Pdf;

namespace iText.Pdfoptimizer.Util.Traversing;

public class ReplaceObjectsAction : IAction
{
	private readonly IDictionary<PdfObject, PdfObject> schema;

	public ReplaceObjectsAction(IDictionary<PdfObject, PdfObject> schema)
	{
		this.schema = schema;
	}

	public virtual void ProcessIndirectObjectDefinition(PdfObject @object)
	{
	}

	public virtual PdfObject ProcessObject(PdfObject @object)
	{
		PdfObject val = schema.Get(@object);
		if (val != null)
		{
			return val;
		}
		return @object;
	}
}
