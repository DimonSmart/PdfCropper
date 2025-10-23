using System.Collections.Generic;
using iText.IO.Source;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Kernel.Pdf.Colorspace;

namespace iText.Pdfoptimizer.Handlers.Converters;

public class PdfCanvasCsConverter : PdfCanvasProcessor
{
	private sealed class _IXObjectDoHandler_41 : IXObjectDoHandler
	{
		public void HandleXObject(PdfCanvasProcessor processor, Stack<CanvasTag> canvasTagHierarchy, PdfStream stream, PdfName xObjectName)
		{
		}
	}

	private readonly PdfCanvas canvas;

	private readonly AbstractCsConverter csConverter;

	private readonly OptimizationSession session;

	public PdfCanvasCsConverter(PdfDocument document, AbstractCsConverter csConverter, OptimizationSession session)
		: base((IEventListener)(object)new IdleEventListener())
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		//IL_001c: Expected O, but got Unknown
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		canvas = new PdfCanvas(new PdfStream(), new PdfResources(), document);
		this.csConverter = csConverter;
		this.session = session;
		((PdfCanvasProcessor)this).RegisterXObjectDoHandler(PdfName.Form, (IXObjectDoHandler)(object)new _IXObjectDoHandler_41());
	}

	public virtual PdfCanvas GetCanvas()
	{
		return canvas;
	}

	protected override void InvokeOperator(PdfLiteral @operator, IList<PdfObject> operands)
	{
		((PdfCanvasProcessor)this).InvokeOperator(@operator, operands);
		string text = ((object)@operator).ToString();
		PdfColorSpace colorSpace = ((CanvasGraphicsState)((PdfCanvasProcessor)this).GetGraphicsState()).GetFillColor().GetColorSpace();
		PdfColorSpace colorSpace2 = ((CanvasGraphicsState)((PdfCanvasProcessor)this).GetGraphicsState()).GetStrokeColor().GetColorSpace();
		IList<PdfObject> operands2 = csConverter.ConvertContentStreamOperands(colorSpace, colorSpace2, text, operands, session);
		WriteOperands(canvas, operands2);
	}

	private static void WriteOperands(PdfCanvas canvas, IList<PdfObject> operands)
	{
		int num = 0;
		foreach (PdfObject operand in operands)
		{
			canvas.GetContentStream().GetOutputStream().Write(operand);
			if (operands.Count > ++num)
			{
				((HighPrecisionOutputStream<PdfOutputStream>)(object)canvas.GetContentStream().GetOutputStream()).WriteSpace();
			}
			else
			{
				((HighPrecisionOutputStream<PdfOutputStream>)(object)canvas.GetContentStream().GetOutputStream()).WriteNewLine();
			}
		}
	}
}
