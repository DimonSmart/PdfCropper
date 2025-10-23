using iText.Pdfoptimizer.Exceptions;

namespace iText.Pdfoptimizer.Handlers.Util.Decoders;

public sealed class RgbColorDecoder : ColorDecoder
{
	private const int RGB_DECODE_ARRAY_LENGTH = 6;

	public RgbColorDecoder(double[] decodeArray)
		: base(decodeArray, 1.0)
	{
		if (decodeArray.Length < 6)
		{
			throw new PdfOptimizerException("Invalid decode array.");
		}
	}
}
