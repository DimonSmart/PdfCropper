using System;
using iText.Commons.Utils;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Colorspace;
using iText.Kernel.Pdf.Xobject;
using iText.Pdfoptimizer.Handlers.Imagequality.Processors.Utils;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors;

public class JpegCompressor : IImageProcessor
{
	private readonly double compressionLevel;

	public JpegCompressor(double compressionLevel)
	{
		if (compressionLevel > 1.0 || compressionLevel < 0.0)
		{
			throw new ArgumentException(MessageFormatUtil.Format("Invalid compression parameter! Value {0} is out of range [0, 1]", new object[1] { compressionLevel }));
		}
		this.compressionLevel = compressionLevel;
	}

	public virtual PdfImageXObject ProcessImage(PdfImageXObject objectToProcess, OptimizationSession session)
	{
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected O, but got Unknown
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Expected O, but got Unknown
		PdfColorSpace val = PdfColorSpace.MakeColorSpace(((PdfDictionary)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).Get(PdfName.ColorSpace));
		if (val is Cmyk)
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Color space {0} is not supported by image processor {1}. Unable to optimize image with reference {2}", PdfName.DeviceCMYK, GetType(), ((PdfObject)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).GetIndirectReference());
			return objectToProcess;
		}
		if (val is Indexed)
		{
			session.RegisterEvent(SeverityLevel.WARNING, "Color space {0} is not supported by image processor {1}. Unable to optimize image with reference {2}", PdfName.Indexed, GetType(), ((PdfObject)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).GetIndirectReference());
			return objectToProcess;
		}
		byte[] data = ImageProcessingUtil.CompressJpeg(objectToProcess.GetImageBytes(), compressionLevel);
		PdfStream val2 = (PdfStream)((PdfObject)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).Clone();
		val2.SetData(data);
		((PdfDictionary)val2).Put(PdfName.Filter, (PdfObject)(object)PdfName.DCTDecode);
		PdfArray asArray = ((PdfDictionary)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).GetAsArray(PdfName.Mask);
		if (asArray != null)
		{
			PdfStream val3 = BuildMask(asArray, new BitmapImagePixels(objectToProcess));
			((PdfDictionary)val2).Put(PdfName.Mask, (PdfObject)(object)val3);
		}
		return new PdfImageXObject(val2);
	}

	private static PdfStream BuildMask(PdfArray maskArray, BitmapImagePixels picture)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Expected O, but got Unknown
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Expected O, but got Unknown
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Expected O, but got Unknown
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Expected O, but got Unknown
		MaskColors maskColors = MaskColors.Create(maskArray);
		BitmapImagePixels bitmapImagePixels = new BitmapImagePixels(picture.GetWidth(), picture.GetHeight(), 1, 1);
		for (int i = 0; i < picture.GetWidth(); i++)
		{
			for (int j = 0; j < picture.GetHeight(); j++)
			{
				if (maskColors.IsColorMasked(picture.GetPixelAsLongs(i, j)))
				{
					bitmapImagePixels.SetPixel(i, j, new long[1] { 1L });
				}
			}
		}
		PdfStream val = new PdfStream();
		val.SetData(bitmapImagePixels.GetData());
		((PdfDictionary)val).Put(PdfName.Width, (PdfObject)new PdfNumber(bitmapImagePixels.GetWidth()));
		((PdfDictionary)val).Put(PdfName.Height, (PdfObject)new PdfNumber(bitmapImagePixels.GetHeight()));
		((PdfDictionary)val).Put(PdfName.ImageMask, (PdfObject)new PdfBoolean(true));
		((PdfDictionary)val).Put(PdfName.Type, (PdfObject)(object)PdfName.XObject);
		((PdfDictionary)val).Put(PdfName.Subtype, (PdfObject)(object)PdfName.Image);
		return val;
	}
}
