using System;
using iText.Commons.Utils;
using iText.Pdfoptimizer.Exceptions;

namespace iText.Pdfoptimizer.Handlers.Util.Decoders;

public sealed class IndexedColorDecoder : ColorDecoder
{
	private const int INDEXED_DECODE_ARRAY_LENGTH = 2;

	public IndexedColorDecoder(double[] decodeArray, int bitsPerComponent)
		: base(decodeArray, Math.Pow(2.0, bitsPerComponent) - 1.0)
	{
		if (decodeArray.Length != 2)
		{
			throw new PdfOptimizerException("Invalid decode array.");
		}
	}

	public override double[] Decode(double[] color)
	{
		double[] array = base.Decode(color);
		array[0] = MathematicUtil.Round(array[0]);
		return array;
	}

	public override double DecodeComponent(double colorComponent, int componentIndex)
	{
		return MathematicUtil.Round(base.DecodeComponent(colorComponent, componentIndex));
	}
}
