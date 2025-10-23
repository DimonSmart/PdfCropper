using System;
using iText.Commons.Utils;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using iText.Pdfoptimizer.Handlers.Imagequality.Processors.Scaling;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors;

public class BitmapScalingProcessor : IImageProcessor
{
	private readonly double scaling;

	private readonly IScalingAlgorithm algorithm;

	public BitmapScalingProcessor(double scaling)
		: this(scaling, GetDefaultAlgorithm())
	{
	}

	public BitmapScalingProcessor(double scaling, IScalingAlgorithm algorithm)
	{
		if (scaling > 1.0 || scaling <= 0.0)
		{
			throw new ArgumentException(MessageFormatUtil.Format("Invalid scaling parameter! Value {0} is out of range (0, 1]", new object[1] { scaling }));
		}
		this.scaling = scaling;
		this.algorithm = algorithm;
	}

	public virtual PdfImageXObject ProcessImage(PdfImageXObject objectToProcess, OptimizationSession session)
	{
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Expected O, but got Unknown
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Expected O, but got Unknown
		//IL_00bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d0: Expected O, but got Unknown
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Expected O, but got Unknown
		if (scaling == 1.0)
		{
			return objectToProcess;
		}
		PdfObject val = ((PdfDictionary)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).Get(PdfName.Filter);
		if (val != null && !((object)PdfName.FlateDecode).Equals((object)val))
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Filter {0} is not supported by image processor {1}. Unable to optimize image with reference {2}", val, GetType(), ((PdfObject)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).GetIndirectReference());
			return objectToProcess;
		}
		BitmapImagePixels original = new BitmapImagePixels(objectToProcess);
		BitmapImagePixels bitmapImagePixels = algorithm.Scale(original, scaling);
		PdfStream val2 = (PdfStream)((PdfObject)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).Clone();
		((PdfDictionary)val2).Put(PdfName.Width, (PdfObject)new PdfNumber(bitmapImagePixels.GetWidth()));
		((PdfDictionary)val2).Put(PdfName.Height, (PdfObject)new PdfNumber(bitmapImagePixels.GetHeight()));
		val2.SetData(bitmapImagePixels.GetData());
		return new PdfImageXObject(val2);
	}

	private static IScalingAlgorithm GetDefaultAlgorithm()
	{
		return new AverageCalculationAlgorithm();
	}
}
