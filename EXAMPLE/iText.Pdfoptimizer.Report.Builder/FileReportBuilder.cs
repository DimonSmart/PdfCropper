using System;
using System.IO;
using Microsoft.Extensions.Logging;
using iText.Commons;
using iText.Pdfoptimizer.Report.Message;
using iText.Pdfoptimizer.Report.Publisher;

namespace iText.Pdfoptimizer.Report.Builder;

public class FileReportBuilder : DefaultReportBuilder
{
	private static readonly ILogger LOGGER = ITextLogManager.GetLogger(typeof(FileReportBuilder));

	private readonly IReportPublisher publisher;

	public FileReportBuilder(SeverityLevel level, IReportPublisher publisher)
		: base(level)
	{
		this.publisher = publisher;
	}

	public override OptimizationResult Build()
	{
		try
		{
			publisher.PublishReport(GetMessages());
		}
		catch (IOException ex)
		{
			LoggerExtensions.LogError(LOGGER, (Exception)ex, "Unable to generate PDF optimization report!", Array.Empty<object>());
		}
		return base.Build();
	}
}
