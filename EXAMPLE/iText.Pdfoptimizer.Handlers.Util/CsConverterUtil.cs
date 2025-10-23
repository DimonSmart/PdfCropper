using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Colorspace;
using iText.Pdfoptimizer.Handlers.Util.Decoders;
using iText.Pdfoptimizer.Handlers.Util.Pixel;

namespace iText.Pdfoptimizer.Handlers.Util;

public sealed class CsConverterUtil
{
	private const int INDEXED_HIVAL_INDEX = 2;

	private const int INDEXED_COLOR_TABLE_INDEX = 3;

	private const int INDEXED_COLOR_SPACE_ARRAY_LENGTH = 4;

	private const int INDEXED_BITS_PER_COMPONENTS = 8;

	private CsConverterUtil()
	{
	}

	public static BitmapImagePixels ConvertBitmapImage(BitmapImagePixels imagePixels, ColorConverter converter)
	{
		return ConvertBitmapImage(imagePixels, converter, new BlankColorDecoder());
	}

	public static BitmapImagePixels ConvertBitmapImage(BitmapImagePixels imagePixels, ColorDecoder colorDecoder)
	{
		return ConvertBitmapImage(imagePixels, null, colorDecoder);
	}

	public static BitmapImagePixels ConvertBitmapImage(BitmapImagePixels imagePixels, ColorConverter converter, ColorDecoder colorDecoder)
	{
		BitmapImagePixels bitmapImagePixels = new BitmapImagePixels(imagePixels.GetWidth(), imagePixels.GetHeight(), imagePixels.GetBitsPerComponent(), converter?.GetTargetNumberOfComponents() ?? imagePixels.GetNumberOfComponents());
		for (int i = 0; i < imagePixels.GetWidth(); i++)
		{
			for (int j = 0; j < imagePixels.GetHeight(); j++)
			{
				double[] array = colorDecoder.Decode(imagePixels.GetPixel(i, j));
				bitmapImagePixels.SetPixel(i, j, (converter == null) ? array : converter.ConvertColor(array));
			}
		}
		return bitmapImagePixels;
	}

	public static BitmapImagePixels ExtractColorTableOfIndexedImage(Indexed indexedCs)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		PdfArray val = (PdfArray)((PdfObjectWrapper<PdfObject>)(object)indexedCs).GetPdfObject();
		if (val.Size() != 4)
		{
			return null;
		}
		PdfObject val2 = val.Get(3);
		byte[] array;
		if (10 == val2.GetObjectType())
		{
			array = ((PdfString)val2).GetValueBytes();
		}
		else
		{
			if (9 != val2.GetObjectType())
			{
				return null;
			}
			array = ((PdfStream)val2).GetBytes();
		}
		PdfNumber asNumber = val.GetAsNumber(2);
		if (asNumber == null || asNumber.IntValue() < 0)
		{
			return null;
		}
		int num = asNumber.IntValue();
		if (array.Length != indexedCs.GetBaseCs().GetNumberOfComponents() * (num + 1))
		{
			return null;
		}
		return new BitmapImagePixels(num + 1, 1, 8, indexedCs.GetBaseCs().GetNumberOfComponents(), array);
	}
}
