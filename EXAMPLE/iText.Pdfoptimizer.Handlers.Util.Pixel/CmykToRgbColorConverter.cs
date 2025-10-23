using iText.Kernel.Pdf;

namespace iText.Pdfoptimizer.Handlers.Util.Pixel;

public sealed class CmykToRgbColorConverter : ColorConverter
{
	private const int CMYK_NUMBER_OF_COMPONENTS = 4;

	private const int RGB_NUMBER_OF_COMPONENTS = 3;

	private const int CMYK_BLACK_COMPONENT_INDEX = 3;

	private static readonly CmykToRgbColorConverter INSTANCE = new CmykToRgbColorConverter();

	private CmykToRgbColorConverter()
	{
	}

	public static CmykToRgbColorConverter GetInstance()
	{
		return INSTANCE;
	}

	public double[] ConvertColor(double[] cmykComponents)
	{
		double[] array = new double[3];
		for (int i = 0; i < 3; i++)
		{
			array[i] = (1.0 - cmykComponents[i]) * (1.0 - cmykComponents[3]);
		}
		return array;
	}

	public PdfName GetSourceColorspace()
	{
		return PdfName.DeviceCMYK;
	}

	public int GetSourceNumberOfComponents()
	{
		return 4;
	}

	public PdfName GetTargetColorspace()
	{
		return PdfName.DeviceRGB;
	}

	public int GetTargetNumberOfComponents()
	{
		return 3;
	}
}
