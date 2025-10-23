using System;
using iText.Commons.Utils;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Colorspace;
using iText.Kernel.Pdf.Xobject;

namespace iText.Pdfoptimizer.Handlers.Util;

public class BitmapImagePixels
{
	private const int BITS_IN_BYTE = 8;

	private const int DEFAULT_BITS_PER_COMPONENT = 8;

	private const int BYTE_WITH_LEADING_BIT = 128;

	private const int BITS_IN_BYTE_LOG = 3;

	private const int BIT_MASK = 7;

	private readonly int width;

	private readonly int bitsInRow;

	private readonly int height;

	private readonly int bitsPerComponent;

	private readonly int maxComponentValue;

	private readonly int numberOfComponents;

	private readonly byte[] data;

	public BitmapImagePixels(int width, int height, int bitsPerComponent, int numberOfComponents)
		: this(width, height, bitsPerComponent, numberOfComponents, null)
	{
	}

	public BitmapImagePixels(PdfImageXObject image)
		: this((int)MathematicUtil.Round((double)((PdfXObject)image).GetWidth()), (int)MathematicUtil.Round((double)((PdfXObject)image).GetHeight()), ObtainBitsPerComponent(image), ObtainNumberOfComponents(image), ((PdfObjectWrapper<PdfStream>)(object)image).GetPdfObject().GetBytes())
	{
	}

	public BitmapImagePixels(int width, int height, int bitsPerComponent, int numberOfComponents, byte[] data)
	{
		this.width = width;
		this.height = height;
		this.bitsPerComponent = bitsPerComponent;
		maxComponentValue = (1 << this.bitsPerComponent) - 1;
		this.numberOfComponents = numberOfComponents;
		int num = width * bitsPerComponent * numberOfComponents;
		if (num % 8 != 0)
		{
			num += 8 - (num & 7);
		}
		bitsInRow = num;
		if (data == null)
		{
			this.data = new byte[bitsInRow * height >>> 3];
			return;
		}
		int num2 = bitsInRow * height;
		int num3 = data.Length * 8;
		if (num2 != num3)
		{
			throw new ArgumentException(MessageFormatUtil.Format("Invalid data length, expected length = {0}, actual length = {1}", new object[2] { num2, num3 }));
		}
		this.data = JavaUtil.ArraysCopyOf<byte>(data, data.Length);
	}

	public virtual double[] GetPixel(int x, int y)
	{
		long[] pixelAsLongs = GetPixelAsLongs(x, y);
		double[] array = new double[pixelAsLongs.Length];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = (double)pixelAsLongs[i] / (double)maxComponentValue;
		}
		return array;
	}

	public virtual long[] GetPixelAsLongs(int x, int y)
	{
		CheckCoordinates(x, y);
		long[] array = new long[numberOfComponents];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ReadNumber(y * bitsInRow + x * bitsPerComponent * numberOfComponents + i * bitsPerComponent);
		}
		return array;
	}

	public virtual void SetPixel(int x, int y, double[] value)
	{
		long[] array = new long[value.Length];
		for (int i = 0; i < value.Length; i++)
		{
			array[i] = (long)MathematicUtil.Round(value[i] * (double)maxComponentValue);
		}
		SetPixel(x, y, array);
	}

	public virtual void SetPixel(int x, int y, long[] value)
	{
		CheckCoordinates(x, y);
		CheckPixel(value);
		for (int i = 0; i < value.Length; i++)
		{
			WriteNumber(value[i], y * bitsInRow + x * bitsPerComponent * numberOfComponents + i * bitsPerComponent);
		}
	}

	public virtual int GetWidth()
	{
		return width;
	}

	public virtual int GetHeight()
	{
		return height;
	}

	public virtual int GetBitsPerComponent()
	{
		return bitsPerComponent;
	}

	public virtual int GetNumberOfComponents()
	{
		return numberOfComponents;
	}

	public virtual byte[] GetData()
	{
		return data;
	}

	public virtual int GetMaxComponentValue()
	{
		return maxComponentValue;
	}

	private long ReadNumber(int index)
	{
		long num = 0L;
		for (int i = 0; i < bitsPerComponent; i++)
		{
			num = (num << 1) + BooleanToInt(GetBit(index + i));
		}
		return num;
	}

	private void WriteNumber(long number, int index)
	{
		for (int i = 0; i < bitsPerComponent; i++)
		{
			int num = 1 << bitsPerComponent - i - 1;
			SetBit(index + i, (number & num) != 0);
		}
	}

	private bool GetBit(int index)
	{
		return (data[index >>> 3] & 0xFF & (128 >>> (index & 7))) != 0;
	}

	private void SetBit(int index, bool value)
	{
		if (value)
		{
			data[index >>> 3] |= (byte)(128 >>> (index & 7));
		}
		else
		{
			data[index >>> 3] &= (byte)(~(128 >>> (index & 7)));
		}
	}

	private void CheckCoordinates(int x, int y)
	{
		if (x < 0 || x >= width || y < 0 || y > height)
		{
			throw new ArgumentException(MessageFormatUtil.Format("Pixel ({0}, {1}) is out of borders of the picture with parameter {2} x {3}", new object[4] { x, y, width, height }));
		}
	}

	private void CheckPixel(long[] pixel)
	{
		if (pixel.Length != numberOfComponents)
		{
			throw new ArgumentException(MessageFormatUtil.Format("Length of pixel array ({0}) should match number of components ({1})", new object[2] { pixel.Length, numberOfComponents }));
		}
		for (int i = 0; i < pixel.Length; i++)
		{
			if (pixel[i] < 0)
			{
				pixel[i] = 0L;
			}
			if (pixel[i] > maxComponentValue)
			{
				pixel[i] = maxComponentValue;
			}
		}
	}

	private static int ObtainBitsPerComponent(PdfImageXObject objectToProcess)
	{
		PdfNumber asNumber = ((PdfDictionary)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).GetAsNumber(PdfName.BitsPerComponent);
		if (asNumber == null)
		{
			return 8;
		}
		return asNumber.IntValue();
	}

	private static int ObtainNumberOfComponents(PdfImageXObject objectToProcess)
	{
		return PdfColorSpace.MakeColorSpace(((PdfDictionary)((PdfObjectWrapper<PdfStream>)(object)objectToProcess).GetPdfObject()).Get(PdfName.ColorSpace)).GetNumberOfComponents();
	}

	private static int BooleanToInt(bool value)
	{
		return value ? 1 : 0;
	}
}
