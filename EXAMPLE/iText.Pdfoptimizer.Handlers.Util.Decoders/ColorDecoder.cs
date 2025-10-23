using iText.Commons.Utils;
using iText.Pdfoptimizer.Exceptions;

namespace iText.Pdfoptimizer.Handlers.Util.Decoders;

public class ColorDecoder
{
	private readonly double[] decodeArray;

	private readonly double maxComponentValue;

	public ColorDecoder(double[] decodeArray, double maxComponentValue)
	{
		this.decodeArray = JavaUtil.ArraysCopyOf<double>(decodeArray, decodeArray.Length);
		this.maxComponentValue = maxComponentValue;
		for (byte b = 0; b < decodeArray.Length; b++)
		{
			if (decodeArray[b] < 0.0 || decodeArray[b] > maxComponentValue)
			{
				throw new PdfOptimizerException("Invalid decode array.");
			}
		}
	}

	public virtual double[] Decode(double[] color)
	{
		if (color.Length * 2 > decodeArray.Length)
		{
			throw new PdfOptimizerException("Invalid color to decode.");
		}
		double[] array = new double[color.Length];
		for (int i = 0; i < color.Length; i++)
		{
			array[i] = DecodeComponent(color[i], i);
		}
		return array;
	}

	public virtual double DecodeComponent(double colorComponent, int componentIndex)
	{
		if (componentIndex * 2 + 1 >= decodeArray.Length)
		{
			throw new PdfOptimizerException("Invalid color to decode.");
		}
		if (colorComponent < 0.0 || colorComponent > maxComponentValue)
		{
			throw new PdfOptimizerException("Invalid color to decode.");
		}
		return decodeArray[componentIndex * 2] + colorComponent * (decodeArray[componentIndex * 2 + 1] - decodeArray[componentIndex * 2]) / maxComponentValue;
	}
}
