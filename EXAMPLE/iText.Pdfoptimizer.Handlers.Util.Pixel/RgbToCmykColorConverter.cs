using System;
using iText.Kernel.Pdf;

namespace iText.Pdfoptimizer.Handlers.Util.Pixel;

public sealed class RgbToCmykColorConverter : ColorConverter
{
	private const int CMYK_NUMBER_OF_COMPONENTS = 4;

	private const int RGB_NUMBER_OF_COMPONENTS = 3;

	private const int CMYK_BLACK_COMPONENT_INDEX = 3;

	private const double EPSILON = 1E-05;

	private static readonly RgbToCmykColorConverter INSTANCE = new RgbToCmykColorConverter();

	private RgbToCmykColorConverter()
	{
	}

	public static RgbToCmykColorConverter GetInstance()
	{
		return INSTANCE;
	}

	public double[] ConvertColor(double[] rgbComponents)
	{
		double num = 1.0 - Math.Max(Math.Max(rgbComponents[0], rgbComponents[1]), rgbComponents[2]);
		if (Math.Abs(num - 1.0) < 1E-05)
		{
			return new double[4] { 0.0, 0.0, 0.0, 1.0 };
		}
		double[] array = new double[4];
		for (int i = 0; i < rgbComponents.Length; i++)
		{
			array[i] = (1.0 - rgbComponents[i] - num) / (1.0 - num);
		}
		array[3] = num;
		return array;
	}

	public PdfName GetSourceColorspace()
	{
		return PdfName.DeviceRGB;
	}

	public int GetSourceNumberOfComponents()
	{
		return 3;
	}

	public PdfName GetTargetColorspace()
	{
		return PdfName.DeviceCMYK;
	}

	public int GetTargetNumberOfComponents()
	{
		return 4;
	}
}
