using System;
using System.Collections.Generic;
using iText.Commons.Utils;
using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using iText.Pdfoptimizer.Handlers.Imagequality.Processors;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Report.Message;
using iText.Pdfoptimizer.Util;
using iText.Pdfoptimizer.Util.Traversing;

namespace iText.Pdfoptimizer.Handlers;

public class ImageQualityOptimizer : AbstractOptimizationHandler
{
	private readonly IDictionary<ImageType, IImageProcessor> imageProcessors;

	private PdfImageXObjectPredicate predicate;

	public ImageQualityOptimizer()
		: this(new Dictionary<ImageType, IImageProcessor>())
	{
	}

	public ImageQualityOptimizer(IDictionary<ImageType, IImageProcessor> processors)
	{
		if (processors == null)
		{
			imageProcessors = new Dictionary<ImageType, IImageProcessor>();
		}
		else
		{
			imageProcessors = new Dictionary<ImageType, IImageProcessor>(processors);
		}
	}

	public virtual ImageQualityOptimizer SetJpegProcessor(IImageProcessor processor)
	{
		imageProcessors.Put((ImageType)0, processor);
		return this;
	}

	public virtual ImageQualityOptimizer SetJpeg2000Processor(IImageProcessor processor)
	{
		imageProcessors.Put((ImageType)7, processor);
		return this;
	}

	public virtual ImageQualityOptimizer SetJBig2Processor(IImageProcessor processor)
	{
		imageProcessors.Put((ImageType)8, processor);
		return this;
	}

	public virtual ImageQualityOptimizer SetTiffProcessor(IImageProcessor processor)
	{
		imageProcessors.Put((ImageType)4, processor);
		return this;
	}

	public virtual ImageQualityOptimizer SetPngProcessor(IImageProcessor processor)
	{
		imageProcessors.Put((ImageType)1, processor);
		return this;
	}

	public virtual ImageQualityOptimizer SetPredicate(PdfImageXObjectPredicate predicate)
	{
		this.predicate = predicate;
		return this;
	}

	public virtual PdfImageXObjectPredicate GetPredicate()
	{
		PdfImageXObjectPredicate defaultImagePredicate = predicate;
		if (defaultImagePredicate == null)
		{
			defaultImagePredicate = GetDefaultImagePredicate();
		}
		return defaultImagePredicate;
	}

	public virtual IDictionary<ImageType, IImageProcessor> GetImageProcessors()
	{
		return JavaCollectionsUtil.UnmodifiableMap<ImageType, IImageProcessor>(imageProcessors);
	}

	protected internal override void OptimizePdf(PdfDocument document, OptimizationSession session)
	{
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Expected O, but got Unknown
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Expected O, but got Unknown
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		PdfImageXObjectPredicate defaultImagePredicate = predicate;
		if (defaultImagePredicate == null)
		{
			defaultImagePredicate = GetDefaultImagePredicate();
		}
		IList<PdfObject> list = DocumentStructureUtils.Search(document, defaultImagePredicate);
		IDictionary<PdfObject, PdfObject> dictionary = new Dictionary<PdfObject, PdfObject>();
		foreach (PdfStream item in list)
		{
			PdfImageXObject val = new PdfImageXObject(item);
			IImageProcessor imageProcessor = imageProcessors.Get(val.IdentifyImageType());
			if (imageProcessor == null)
			{
				continue;
			}
			try
			{
				PdfImageXObject val2 = imageProcessor.ProcessImage(val, session);
				PdfStream pdfObject = ((PdfObjectWrapper<PdfStream>)(object)val).GetPdfObject();
				PdfStream pdfObject2 = ((PdfObjectWrapper<PdfStream>)(object)val2).GetPdfObject();
				bool flag = WasImageOptimized(val, val2, document);
				if (pdfObject != pdfObject2 && flag)
				{
					session.RegisterEvent(SeverityLevel.INFO, "Image with reference {0} was optimized.", ((PdfObject)pdfObject).GetIndirectReference());
					((PdfObject)pdfObject2).MakeIndirect(document);
					dictionary.Put((PdfObject)(object)pdfObject, (PdfObject)(object)pdfObject2);
				}
				else if (!flag)
				{
					session.RegisterEvent(SeverityLevel.INFO, "Image with reference {0} has increased size after optimization, the original image will be saved.", ((PdfObject)pdfObject).GetIndirectReference());
				}
			}
			catch (Exception)
			{
				session.RegisterEvent(SeverityLevel.ERROR, "Unable to optimize image with reference {0} of type {1}", ((PdfObject)((PdfObjectWrapper<PdfStream>)(object)val).GetPdfObject()).GetIndirectReference(), val.IdentifyImageType());
			}
		}
		DocumentStructureUtils.Traverse(document, new ReplaceObjectsAction(dictionary));
	}

	private static PdfImageXObjectPredicate GetDefaultImagePredicate()
	{
		return new PdfImageXObjectPredicate();
	}

	private static bool WasImageOptimized(PdfImageXObject imageXObject, PdfImageXObject optimizedImageXObject, PdfDocument pdfDocument)
	{
		PdfStream pdfObject = ((PdfObjectWrapper<PdfStream>)(object)imageXObject).GetPdfObject();
		long num = ((!((PdfObject)pdfObject).IsModified() && ((PdfObject)pdfObject).GetIndirectReference() != null && ((PdfDictionary)pdfObject).GetAsNumber(PdfName.Length) != null) ? ((PdfDictionary)((PdfObjectWrapper<PdfStream>)(object)imageXObject).GetPdfObject()).GetAsNumber(PdfName.Length).LongValue() : PdfObjectSizeCalculationUtil.CalculateImageStreamLengthInBytes(imageXObject, pdfDocument));
		return PdfObjectSizeCalculationUtil.CalculateImageStreamLengthInBytes(optimizedImageXObject, pdfDocument) < num;
	}
}
