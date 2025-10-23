using System;
using System.Collections.Generic;
using iText.Commons.Utils;
using iText.IO.Colors;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Colorspace;
using iText.Pdfoptimizer.Exceptions;
using iText.Pdfoptimizer.Handlers.Util.Decoders;
using iText.Pdfoptimizer.Handlers.Util.Pixel;

namespace iText.Pdfoptimizer.Handlers.Converters;

public class CmykToRgbCsConverter : AbstractCsConverter
{
	private const string k = "k";

	private const string K = "K";

	private static readonly PdfLiteral rgLiteral = new PdfLiteral("rg");

	private static readonly PdfLiteral RGLiteral = new PdfLiteral("RG");

	private const string ICC_COLOR_SPACE_RGB = "RGB ";

	public CmykToRgbCsConverter(CsConverterProperties csConverterProperties)
		: base(csConverterProperties)
	{
		PdfOutputIntent outputIntent = GetConverterProperties().GetOutputIntent();
		if (outputIntent != null)
		{
			string iccColorSpaceName = IccProfile.GetIccColorSpaceName(outputIntent.GetDestOutputProfile().GetBytes());
			if (!"RGB ".Equals(iccColorSpaceName))
			{
				throw new PdfOptimizerException(MessageFormatUtil.Format("Invalid output intent Icc profile color space, expected = {0}, actual = {1}.", new object[2] { "RGB ", iccColorSpaceName }));
			}
		}
	}

	protected internal override Type GetOriginalCsClass()
	{
		return typeof(Cmyk);
	}

	protected internal override ColorConverter GetColorConverter()
	{
		return CmykToRgbColorConverter.GetInstance();
	}

	protected internal override ColorDecoder CreateColorDecoder(double[] decodeArray)
	{
		return new CmykColorDecoder(decodeArray);
	}

	internal override IList<PdfObject> ConvertContentStreamOperands(PdfColorSpace fillCs, PdfColorSpace strokeCs, string @operator, IList<PdfObject> operands, OptimizationSession session)
	{
		if ("K".Equals(@operator) || "k".Equals(@operator))
		{
			IList<PdfObject> list = ConvertOperatorParameters(operands);
			list.Add((PdfObject)(object)("K".Equals(@operator) ? RGLiteral : rgLiteral));
			return list;
		}
		return base.ConvertContentStreamOperands(fillCs, strokeCs, @operator, operands, session);
	}
}
