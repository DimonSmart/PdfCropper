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

public class RgbToCmykCsConverter : AbstractCsConverter
{
	private const string rg = "rg";

	private const string RG = "RG";

	private static readonly PdfLiteral k = new PdfLiteral("k");

	private static readonly PdfLiteral K = new PdfLiteral("K");

	private const string ICC_COLOR_SPACE_CMYK = "CMYK";

	public RgbToCmykCsConverter(CsConverterProperties csConverterProperties)
		: base(csConverterProperties)
	{
		PdfOutputIntent outputIntent = GetConverterProperties().GetOutputIntent();
		if (outputIntent != null)
		{
			string iccColorSpaceName = IccProfile.GetIccColorSpaceName(outputIntent.GetDestOutputProfile().GetBytes());
			if (!"CMYK".Equals(iccColorSpaceName))
			{
				throw new PdfOptimizerException(MessageFormatUtil.Format("Invalid output intent Icc profile color space, expected = {0}, actual = {1}.", new object[2] { "CMYK", iccColorSpaceName }));
			}
		}
	}

	protected internal override Type GetOriginalCsClass()
	{
		return typeof(Rgb);
	}

	protected internal override ColorConverter GetColorConverter()
	{
		return RgbToCmykColorConverter.GetInstance();
	}

	protected internal override ColorDecoder CreateColorDecoder(double[] decodeArray)
	{
		return new RgbColorDecoder(decodeArray);
	}

	internal override IList<PdfObject> ConvertContentStreamOperands(PdfColorSpace fillCs, PdfColorSpace strokeCs, string @operator, IList<PdfObject> operands, OptimizationSession session)
	{
		if ("RG".Equals(@operator) || "rg".Equals(@operator))
		{
			IList<PdfObject> list = ConvertOperatorParameters(operands);
			list.Add((PdfObject)(object)("RG".Equals(@operator) ? K : k));
			return list;
		}
		return base.ConvertContentStreamOperands(fillCs, strokeCs, @operator, operands, session);
	}
}
