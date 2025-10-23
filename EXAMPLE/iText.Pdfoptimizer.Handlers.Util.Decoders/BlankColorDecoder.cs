namespace iText.Pdfoptimizer.Handlers.Util.Decoders;

public sealed class BlankColorDecoder : ColorDecoder
{
	public BlankColorDecoder()
		: base(new double[0], 0.0)
	{
	}

	public override double[] Decode(double[] color)
	{
		return color;
	}

	public override double DecodeComponent(double colorComponent, int componentIndex)
	{
		return colorComponent;
	}
}
