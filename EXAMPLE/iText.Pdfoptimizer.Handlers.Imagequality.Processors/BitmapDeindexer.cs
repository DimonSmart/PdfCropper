using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Colorspace;
using iText.Kernel.Pdf.Xobject;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Handlers.Util.Decoders;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors;

public class BitmapDeindexer : IImageProcessor
{
	private const int INDEXED_BASE_INDEX = 1;

	private const int MASK_ARRAY_NUMBER_OF_COMPONENTS = 2;

	public virtual PdfImageXObject ProcessImage(PdfImageXObject objectToProcess, OptimizationSession session)
	{
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_00f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Expected O, but got Unknown
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Expected O, but got Unknown
		//IL_0174: Unknown result type (might be due to invalid IL or missing references)
		//IL_017a: Expected O, but got Unknown
		PdfStream pdfObject = ((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject();
		PdfColorSpace val = PdfColorSpace.MakeColorSpace(((PdfDictionary)pdfObject).Get(PdfName.ColorSpace));
		if (!(val is Indexed) || 1 != ((PdfObjectWrapper<PdfObject>)(object)val).GetPdfObject().GetObjectType())
		{
			return objectToProcess;
		}
		BitmapImagePixels bitmapImagePixels = CsConverterUtil.ExtractColorTableOfIndexedImage((Indexed)val);
		if (bitmapImagePixels == null)
		{
			return objectToProcess;
		}
		BitmapImagePixels bitmapImagePixels2 = new BitmapImagePixels(objectToProcess);
		BitmapImagePixels bitmapImagePixels3 = new BitmapImagePixels(bitmapImagePixels2.GetWidth(), bitmapImagePixels2.GetHeight(), bitmapImagePixels.GetBitsPerComponent(), bitmapImagePixels.GetNumberOfComponents());
		PdfArray asArray = ((PdfDictionary)pdfObject).GetAsArray(PdfName.Decode);
		ColorDecoder colorDecoder = ((asArray != null) ? ((ColorDecoder)new IndexedColorDecoder(asArray.ToDoubleArray(), bitmapImagePixels2.GetBitsPerComponent())) : ((ColorDecoder)new BlankColorDecoder()));
		for (int i = 0; i < bitmapImagePixels3.GetWidth(); i++)
		{
			for (int j = 0; j < bitmapImagePixels3.GetHeight(); j++)
			{
				int x = (int)colorDecoder.DecodeComponent(bitmapImagePixels2.GetPixelAsLongs(i, j)[0], 0);
				bitmapImagePixels3.SetPixel(i, j, bitmapImagePixels.GetPixel(x, 0));
			}
		}
		PdfStream val2 = (PdfStream)((PdfObject)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).Clone();
		val2.SetData(bitmapImagePixels3.GetData());
		((PdfDictionary)val2).Put(PdfName.ColorSpace, ((PdfDictionary)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).GetAsArray(PdfName.ColorSpace).Get(1));
		((PdfDictionary)val2).Put(PdfName.BitsPerComponent, (PdfObject)new PdfNumber(bitmapImagePixels3.GetBitsPerComponent()));
		((PdfDictionary)val2).Remove(PdfName.Decode);
		PdfArray val3 = DeindexMask(objectToProcess, bitmapImagePixels);
		if (val3 != null)
		{
			((PdfDictionary)val2).Put(PdfName.Mask, (PdfObject)(object)val3);
		}
		return new PdfImageXObject(val2);
	}

	private static PdfArray DeindexMask(PdfImageXObject originalImage, BitmapImagePixels colorTable)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Expected O, but got Unknown
		PdfArray asArray = ((PdfDictionary)((PdfObjectWrapper<PdfStream>)(object)originalImage).GetPdfObject()).GetAsArray(PdfName.Mask);
		if (asArray == null || asArray.Size() != 2)
		{
			return null;
		}
		PdfArray val = new PdfArray();
		PdfNumber asNumber = asArray.GetAsNumber(0);
		PdfNumber asNumber2 = asArray.GetAsNumber(1);
		long[] pixelAsLongs = colorTable.GetPixelAsLongs(asNumber.IntValue(), 0);
		long[] pixelAsLongs2 = colorTable.GetPixelAsLongs(asNumber2.IntValue(), 0);
		for (int i = 0; i < pixelAsLongs2.Length; i++)
		{
			val.Add((PdfObject)new PdfNumber((double)pixelAsLongs[i]));
			val.Add((PdfObject)new PdfNumber((double)pixelAsLongs2[i]));
		}
		return val;
	}
}
