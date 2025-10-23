using System.Collections.Generic;
using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;
using iText.Pdfoptimizer.Handlers.Imagequality.Processors.Utils;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Handlers.Util.Decoders;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors;

public class BitmapIndexer : IImageProcessor
{
	private const int MAXIMUM_NUMBER_OF_COLORS = 256;

	private const int INDEXED_BITS_PER_COMPONENTS = 8;

	public virtual PdfImageXObject ProcessImage(PdfImageXObject objectToProcess, OptimizationSession session)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Invalid comparison between Unknown and I4
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Invalid comparison between Unknown and I4
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Expected O, but got Unknown
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Expected O, but got Unknown
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Expected O, but got Unknown
		//IL_0124: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Expected O, but got Unknown
		//IL_014b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Expected O, but got Unknown
		//IL_017f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0185: Expected O, but got Unknown
		ImageType val = objectToProcess.IdentifyImageType();
		if ((int)val != 4 && (int)val != 1)
		{
			return objectToProcess;
		}
		PdfStream pdfObject = ((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject();
		if (!(((PdfDictionary)pdfObject).Get(PdfName.ColorSpace) is PdfName))
		{
			return objectToProcess;
		}
		BitmapImagePixels bitmapImagePixels = new BitmapImagePixels(objectToProcess);
		PdfArray asArray = ((PdfDictionary)pdfObject).GetAsArray(PdfName.Decode);
		if (asArray != null)
		{
			ColorDecoder colorDecoder = new ColorDecoder(asArray.ToDoubleArray(), 1.0);
			bitmapImagePixels = CsConverterUtil.ConvertBitmapImage(bitmapImagePixels, colorDecoder);
		}
		ArrayStorage arrayStorage = BuildStorageForImage(bitmapImagePixels);
		MaskColors maskColors = MaskColors.Create(((PdfDictionary)pdfObject).GetAsArray(PdfName.Mask));
		if (maskColors != null)
		{
			arrayStorage.Add(maskColors.GetMin());
			arrayStorage.Add(maskColors.GetMax());
		}
		if (arrayStorage.Size() > 256)
		{
			return objectToProcess;
		}
		BitmapImagePixels bitmapImagePixels2 = CreateIndexedImage(bitmapImagePixels, arrayStorage);
		PdfStream val2 = (PdfStream)((PdfObject)pdfObject).Clone();
		val2.SetData(bitmapImagePixels2.GetData());
		PdfArray val3 = new PdfArray();
		val3.Add((PdfObject)(object)PdfName.Indexed);
		val3.Add(((PdfDictionary)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).Get(PdfName.ColorSpace));
		BitmapImagePixels bitmapImagePixels3 = CreateColorTable(bitmapImagePixels, arrayStorage);
		val3.Add((PdfObject)new PdfNumber(bitmapImagePixels3.GetWidth() - 1));
		val3.Add((PdfObject)new PdfStream(bitmapImagePixels3.GetData(), 9));
		((PdfDictionary)val2).Put(PdfName.ColorSpace, (PdfObject)(object)val3);
		((PdfDictionary)val2).Put(PdfName.BitsPerComponent, (PdfObject)new PdfNumber(bitmapImagePixels2.GetBitsPerComponent()));
		((PdfDictionary)val2).Remove(PdfName.Decode);
		if (maskColors != null)
		{
			((PdfDictionary)val2).Put(PdfName.Mask, (PdfObject)(object)IndexMask(maskColors, arrayStorage));
		}
		return new PdfImageXObject(val2);
	}

	private static ArrayStorage BuildStorageForImage(BitmapImagePixels pixels)
	{
		ArrayStorage arrayStorage = new ArrayStorage();
		for (int i = 0; i < pixels.GetWidth(); i++)
		{
			for (int j = 0; j < pixels.GetHeight(); j++)
			{
				arrayStorage.Add(pixels.GetPixelAsLongs(i, j));
				if (arrayStorage.Size() > 256)
				{
					return arrayStorage;
				}
			}
		}
		return arrayStorage;
	}

	private static BitmapImagePixels CreateColorTable(BitmapImagePixels originalPixels, ArrayStorage storage)
	{
		BitmapImagePixels bitmapImagePixels = new BitmapImagePixels(storage.Size(), 1, 8, originalPixels.GetNumberOfComponents());
		foreach (KeyValuePair<HashableArray, int?> item in storage.GetAll())
		{
			bitmapImagePixels.SetPixel(item.Value.Value, 0, NormalizePixel(item.Key.GetArray(), originalPixels.GetMaxComponentValue()));
		}
		return bitmapImagePixels;
	}

	private static BitmapImagePixels CreateIndexedImage(BitmapImagePixels originalPixels, ArrayStorage storage)
	{
		int bitsPerComponent = Log2(storage.Size());
		BitmapImagePixels bitmapImagePixels = new BitmapImagePixels(originalPixels.GetWidth(), originalPixels.GetHeight(), bitsPerComponent, 1);
		for (int i = 0; i < originalPixels.GetWidth(); i++)
		{
			for (int j = 0; j < originalPixels.GetHeight(); j++)
			{
				bitmapImagePixels.SetPixel(i, j, new long[1] { storage.Get(originalPixels.GetPixelAsLongs(i, j)).Value });
			}
		}
		return bitmapImagePixels;
	}

	private static PdfArray IndexMask(MaskColors originalMask, ArrayStorage storage)
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		//IL_0048: Expected O, but got Unknown
		int value = storage.Get(originalMask.GetMin()).Value;
		int value2 = storage.Get(originalMask.GetMax()).Value;
		PdfArray val = new PdfArray();
		val.Add((PdfObject)new PdfNumber(value));
		val.Add((PdfObject)new PdfNumber(value2));
		return val;
	}

	private static int Log2(int value)
	{
		if (value == 1)
		{
			return 1;
		}
		value--;
		int num = 31;
		int num2 = 1073741824;
		while ((num2 & value) == 0)
		{
			num2 >>>= 1;
			num--;
		}
		return num;
	}

	private static double[] NormalizePixel(long[] originalPixel, int maxComponentValue)
	{
		double[] array = new double[originalPixel.Length];
		for (int i = 0; i < originalPixel.Length; i++)
		{
			array[i] = (double)originalPixel[i] / (double)maxComponentValue;
		}
		return array;
	}
}
