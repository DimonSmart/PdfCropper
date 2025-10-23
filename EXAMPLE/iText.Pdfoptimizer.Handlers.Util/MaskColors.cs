using System;
using iText.Commons.Utils;
using iText.Kernel.Exceptions;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Util.Decoders;
using iText.Pdfoptimizer.Handlers.Util.Pixel;

namespace iText.Pdfoptimizer.Handlers.Util;

public sealed class MaskColors
{
	private const int MASK_ARRAY_LENGTH_MULTIPLIER = 2;

	private readonly long[] min;

	private readonly long[] max;

	private MaskColors(long[] min, long[] max)
	{
		if (min.Length != max.Length)
		{
			throw new InvalidOperationException(MessageFormatUtil.Format("Minimum and maximum masked colors have different number of components: {0} and {1}.", new object[2] { min.Length, max.Length }));
		}
		this.min = JavaUtil.ArraysCopyOf<long>(min, min.Length);
		this.max = JavaUtil.ArraysCopyOf<long>(max, max.Length);
	}

	public static MaskColors Create(PdfArray array)
	{
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		if (array == null)
		{
			return null;
		}
		if (array.Size() == 0 || array.Size() % 2 != 0)
		{
			throw new PdfException(MessageFormatUtil.Format("Mask array has invalid length {0}. It should have even positive length", new object[1] { array.Size() }));
		}
		long[] array2 = new long[array.Size() / 2];
		long[] array3 = new long[array.Size() / 2];
		for (int i = 0; i < array2.Length; i++)
		{
			array2[i] = array.GetAsNumber(2 * i).LongValue();
			array3[i] = array.GetAsNumber(2 * i + 1).LongValue();
		}
		return new MaskColors(array2, array3);
	}

	public MaskColors GetConvertedColorMask(long maximumComponentValue, ColorConverter converter)
	{
		return GetConvertedColorMask(maximumComponentValue, converter, new BlankColorDecoder());
	}

	public MaskColors GetConvertedColorMask(long maximumComponentValue, ColorConverter converter, ColorDecoder colorDecoder)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		if (min.Length != converter.GetSourceNumberOfComponents())
		{
			throw new PdfException(MessageFormatUtil.Format("Mask array does not correspond with the converter! Its length is {0} but expected length is {1}", new object[2]
			{
				2 * min.Length,
				2 * converter.GetSourceNumberOfComponents()
			}));
		}
		long[] array = ConvertPixelAsLongs(GetMin(), maximumComponentValue, converter, colorDecoder);
		long[] array2 = ConvertPixelAsLongs(GetMax(), maximumComponentValue, converter, colorDecoder);
		if (CompareArrays(array, array2) <= 0)
		{
			return new MaskColors(array, array2);
		}
		return new MaskColors(array2, array);
	}

	public long[] GetMin()
	{
		return JavaUtil.ArraysCopyOf<long>(min, min.Length);
	}

	public long[] GetMax()
	{
		return JavaUtil.ArraysCopyOf<long>(max, max.Length);
	}

	public bool IsColorMasked(long[] color)
	{
		if (max.Length != color.Length)
		{
			return false;
		}
		for (int i = 0; i < max.Length; i++)
		{
			if (color[i] < min[i] || color[i] > max[i])
			{
				return false;
			}
		}
		return true;
	}

	public PdfArray ToPdfArray()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Expected O, but got Unknown
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Expected O, but got Unknown
		PdfArray val = new PdfArray();
		for (int i = 0; i < min.Length; i++)
		{
			val.Add((PdfObject)new PdfNumber((double)min[i]));
			val.Add((PdfObject)new PdfNumber((double)max[i]));
		}
		return val;
	}

	private static long[] ConvertPixelAsLongs(long[] components, long maximumComponentValue, ColorConverter converter, ColorDecoder colorDecoder)
	{
		double[] array = new double[components.Length];
		for (int i = 0; i < components.Length; i++)
		{
			array[i] = (double)components[i] / (double)maximumComponentValue;
		}
		double[] array2 = converter.ConvertColor(colorDecoder.Decode(array));
		long[] array3 = new long[array2.Length];
		for (int j = 0; j < array3.Length; j++)
		{
			array3[j] = (long)MathematicUtil.Round(array2[j] * (double)maximumComponentValue);
		}
		return array3;
	}

	private static long CompareArrays(long[] array1, long[] array2)
	{
		for (int i = 0; i < array1.Length; i++)
		{
			if (array1[i] != array2[i])
			{
				return array1[i] - array2[i];
			}
		}
		return 0L;
	}
}
