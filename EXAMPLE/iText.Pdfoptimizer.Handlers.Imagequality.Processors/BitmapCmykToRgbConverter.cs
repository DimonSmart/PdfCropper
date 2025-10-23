using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Colorspace;
using iText.Kernel.Pdf.Xobject;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Handlers.Util.Decoders;
using iText.Pdfoptimizer.Handlers.Util.Pixel;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors;

public class BitmapCmykToRgbConverter : IImageProcessor
{
	public virtual PdfImageXObject ProcessImage(PdfImageXObject objectToProcess, OptimizationSession session)
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected O, but got Unknown
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Expected O, but got Unknown
		PdfStream pdfObject = ((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject();
		if (!(PdfColorSpace.MakeColorSpace(((PdfDictionary)pdfObject).Get(PdfName.ColorSpace)) is Cmyk))
		{
			return objectToProcess;
		}
		BitmapImagePixels bitmapImagePixels = new BitmapImagePixels(objectToProcess);
		PdfArray asArray = ((PdfDictionary)pdfObject).GetAsArray(PdfName.Decode);
		ColorDecoder colorDecoder = ((asArray != null) ? ((ColorDecoder)new CmykColorDecoder(asArray.ToDoubleArray())) : ((ColorDecoder)new BlankColorDecoder()));
		BitmapImagePixels bitmapImagePixels2 = CsConverterUtil.ConvertBitmapImage(bitmapImagePixels, CmykToRgbColorConverter.GetInstance(), colorDecoder);
		PdfStream val = (PdfStream)((PdfObject)pdfObject).Clone();
		val.SetData(bitmapImagePixels2.GetData());
		((PdfDictionary)val).Put(PdfName.ColorSpace, (PdfObject)(object)PdfName.DeviceRGB);
		((PdfDictionary)val).Remove(PdfName.Decode);
		PdfArray asArray2 = ((PdfDictionary)pdfObject).GetAsArray(PdfName.Mask);
		if (asArray2 != null)
		{
			PdfArray val2 = MaskColors.Create(asArray2).GetConvertedColorMask(bitmapImagePixels.GetMaxComponentValue(), CmykToRgbColorConverter.GetInstance(), colorDecoder).ToPdfArray();
			((PdfDictionary)val).Put(PdfName.Mask, (PdfObject)(object)val2);
		}
		return new PdfImageXObject(val);
	}
}
