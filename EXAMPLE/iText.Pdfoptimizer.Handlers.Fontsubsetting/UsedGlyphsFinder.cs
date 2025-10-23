using System;
using System.Collections.Generic;
using iText.Commons.Utils;
using iText.IO.Font.Otf;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Annot;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Pdfoptimizer.Exceptions;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Handlers.Fontsubsetting;

public class UsedGlyphsFinder
{
	internal sealed class UsedGlyphsListener : IEventListener
	{
		private readonly IDictionary<PdfFont, FontGlyphs> usedGlyphsInFonts = (IDictionary<PdfFont, FontGlyphs>)new LinkedDictionary<PdfFont, FontGlyphs>();

		public void EventOccurred(IEventData data, EventType type)
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			//IL_0006: Unknown result type (might be due to invalid IL or missing references)
			TextRenderInfo val = (TextRenderInfo)data;
			PdfFont font = val.GetFont();
			PdfString pdfString = val.GetPdfString();
			IList<Glyph> list = new List<Glyph>();
			bool allGlyphsDecoded = font.AppendDecodedCodesToGlyphsList(list, pdfString);
			FontGlyphs fontGlyphs = usedGlyphsInFonts.Get(font);
			if (fontGlyphs == null)
			{
				fontGlyphs = new FontGlyphs();
				usedGlyphsInFonts.Put(font, fontGlyphs);
			}
			fontGlyphs.GetGlyphs().AddAll(list);
			fontGlyphs.UpdateGlyphsDecodingFailedStatus(allGlyphsDecoded);
		}

		public ICollection<EventType> GetSupportedEvents()
		{
			return (ICollection<EventType>)new LinkedHashSet<EventType>((ICollection<EventType>)JavaCollectionsUtil.SingletonList<EventType>((EventType)1));
		}

		public IDictionary<PdfFont, FontGlyphs> GetUsedGlyphsMap()
		{
			return usedGlyphsInFonts;
		}
	}

	public sealed class FontGlyphs
	{
		private readonly ICollection<Glyph> glyphs = (ICollection<Glyph>)new LinkedHashSet<Glyph>();

		private bool allGlyphsDecoded = true;

		public ICollection<Glyph> GetGlyphs()
		{
			return glyphs;
		}

		public bool IsAnyGlyphsDecodingFailed()
		{
			return !allGlyphsDecoded;
		}

		public void UpdateGlyphsDecodingFailedStatus(bool allGlyphsDecoded)
		{
			this.allGlyphsDecoded &= allGlyphsDecoded;
		}
	}

	public virtual IDictionary<PdfFont, FontGlyphs> FindUsedGlyphsInFonts(PdfDocument document, OptimizationSession session)
	{
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected O, but got Unknown
		UsedGlyphsListener usedGlyphsListener = new UsedGlyphsListener();
		session.StoreValue("event-listener-key", usedGlyphsListener);
		session.StoreValue("canvas-processor-key", (object)new PdfCanvasProcessor((IEventListener)(object)usedGlyphsListener));
		session.StoreValue("resources-for-stream-processing-key", new Stack<PdfDictionary>());
		session.StoreValue("processed-content-streams-key", new HashSet<PdfIndirectReference>());
		session.StoreValue("any-errors-occurred-key", false);
		ProcessContentStreamAndResources(document, session);
		return GetStoredUsedGlyphsMap(session);
	}

	private void ProcessContentStreamAndResources(PdfDocument document, OptimizationSession session)
	{
		for (int i = 1; i <= document.GetNumberOfPages(); i++)
		{
			PdfPage page = document.GetPage(i);
			try
			{
				GetCanvasProcessor(session).ProcessContent(page.GetContentBytes(), page.GetResources());
				ICollection<PdfIndirectReference> processedStreams = GetProcessedStreams(session);
				for (int j = 0; j < page.GetContentStreamCount(); j++)
				{
					processedStreams.Add(((PdfObject)page.GetContentStream(j)).GetIndirectReference());
				}
				GetPendingResources(session).Push(((PdfObjectWrapper<PdfDictionary>)(object)page.GetResources()).GetPdfObject());
			}
			catch (PdfOptimizerException)
			{
				throw;
			}
			catch (Exception)
			{
				session.RegisterEvent(SeverityLevel.ERROR, "Unable to process glyphs in page content stream with reference {0}.", ((PdfObject)((PdfObjectWrapper<PdfDictionary>)(object)page).GetPdfObject()).GetIndirectReference());
				session.StoreValue("any-errors-occurred-key", true);
				throw;
			}
			ProcessPageAnnotations(page, session);
		}
		try
		{
			ProcessStoredResources(session);
			session.RegisterEvent(SeverityLevel.INFO, "Glyphs in document were found successfully.");
		}
		catch (PdfOptimizerException)
		{
			throw;
		}
		catch (Exception)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Unable to process glyphs of the content stream resources.");
			session.StoreValue("any-errors-occurred-key", true);
			throw;
		}
	}

	private void ProcessPageAnnotations(PdfPage page, OptimizationSession session)
	{
		foreach (PdfAnnotation annotation in page.GetAnnotations())
		{
			PdfDictionary appearanceDictionary = annotation.GetAppearanceDictionary();
			if (appearanceDictionary != null)
			{
				ConvertApDict(appearanceDictionary, PdfName.N, session);
				ConvertApDict(appearanceDictionary, PdfName.R, session);
				ConvertApDict(appearanceDictionary, PdfName.D, session);
			}
		}
	}

	private void ConvertApDict(PdfDictionary apDict, PdfName apName, OptimizationSession session)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Expected O, but got Unknown
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		PdfObject val = apDict.Get(apName);
		if (!(val is PdfDictionary))
		{
			return;
		}
		PdfDictionary val2 = (PdfDictionary)val;
		if (9 == ((PdfObject)val2).GetObjectType())
		{
			ProcessContentStream((PdfStream)val2, session);
			return;
		}
		foreach (PdfObject item in val2.Values())
		{
			if (9 == item.GetObjectType())
			{
				ProcessContentStream((PdfStream)item, session);
			}
		}
	}

	private void ProcessStoredResources(OptimizationSession session)
	{
		Stack<PdfDictionary> pendingResources = GetPendingResources(session);
		while (pendingResources.Count != 0)
		{
			PdfDictionary obj = pendingResources.Pop();
			PdfDictionary asDictionary = obj.GetAsDictionary(PdfName.Pattern);
			if (asDictionary != null)
			{
				ProcessPatternResources(asDictionary, session);
			}
			PdfDictionary asDictionary2 = obj.GetAsDictionary(PdfName.XObject);
			if (asDictionary2 != null)
			{
				ProcessFormXObjectResources(asDictionary2, session);
			}
		}
	}

	private void ProcessPatternResources(PdfDictionary pattern, OptimizationSession session)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		foreach (PdfObject item in pattern.Values())
		{
			if (9 == item.GetObjectType())
			{
				ProcessContentStream((PdfStream)item, session);
			}
		}
	}

	private void ProcessFormXObjectResources(PdfDictionary xObject, OptimizationSession session)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Expected O, but got Unknown
		foreach (PdfObject item in xObject.Values())
		{
			if (9 == item.GetObjectType())
			{
				PdfStream val = (PdfStream)item;
				if (((object)PdfName.Form).Equals((object)((PdfDictionary)val).GetAsName(PdfName.Subtype)))
				{
					ProcessContentStream(val, session);
				}
			}
		}
	}

	private static void ProcessContentStream(PdfStream stream, OptimizationSession session)
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Expected O, but got Unknown
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Expected O, but got Unknown
		ICollection<PdfIndirectReference> processedStreams = GetProcessedStreams(session);
		if (((PdfObject)stream).GetIndirectReference() != null && !processedStreams.Contains(((PdfObject)stream).GetIndirectReference()))
		{
			processedStreams.Add(((PdfObject)stream).GetIndirectReference());
			Stack<PdfDictionary> pendingResources = GetPendingResources(session);
			PdfDictionary asDictionary = ((PdfDictionary)stream).GetAsDictionary(PdfName.Resources);
			PdfResources val;
			if (asDictionary == null)
			{
				val = new PdfResources(new PdfDictionary());
			}
			else
			{
				val = new PdfResources(asDictionary);
				pendingResources.Push(asDictionary);
			}
			GetCanvasProcessor(session).ProcessContent(stream.GetBytes(), val);
		}
	}

	private static IDictionary<PdfFont, FontGlyphs> GetStoredUsedGlyphsMap(OptimizationSession session)
	{
		try
		{
			return ((UsedGlyphsListener)session.GetStoredValue("event-listener-key")).GetUsedGlyphsMap();
		}
		catch (InvalidCastException)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Used glyphs event listener are not accessible!");
			throw;
		}
	}

	private static ICollection<PdfIndirectReference> GetProcessedStreams(OptimizationSession session)
	{
		try
		{
			return (ICollection<PdfIndirectReference>)session.GetStoredValue("processed-content-streams-key");
		}
		catch (InvalidCastException)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Processed content streams are not accessible!");
			session.StoreValue("any-errors-occurred-key", true);
			throw;
		}
	}

	private static Stack<PdfDictionary> GetPendingResources(OptimizationSession session)
	{
		try
		{
			return (Stack<PdfDictionary>)session.GetStoredValue("resources-for-stream-processing-key");
		}
		catch (InvalidCastException)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Resource for stream processing are not accessible!");
			session.StoreValue("any-errors-occurred-key", true);
			throw;
		}
	}

	private static PdfCanvasProcessor GetCanvasProcessor(OptimizationSession session)
	{
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Expected O, but got Unknown
		try
		{
			return (PdfCanvasProcessor)session.GetStoredValue("canvas-processor-key");
		}
		catch (InvalidCastException)
		{
			session.RegisterEvent(SeverityLevel.ERROR, "Canvas processor are not accessible!");
			session.StoreValue("any-errors-occurred-key", true);
			throw;
		}
	}
}
