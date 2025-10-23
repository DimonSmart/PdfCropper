using System.Collections.Generic;
using iText.Commons.Utils;
using iText.Pdfoptimizer.Report.Builder;
using iText.Pdfoptimizer.Report.Location;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer;

public class OptimizationSession
{
	public const string WRITER_PROPERTIES_KEY = "writer-properties-key";

	public const string RESOURCES_FOR_CONVERSION_KEY = "resources-for-conversion-key";

	public const string CONVERTED_CONTENT_STREAMS_KEY = "converted-content-streams-key";

	public const string RESOURCES_FOR_STREAM_PROCESSING_KEY = "resources-for-stream-processing-key";

	public const string PROCESSED_CONTENT_STREAMS_KEY = "processed-content-streams-key";

	public const string ANY_ERRORS_OCCURRED_KEY = "any-errors-occurred-key";

	public const string CURRENT_RESOURCES_KEY = "current-resources-key";

	public const string IS_PDF_A_DOCUMENT_KEY = "is-pdf-a-document-key";

	public const string CANVAS_PROCESSOR_KEY = "canvas-processor-key";

	public const string EVENT_LISTENER_KEY = "event-listener-key";

	private readonly DefaultReportBuilder reportBuilder;

	private readonly IDictionary<string, object> storedValues = new Dictionary<string, object>();

	private readonly LocationStack locationStack = new LocationStack();

	public OptimizationSession(DefaultReportBuilder reportBuilder)
	{
		this.reportBuilder = reportBuilder;
	}

	public virtual void RegisterEvent(SeverityLevel level, string message, params object[] args)
	{
		reportBuilder.Log(level, DateTimeUtil.GetCurrentUtcTime(), locationStack, message, args);
	}

	public virtual object GetStoredValue(string key)
	{
		return storedValues.Get(key);
	}

	public virtual void StoreValue(string key, object value)
	{
		storedValues.Put(key, value);
	}

	internal virtual LocationStack GetLocationStack()
	{
		return locationStack;
	}
}
