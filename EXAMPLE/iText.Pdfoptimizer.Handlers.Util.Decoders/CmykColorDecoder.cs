using iText.Pdfoptimizer.Exceptions;

namespace iText.Pdfoptimizer.Handlers.Util.Decoders;

public sealed class CmykColorDecoder : ColorDecoder
{
	private const int CMYK_DECODE_ARRAY_LENGTH = 8;

	public CmykColorDecoder(double[] decodeArray)
		: base(decodeArray, 1.0)
	{
		if (decodeArray.Length < 8)
		{
			throw new PdfOptimizerException("Invalid decode array.");
		}
	}
}
