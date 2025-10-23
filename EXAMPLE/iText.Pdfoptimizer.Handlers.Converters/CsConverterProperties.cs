using iText.Kernel.Pdf;

namespace iText.Pdfoptimizer.Handlers.Converters;

public class CsConverterProperties
{
	private readonly ColorConversionMode conversionMode;

	private PdfOutputIntent outputIntent;

	public CsConverterProperties(ColorConversionMode conversionMode)
	{
		this.conversionMode = conversionMode;
	}

	public virtual CsConverterProperties SetOutputIntent(PdfOutputIntent outputIntent)
	{
		this.outputIntent = outputIntent;
		return this;
	}

	public virtual PdfOutputIntent GetOutputIntent()
	{
		return outputIntent;
	}

	public virtual ColorConversionMode GetConversionMode()
	{
		return conversionMode;
	}
}
