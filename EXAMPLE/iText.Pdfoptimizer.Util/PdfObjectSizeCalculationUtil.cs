using System.IO;
using iText.IO.Source;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Xobject;

namespace iText.Pdfoptimizer.Util;

public sealed class PdfObjectSizeCalculationUtil
{
	private PdfObjectSizeCalculationUtil()
	{
	}

	public static long CalculateImageStreamLengthInBytes(PdfImageXObject imageXObject, PdfDocument pdfDocument)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		PdfOutputStream val = new PdfOutputStream((Stream)new IdleOutputStream());
		PdfStream val2 = CopyImageStream(((PdfObjectWrapper<PdfStream>)(object)imageXObject).GetPdfObject(), pdfDocument);
		val.Write((PdfObject)(object)val2);
		return ((PdfDictionary)val2).GetAsNumber(PdfName.Length).LongValue();
	}

	private static PdfStream CopyImageStream(PdfStream stream, PdfDocument pdfDocument)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		byte[] bytes = stream.GetBytes(false);
		int num = stream.GetCompressionLevel();
		if (num == int.MinValue)
		{
			num = ((pdfDocument == null) ? (-1) : pdfDocument.GetWriter().GetCompressionLevel());
		}
		if (((PdfDictionary)stream).ContainsKey(PdfName.Filter))
		{
			num = int.MinValue;
		}
		PdfStream val = new PdfStream(num);
		if (val.GetOutputStream() != null && ((HighPrecisionOutputStream<PdfOutputStream>)(object)val.GetOutputStream()).GetOutputStream() is ByteArrayOutputStream)
		{
			((ByteArrayOutputStream)((HighPrecisionOutputStream<PdfOutputStream>)(object)val.GetOutputStream()).GetOutputStream()).AssignBytes(bytes, bytes.Length);
		}
		PdfObject val2 = ((PdfDictionary)stream).Get(PdfName.Filter);
		if (val2 != null)
		{
			((PdfDictionary)val).Put(PdfName.Filter, val2);
		}
		PdfObject val3 = ((PdfDictionary)stream).Get(PdfName.DecodeParms);
		if (val3 != null)
		{
			((PdfDictionary)val).Put(PdfName.DecodeParms, val3);
		}
		return val;
	}
}
