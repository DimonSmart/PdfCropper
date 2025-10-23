using System;
using System.Collections.Generic;
using iText.Commons.Utils;
using iText.IO.Image;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Colorspace;
using iText.Kernel.Pdf.Xobject;
using iText.Pdfoptimizer.Exceptions;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Handlers.Util.Decoders;
using iText.Pdfoptimizer.Handlers.Util.Pixel;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Handlers.Converters;

public abstract class AbstractCsConverter : ICsConverter
{
	private const int INDEXED_BASE_INDEX = 1;

	private const int INDEXED_COLOR_TABLE_INDEX = 3;

	private const int INDEXED_COLOR_SPACE_ARRAY_LENGTH = 4;

	private const int SEPARATION_COLOR_SPACE_ARRAY_LENGTH = 4;

	private const int SEPARATION_ALTERNATIVE_INDEX = 2;

	private const int DEVICEN_COLOR_SPACE_ARRAY_LENGTH = 4;

	private const int DEVICEN_ALTERNATIVE_INDEX = 2;

	private const int COLORED_TILING_PATTERN_TYPE = 1;

	private const int COLORED_TILING_PAINT_TYPE = 1;

	private const int SHADING_PATTERN_TYPE = 2;

	private const int PATTERN_COLOR_SPACE_ARRAY_LENGTH = 2;

	private const string sc = "sc";

	private const string SC = "SC";

	private const string scn = "scn";

	private const string SCN = "SCN";

	private const string EI = "EI";

	private static readonly PdfLiteral Do = new PdfLiteral("Do");

	private static readonly PdfName Matte = new PdfName("Matte");

	private readonly CsConverterProperties properties;

	public AbstractCsConverter(CsConverterProperties csConverterProperties)
	{
		properties = csConverterProperties;
	}

	public virtual PdfImageXObject ConvertImageCs(PdfImageXObject imageToConvert, OptimizationSession session)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Invalid comparison between I4 and Unknown
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0090: Invalid comparison between I4 and Unknown
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0156: Expected O, but got Unknown
		//IL_00bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_0181: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Expected O, but got Unknown
		//IL_0186: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Expected O, but got Unknown
		PdfStream pdfObject = ((PdfObjectWrapper<PdfStream>)(object)imageToConvert).GetPdfObject();
		PdfColorSpace val = PdfColorSpace.MakeColorSpace(((PdfDictionary)pdfObject).Get(PdfName.ColorSpace));
		Type originalCsClass = GetOriginalCsClass();
		if (val is Separation)
		{
			if (((object)((Separation)val).GetBaseCs()).GetType().Equals(originalCsClass))
			{
				LogErrorOrThrowException(session, "Unable to convert separation color space.", "Can't convert separation color space, PDF\\A conformance will be compromised.");
			}
		}
		else if (val is DeviceN && ((object)((DeviceN)val).GetBaseCs()).GetType().Equals(originalCsClass))
		{
			LogErrorOrThrowException(session, "Unable to convert deviceN color space.", "Can't convert deviceN color space, PDF\\A conformance will be compromised.");
		}
		ImageType val2 = imageToConvert.IdentifyImageType();
		if (1 != (int)val2 && 4 != (int)val2)
		{
			if (((object)val).GetType().Equals(originalCsClass))
			{
				LogErrorOrThrowException(session, "Unable to convert device color space for non-bitmap image.", "Can't convert original device color space for non-bitmap image, PDF\\A conformance will be compromised.");
			}
			else if (val is Indexed && ((object)((Indexed)val).GetBaseCs()).GetType().Equals(originalCsClass))
			{
				LogErrorOrThrowException(session, "Unable to convert indexed color space based on device color space for non-bitmap image.", "Can't convert indexed color space based on original color space for non-bitmap image, PDF\\A conformance will be compromised.");
			}
			return imageToConvert;
		}
		PdfStream asStream = ((PdfDictionary)pdfObject).GetAsStream(PdfName.SMask);
		if (asStream != null && ((PdfDictionary)asStream).GetAsArray(Matte) != null)
		{
			session.RegisterEvent(SeverityLevel.INFO, MessageFormatUtil.Format("Unable to convert color space of the image with reference {0} which contain sMask with Matte field.", new object[1] { ((PdfObject)pdfObject).GetIndirectReference() }));
			return imageToConvert;
		}
		if (((object)val).GetType().Equals(originalCsClass))
		{
			return ConvertImageWithOriginalCs(imageToConvert);
		}
		if (val is Indexed)
		{
			Indexed val3 = (Indexed)val;
			if (((object)val3.GetBaseCs()).GetType().Equals(originalCsClass))
			{
				if (!AttemptToConvertIndexedCsBasedOnOriginalCs(val3))
				{
					return imageToConvert;
				}
				return new PdfImageXObject((PdfStream)((PdfObject)((PdfObjectWrapper<PdfStream>)(object)imageToConvert).GetPdfObject()).Clone());
			}
		}
		return imageToConvert;
	}

	public virtual PdfStream ConvertContentStream(PdfStream stream, PdfResources externalResources, OptimizationSession session)
	{
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Expected O, but got Unknown
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Expected O, but got Unknown
		ICollection<PdfIndirectReference> collection = null;
		try
		{
			collection = (ICollection<PdfIndirectReference>)session.GetStoredValue("converted-content-streams-key");
		}
		catch (InvalidCastException)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Converted content streams are not accessible!");
			return stream;
		}
		if (((PdfObject)stream).GetIndirectReference() == null || collection.Contains(((PdfObject)stream).GetIndirectReference()))
		{
			return stream;
		}
		collection.Add(((PdfObject)stream).GetIndirectReference());
		Stack<PdfDictionary> stack;
		try
		{
			stack = (Stack<PdfDictionary>)session.GetStoredValue("resources-for-conversion-key");
		}
		catch (InvalidCastException)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Resources for conversion are not accessible!");
			return stream;
		}
		PdfResources val;
		if (externalResources == null)
		{
			PdfDictionary asDictionary = ((PdfDictionary)stream).GetAsDictionary(PdfName.Resources);
			if (asDictionary == null)
			{
				val = new PdfResources(new PdfDictionary());
			}
			else
			{
				val = new PdfResources(asDictionary);
				stack.Push(asDictionary);
			}
		}
		else
		{
			val = externalResources;
			stack.Push(((PdfObjectWrapper<PdfDictionary>)(object)val).GetPdfObject());
		}
		Stack<PdfResources> currentResources = GetCurrentResources(session);
		currentResources?.Push(val);
		PdfCanvasCsConverter pdfCanvasCsConverter = new PdfCanvasCsConverter(((PdfObject)stream).GetIndirectReference().GetDocument(), this, session);
		((PdfCanvasProcessor)pdfCanvasCsConverter).ProcessContent(stream.GetBytes(), val);
		PdfStream contentStream = pdfCanvasCsConverter.GetCanvas().GetContentStream();
		foreach (KeyValuePair<PdfName, PdfObject> item in ((PdfDictionary)stream).EntrySet())
		{
			PdfName key = item.Key;
			if (!((object)PdfName.Filter).Equals((object)key) && !((object)PdfName.Length).Equals((object)key) && !((object)PdfName.DecodeParms).Equals((object)key))
			{
				((PdfDictionary)contentStream).Put(key, item.Value);
			}
		}
		currentResources?.Pop();
		return contentStream;
	}

	public virtual void ConvertStoredResources(OptimizationSession session)
	{
		Stack<PdfDictionary> stack = null;
		try
		{
			stack = (Stack<PdfDictionary>)session.GetStoredValue("resources-for-conversion-key");
		}
		catch (InvalidCastException)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Resources for conversion are not accessible!");
			return;
		}
		while (stack.Count != 0)
		{
			PdfDictionary obj = stack.Pop();
			PdfDictionary asDictionary = obj.GetAsDictionary(PdfName.ColorSpace);
			if (asDictionary != null)
			{
				ConvertColorSpaceResources(asDictionary, session);
			}
			PdfDictionary asDictionary2 = obj.GetAsDictionary(PdfName.Pattern);
			if (asDictionary2 != null)
			{
				ConvertPatternResources(asDictionary2, session);
			}
			PdfDictionary asDictionary3 = obj.GetAsDictionary(PdfName.XObject);
			if (asDictionary3 != null)
			{
				ConvertFormXObjectResources(asDictionary3, session);
			}
		}
	}

	public virtual PdfArray ConvertAnnotationIcArray(PdfArray icArray)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		if (icArray.Size() != GetColorConverter().GetSourceNumberOfComponents())
		{
			return icArray;
		}
		double[] array = new double[GetColorConverter().GetSourceNumberOfComponents()];
		for (int i = 0; i < array.Length; i++)
		{
			if (icArray.GetAsNumber(i) == null)
			{
				return icArray;
			}
			array[i] = icArray.GetAsNumber(i).DoubleValue();
		}
		double[] array2 = GetColorConverter().ConvertColor(array);
		PdfArray val = new PdfArray();
		for (int j = 0; j < GetColorConverter().GetTargetNumberOfComponents(); j++)
		{
			val.Add((PdfObject)new PdfNumber(array2[j]));
		}
		return val;
	}

	public virtual void AttemptToConvertTransparencyGroup(PdfObject groupEntryHolder, OptimizationSession session)
	{
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		if (groupEntryHolder is PdfDictionary)
		{
			PdfDictionary asDictionary = ((PdfDictionary)groupEntryHolder).GetAsDictionary(PdfName.Group);
			if (asDictionary != null)
			{
				CheckColorSpaceObjectForPdfAConformance(asDictionary.Get(PdfName.CS), "Can't convert color space of transparency xObject group, PDF\\A conformance will be compromised.", "Unable to convert transparency group color space.", session);
			}
		}
	}

	public virtual CsConverterProperties GetConverterProperties()
	{
		return properties;
	}

	protected internal abstract Type GetOriginalCsClass();

	protected internal abstract ColorConverter GetColorConverter();

	protected internal virtual ColorDecoder CreateColorDecoder(double[] decodeArray)
	{
		return new BlankColorDecoder();
	}

	protected internal virtual IList<PdfObject> ConvertOperatorParameters(IList<PdfObject> operands)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Expected O, but got Unknown
		double[] array = new double[GetColorConverter().GetSourceNumberOfComponents()];
		for (int i = 0; i < array.Length; i++)
		{
			array[i] = ((PdfNumber)operands[i]).DoubleValue();
		}
		double[] array2 = GetColorConverter().ConvertColor(array);
		IList<PdfObject> list = new List<PdfObject>(GetColorConverter().GetTargetNumberOfComponents() + 1);
		for (int j = 0; j < GetColorConverter().GetTargetNumberOfComponents(); j++)
		{
			list.Add((PdfObject)new PdfNumber(array2[j]));
		}
		return list;
	}

	internal virtual IList<PdfObject> ConvertContentStreamOperands(PdfColorSpace fillCs, PdfColorSpace strokeCs, string @operator, IList<PdfObject> operands, OptimizationSession session)
	{
		if ("sc".Equals(@operator) && GetOriginalCsClass().Equals(((object)fillCs).GetType()))
		{
			IList<PdfObject> list = ConvertOperatorParameters(operands);
			list.Add(operands[operands.Count - 1]);
			return list;
		}
		if ("SC".Equals(@operator) && GetOriginalCsClass().Equals(((object)strokeCs).GetType()))
		{
			IList<PdfObject> list2 = ConvertOperatorParameters(operands);
			list2.Add(operands[operands.Count - 1]);
			return list2;
		}
		if ("scn".Equals(@operator) && typeof(UncoloredTilingPattern).Equals(((object)fillCs).GetType()))
		{
			return ConvertPatternOperands(operands, fillCs);
		}
		if ("SCN".Equals(@operator) && typeof(UncoloredTilingPattern).Equals(((object)fillCs).GetType()))
		{
			return ConvertPatternOperands(operands, strokeCs);
		}
		if ("EI".Equals(@operator) && operands.Count == 2)
		{
			return ConvertInlineImageOperands(operands, session);
		}
		return operands;
	}

	private IList<PdfObject> ConvertInlineImageOperands(IList<PdfObject> operands, OptimizationSession session)
	{
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Expected O, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		PdfStream val = (PdfStream)operands[0];
		if (val == null)
		{
			return operands;
		}
		Stack<PdfResources> currentResources = GetCurrentResources(session);
		if (currentResources == null)
		{
			return operands;
		}
		try
		{
			PdfImageXObject val2 = new PdfImageXObject(val);
			PdfImageXObject val3 = ConvertImageCs(val2, session);
			val3.Put(PdfName.Type, (PdfObject)(object)PdfName.XObject);
			val3.Put(PdfName.Subtype, (PdfObject)(object)PdfName.Image);
			PdfName item = currentResources.Peek().AddImage(val3);
			if (val3 != val2)
			{
				session.RegisterEvent(SeverityLevel.INFO, "Inline image was converted.");
			}
			else
			{
				session.RegisterEvent(SeverityLevel.INFO, "Inline image was transformed to xObject.");
			}
			operands = new List<PdfObject>();
			operands.Add((PdfObject)(object)item);
			operands.Add((PdfObject)(object)Do);
		}
		catch (PdfOptimizerException)
		{
			throw;
		}
		catch (Exception)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Unable to convert inline image.");
		}
		return operands;
	}

	private IList<PdfObject> ConvertPatternOperands(IList<PdfObject> operands, PdfColorSpace currentCs)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		if (1 == ((PdfObjectWrapper<PdfObject>)(object)currentCs).GetPdfObject().GetObjectType())
		{
			PdfArray val = (PdfArray)((PdfObjectWrapper<PdfObject>)(object)currentCs).GetPdfObject();
			if (val.Size() == 2 && ((object)PdfName.Pattern).Equals((object)val.GetAsName(0)) && ((object)GetColorConverter().GetSourceColorspace()).Equals((object)val.GetAsName(1)))
			{
				IList<PdfObject> list = ConvertOperatorParameters(operands);
				list.Add(operands[operands.Count - 2]);
				list.Add(operands[operands.Count - 1]);
				return list;
			}
		}
		return operands;
	}

	private void ConvertColorSpaceResources(PdfDictionary colorSpace, OptimizationSession session)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Expected O, but got Unknown
		//IL_00d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Expected O, but got Unknown
		IDictionary<PdfName, PdfObject> dictionary = new Dictionary<PdfName, PdfObject>();
		foreach (KeyValuePair<PdfName, PdfObject> item in colorSpace.EntrySet())
		{
			PdfName key = item.Key;
			PdfObject value = item.Value;
			if (6 == value.GetObjectType())
			{
				PdfName obj = (PdfName)value;
				if (((object)GetColorConverter().GetSourceColorspace()).Equals((object)obj))
				{
					dictionary.Put(key, (PdfObject)(object)GetColorConverter().GetTargetColorspace());
				}
			}
			else
			{
				if (1 != value.GetObjectType())
				{
					continue;
				}
				PdfArray val = (PdfArray)value;
				PdfName asName = val.GetAsName(0);
				if (asName != null)
				{
					PdfName sourceColorspace = GetColorConverter().GetSourceColorspace();
					if (val.Size() == 4 && ((object)PdfName.Indexed).Equals((object)asName) && ((object)sourceColorspace).Equals((object)val.GetAsName(1)))
					{
						Indexed indexedCs = new Indexed(val);
						AttemptToConvertIndexedCsBasedOnOriginalCs(indexedCs);
					}
					else if (val.Size() == 2 && ((object)PdfName.Pattern).Equals((object)asName) && ((object)sourceColorspace).Equals((object)val.GetAsName(1)))
					{
						val.Set(1, (PdfObject)(object)GetColorConverter().GetTargetColorspace());
					}
					if (val.Size() == 4 && ((object)PdfName.Separation).Equals((object)asName) && ((object)sourceColorspace).Equals((object)val.GetAsName(2)))
					{
						LogErrorOrThrowException(session, "Unable to convert separation color space.", "Can't convert separation color space, PDF\\A conformance will be compromised.");
					}
					else if (val.Size() == 4 && ((object)PdfName.DeviceN).Equals((object)asName) && ((object)sourceColorspace).Equals((object)val.GetAsName(2)))
					{
						LogErrorOrThrowException(session, "Unable to convert deviceN color space.", "Can't convert deviceN color space, PDF\\A conformance will be compromised.");
					}
				}
			}
		}
		ReplaceValues(colorSpace, dictionary);
	}

	private void ConvertFormXObjectResources(PdfDictionary xObject, OptimizationSession session)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Expected O, but got Unknown
		IDictionary<PdfName, PdfObject> dictionary = new Dictionary<PdfName, PdfObject>();
		foreach (KeyValuePair<PdfName, PdfObject> item in xObject.EntrySet())
		{
			PdfName key = item.Key;
			PdfObject value = item.Value;
			if (9 != value.GetObjectType())
			{
				continue;
			}
			PdfStream val = (PdfStream)value;
			if (((object)PdfName.Form).Equals((object)((PdfDictionary)val).GetAsName(PdfName.Subtype)))
			{
				AttemptToConvertTransparencyGroup((PdfObject)(object)val, session);
				PdfStream val2 = ConvertContentStream(val, null, session);
				if (val2 != val)
				{
					dictionary.Put(key, (PdfObject)(object)val2);
				}
			}
		}
		ReplaceValues(xObject, dictionary);
	}

	private void ConvertPatternResources(PdfDictionary pattern, OptimizationSession session)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		IDictionary<PdfName, PdfObject> dictionary = new Dictionary<PdfName, PdfObject>();
		foreach (KeyValuePair<PdfName, PdfObject> item in pattern.EntrySet())
		{
			PdfName key = item.Key;
			PdfObject value = item.Value;
			if (9 == value.GetObjectType())
			{
				PdfStream val = (PdfStream)value;
				PdfNumber asNumber = ((PdfDictionary)val).GetAsNumber(PdfName.PatternType);
				PdfNumber asNumber2 = ((PdfDictionary)val).GetAsNumber(PdfName.PaintType);
				if (asNumber != null && asNumber2 != null && asNumber.IntValue() == 1 && asNumber2.IntValue() == 1)
				{
					PdfStream val2 = ConvertContentStream(val, null, session);
					if (val2 != val)
					{
						dictionary.Put(key, (PdfObject)(object)val2);
					}
				}
			}
			else
			{
				if (3 != value.GetObjectType())
				{
					continue;
				}
				PdfDictionary val3 = (PdfDictionary)value;
				if (val3.GetAsNumber(PdfName.PatternType).IntValue() == 2)
				{
					PdfDictionary asDictionary = val3.GetAsDictionary(PdfName.Shading);
					if (asDictionary != null)
					{
						CheckColorSpaceObjectForPdfAConformance(asDictionary.Get(PdfName.ColorSpace), "Can't convert color space of shading pattern, PDF\\A conformance will be compromised.", "Unable to convert shading pattern color space.", session);
					}
				}
			}
		}
		ReplaceValues(pattern, dictionary);
	}

	private void CheckColorSpaceObjectForPdfAConformance(PdfObject objectToCheck, string exceptionMessage, string errorLogMessage, OptimizationSession session)
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		if (objectToCheck == null)
		{
			return;
		}
		if (6 == objectToCheck.GetObjectType())
		{
			PdfName obj = (PdfName)objectToCheck;
			if (((object)GetColorConverter().GetSourceColorspace()).Equals((object)obj))
			{
				LogErrorOrThrowException(session, errorLogMessage, exceptionMessage);
			}
		}
		else
		{
			if (1 != objectToCheck.GetObjectType())
			{
				return;
			}
			foreach (PdfObject item in (PdfArray)objectToCheck)
			{
				if (((object)GetColorConverter().GetSourceColorspace()).Equals((object)item))
				{
					LogErrorOrThrowException(session, errorLogMessage, exceptionMessage);
				}
			}
		}
	}

	private PdfImageXObject ConvertImageWithOriginalCs(PdfImageXObject imageToConvert)
	{
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected O, but got Unknown
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Expected O, but got Unknown
		BitmapImagePixels bitmapImagePixels = new BitmapImagePixels(imageToConvert);
		PdfArray asArray = ((PdfDictionary)((PdfObjectWrapper<PdfStream>)(object)imageToConvert).GetPdfObject()).GetAsArray(PdfName.Decode);
		ColorDecoder colorDecoder = ((asArray != null) ? CreateColorDecoder(asArray.ToDoubleArray()) : new BlankColorDecoder());
		BitmapImagePixels bitmapImagePixels2 = CsConverterUtil.ConvertBitmapImage(bitmapImagePixels, GetColorConverter(), colorDecoder);
		PdfStream val = (PdfStream)((PdfObject)((PdfObjectWrapper<PdfStream>)(object)imageToConvert).GetPdfObject()).Clone();
		val.SetData(bitmapImagePixels2.GetData());
		((PdfDictionary)val).Put(PdfName.ColorSpace, (PdfObject)(object)GetColorConverter().GetTargetColorspace());
		((PdfDictionary)val).Remove(PdfName.Decode);
		PdfArray asArray2 = ((PdfDictionary)val).GetAsArray(PdfName.Mask);
		if (asArray2 != null)
		{
			PdfArray val2 = MaskColors.Create(asArray2).GetConvertedColorMask(bitmapImagePixels.GetMaxComponentValue(), GetColorConverter(), colorDecoder).ToPdfArray();
			((PdfDictionary)val).Put(PdfName.Mask, (PdfObject)(object)val2);
		}
		return new PdfImageXObject(val);
	}

	private bool AttemptToConvertIndexedCsBasedOnOriginalCs(Indexed indexedCs)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Expected O, but got Unknown
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Expected O, but got Unknown
		if (1 != ((PdfObjectWrapper<PdfObject>)(object)indexedCs).GetPdfObject().GetObjectType())
		{
			return false;
		}
		PdfArray val = (PdfArray)((PdfObjectWrapper<PdfObject>)(object)indexedCs).GetPdfObject();
		BitmapImagePixels bitmapImagePixels = CsConverterUtil.ExtractColorTableOfIndexedImage(indexedCs);
		if (bitmapImagePixels == null)
		{
			return false;
		}
		PdfStream val2 = new PdfStream(CsConverterUtil.ConvertBitmapImage(bitmapImagePixels, GetColorConverter()).GetData(), 9);
		val.Set(3, (PdfObject)(object)val2);
		val.Set(1, (PdfObject)(object)GetColorConverter().GetTargetColorspace());
		return true;
	}

	private void LogErrorOrThrowException(OptimizationSession session, string errorLogMessage, string exceptionMessage)
	{
		bool flag;
		try
		{
			flag = (bool)session.GetStoredValue("is-pdf-a-document-key");
		}
		catch (InvalidCastException)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Is PDF\\A document key are not accessible!");
			flag = false;
		}
		if (!flag || properties.GetConversionMode() == ColorConversionMode.IGNORE_PDF_A_CONFORMANCE)
		{
			session.RegisterEvent(SeverityLevel.ERROR, errorLogMessage);
			return;
		}
		throw new PdfOptimizerException(exceptionMessage);
	}

	private static void ReplaceValues(PdfDictionary source, IDictionary<PdfName, PdfObject> replacementScheme)
	{
		foreach (PdfName key in replacementScheme.Keys)
		{
			source.Remove(key);
		}
		foreach (KeyValuePair<PdfName, PdfObject> item in replacementScheme)
		{
			source.Put(item.Key, item.Value);
		}
	}

	private static Stack<PdfResources> GetCurrentResources(OptimizationSession session)
	{
		try
		{
			return (Stack<PdfResources>)session.GetStoredValue("current-resources-key");
		}
		catch (InvalidCastException)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Current resources are not accessible!");
			return null;
		}
	}
}
