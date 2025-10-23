using System;
using System.Collections.Generic;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Pdf.Xobject;
using iText.Kernel.XMP;
using iText.Pdfoptimizer.Exceptions;
using iText.Pdfoptimizer.Handlers.Converters;
using iText.Pdfoptimizer.Handlers.Util;
using iText.Pdfoptimizer.Report.Message;
using iText.Pdfoptimizer.Util;
using iText.Pdfoptimizer.Util.Traversing;

namespace iText.Pdfoptimizer.Handlers;

public class ColorSpaceConverter : AbstractOptimizationHandler
{
	private const string XMP_PDF_AMD_PROPERTY = "amd";

	private const string XMP_PDF_CORR_PROPERTY = "corr";

	private ICsConverter csConverter;

	public virtual ICsConverter GetCsConverter()
	{
		return csConverter;
	}

	public virtual ColorSpaceConverter SetCsConverter(ICsConverter csConverter)
	{
		this.csConverter = csConverter;
		return this;
	}

	protected internal override void OptimizePdf(PdfDocument document, OptimizationSession session)
	{
		if (csConverter == null)
		{
			session.RegisterEvent(SeverityLevel.WARNING, "No color space converter was installed.");
			return;
		}
		Stack<PdfDictionary> value = new Stack<PdfDictionary>();
		ICollection<PdfIndirectReference> value2 = new HashSet<PdfIndirectReference>();
		Stack<PdfResources> value3 = new Stack<PdfResources>();
		session.StoreValue("resources-for-conversion-key", value);
		session.StoreValue("converted-content-streams-key", value2);
		session.StoreValue("current-resources-key", value3);
		bool flag = document.GetConformance().IsPdfA();
		session.StoreValue("is-pdf-a-document-key", flag);
		CsConverterProperties converterProperties = csConverter.GetConverterProperties();
		if (flag && converterProperties.GetConversionMode() == ColorConversionMode.NORMAL && converterProperties.GetOutputIntent() == null)
		{
			throw new PdfOptimizerException("PDF/A document color space is under color conversion, but new output intent is not set. Either set new output intent or ignore PDF/A conformance in CsConverterProperties.");
		}
		if (flag && converterProperties.GetOutputIntent() != null && !((object)PdfName.GTS_PDFA1).Equals((object)converterProperties.GetOutputIntent().GetOutputIntentSubtype()))
		{
			throw new PdfOptimizerException("Invalid output intent subtype, should be GTS_PDFA1.");
		}
		ConvertImage(document, session);
		ConvertContentStreamAndResources(document, session);
		if (flag && converterProperties.GetConversionMode() == ColorConversionMode.IGNORE_PDF_A_CONFORMANCE)
		{
			RemovePdfAMetadata(document, session);
		}
		ReplaceOutputIntents(document, session);
	}

	private void RemovePdfAMetadata(PdfDocument document, OptimizationSession session)
	{
		try
		{
			XMPMeta xmpMetadata = document.GetXmpMetadata();
			session.RegisterEvent(SeverityLevel.WARNING, "PDF\\A id schemas were removed from PDF XMP metadata.");
			xmpMetadata.DeleteProperty("http://www.aiim.org/pdfa/ns/id/", "conformance");
			xmpMetadata.DeleteProperty("http://www.aiim.org/pdfa/ns/id/", "part");
			xmpMetadata.DeleteProperty("http://www.aiim.org/pdfa/ns/id/", "amd");
			xmpMetadata.DeleteProperty("http://www.aiim.org/pdfa/ns/id/", "corr");
			document.SetXmpMetadata(xmpMetadata);
		}
		catch (XMPException)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Unable to remove PDF\\A id schemas from PDF XMP metadata.");
		}
	}

	private void ReplaceOutputIntents(PdfDocument document, OptimizationSession session)
	{
		PdfOutputIntent outputIntent = csConverter.GetConverterProperties().GetOutputIntent();
		if (outputIntent != null)
		{
			PdfArray asArray = ((PdfObjectWrapper<PdfDictionary>)(object)document.GetCatalog()).GetPdfObject().GetAsArray(PdfName.OutputIntents);
			if (asArray != null)
			{
				asArray.Clear();
				asArray.Add((PdfObject)(object)((PdfObjectWrapper<PdfDictionary>)(object)outputIntent).GetPdfObject());
				session.RegisterEvent(SeverityLevel.INFO, "Output intent was replaced.");
			}
		}
	}

	private void ConvertImage(PdfDocument document, OptimizationSession session)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		IList<PdfObject> list = DocumentStructureUtils.Search(document, new PdfImageXObjectPredicate());
		IDictionary<PdfObject, PdfObject> dictionary = new Dictionary<PdfObject, PdfObject>();
		foreach (PdfStream item in list)
		{
			PdfImageXObject val = new PdfImageXObject(item);
			try
			{
				PdfImageXObject obj = csConverter.ConvertImageCs(val, session);
				PdfStream pdfObject = ((PdfObjectWrapper<PdfStream>)(object)val).GetPdfObject();
				PdfStream pdfObject2 = ((PdfObjectWrapper<PdfStream>)(object)obj).GetPdfObject();
				if (pdfObject != pdfObject2)
				{
					session.RegisterEvent(SeverityLevel.INFO, "Color space of the image with reference {0} was converted.", ((PdfObject)pdfObject).GetIndirectReference());
					((PdfObject)pdfObject2).MakeIndirect(document);
					((PdfObject)pdfObject2).SetModified();
					dictionary.Put((PdfObject)(object)pdfObject, (PdfObject)(object)pdfObject2);
				}
			}
			catch (PdfOptimizerException)
			{
				throw;
			}
			catch (Exception)
			{
				SafeLogUnableToConvertMessage(session, val);
			}
		}
		DocumentStructureUtils.Traverse(document, new ReplaceObjectsAction(dictionary));
	}

	private void ConvertContentStreamAndResources(PdfDocument document, OptimizationSession session)
	{
		for (int i = 1; i <= document.GetNumberOfPages(); i++)
		{
			PdfPage page = document.GetPage(i);
			csConverter.AttemptToConvertTransparencyGroup((PdfObject)(object)((PdfObjectWrapper<PdfDictionary>)(object)page).GetPdfObject(), session);
			for (int j = 0; j < page.GetContentStreamCount(); j++)
			{
				PdfStream contentStream = page.GetContentStream(j);
				try
				{
					PdfStream val = csConverter.ConvertContentStream(contentStream, page.GetResources(), session);
					if (val != contentStream)
					{
						if (page.GetContentStreamCount() == 1)
						{
							page.Put(PdfName.Contents, (PdfObject)(object)val);
						}
						else
						{
							((PdfObjectWrapper<PdfDictionary>)(object)page).GetPdfObject().GetAsArray(PdfName.Contents).Set(j, (PdfObject)(object)val);
						}
						session.RegisterEvent(SeverityLevel.INFO, "Color space of the content stream with reference {0} was converted.", ((PdfObject)contentStream).GetIndirectReference());
					}
				}
				catch (PdfOptimizerException)
				{
					throw;
				}
				catch (Exception)
				{
					session.RegisterEvent(SeverityLevel.ERROR, "Unable to convert color space of the content stream with reference {0}.", ((PdfObject)contentStream).GetIndirectReference());
				}
			}
			foreach (PdfAnnotation annotation in page.GetAnnotations())
			{
				PdfDictionary appearanceDictionary = annotation.GetAppearanceDictionary();
				if (appearanceDictionary != null)
				{
					ConvertApDict(appearanceDictionary, PdfName.N, session);
					ConvertApDict(appearanceDictionary, PdfName.R, session);
					ConvertApDict(appearanceDictionary, PdfName.D, session);
				}
				PdfName subtype = annotation.GetSubtype();
				if (!((object)PdfName.Line).Equals((object)subtype) && !((object)PdfName.Square).Equals((object)subtype) && !((object)PdfName.Circle).Equals((object)subtype) && !((object)PdfName.Polygon).Equals((object)subtype) && !((object)PdfName.PolyLine).Equals((object)subtype))
				{
					continue;
				}
				PdfArray asArray = ((PdfObjectWrapper<PdfDictionary>)(object)annotation).GetPdfObject().GetAsArray(PdfName.IC);
				if (asArray != null)
				{
					PdfArray val2 = csConverter.ConvertAnnotationIcArray(asArray);
					if (val2 != asArray)
					{
						((PdfObjectWrapper<PdfDictionary>)(object)annotation).GetPdfObject().Put(PdfName.IC, (PdfObject)(object)val2);
					}
				}
			}
		}
		try
		{
			csConverter.ConvertStoredResources(session);
			session.RegisterEvent(SeverityLevel.INFO, "Color space of the content stream resources was converted.");
		}
		catch (PdfOptimizerException)
		{
			throw;
		}
		catch (Exception)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Unable to convert color space of the content stream resources.");
		}
	}

	private void ConvertApDict(PdfDictionary apDict, PdfName apName, OptimizationSession session)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0087: Expected O, but got Unknown
		PdfObject val = apDict.Get(apName);
		if (!(val is PdfDictionary))
		{
			return;
		}
		PdfDictionary val2 = (PdfDictionary)val;
		if (9 == ((PdfObject)val2).GetObjectType())
		{
			PdfStream apStream = (PdfStream)val2;
			PdfStream val3 = ConvertCurrentApStream(apStream, session);
			if (val3 != null)
			{
				apDict.Put(apName, (PdfObject)(object)val3);
			}
			return;
		}
		IDictionary<PdfName, PdfObject> dictionary = new Dictionary<PdfName, PdfObject>();
		foreach (KeyValuePair<PdfName, PdfObject> item in val2.EntrySet())
		{
			PdfName key = item.Key;
			PdfObject value = item.Value;
			if (9 == value.GetObjectType())
			{
				PdfStream apStream2 = (PdfStream)value;
				PdfStream val4 = ConvertCurrentApStream(apStream2, session);
				if (val4 != null)
				{
					dictionary.Put(key, (PdfObject)(object)val4);
				}
			}
		}
		foreach (PdfName key2 in dictionary.Keys)
		{
			val2.Remove(key2);
		}
		foreach (KeyValuePair<PdfName, PdfObject> item2 in dictionary)
		{
			val2.Put(item2.Key, item2.Value);
		}
	}

	private PdfStream ConvertCurrentApStream(PdfStream apStream, OptimizationSession session)
	{
		try
		{
			csConverter.AttemptToConvertTransparencyGroup((PdfObject)(object)apStream, session);
			PdfStream val = csConverter.ConvertContentStream(apStream, null, session);
			if (apStream != val)
			{
				session.RegisterEvent(SeverityLevel.INFO, "Color space of the appearance stream with reference {0} was converted.", ((PdfObject)apStream).GetIndirectReference());
				return val;
			}
		}
		catch (PdfOptimizerException)
		{
			throw;
		}
		catch (Exception)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Unable to convert color space of the appearance stream with reference {0}.", ((PdfObject)apStream).GetIndirectReference());
		}
		return null;
	}

	private static void SafeLogUnableToConvertMessage(OptimizationSession session, PdfImageXObject image)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Unable to convert color space of the image with reference {0} of type {1}.", ((PdfObject)((PdfObjectWrapper<PdfStream>)(object)image).GetPdfObject()).GetIndirectReference(), image.IdentifyImageType());
		}
		catch (Exception)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Unable to convert color space of the image with reference {0} of type {1}.", ((PdfObject)((PdfObjectWrapper<PdfStream>)(object)image).GetPdfObject()).GetIndirectReference(), "undefined");
		}
	}
}
