using System;
using Microsoft.Extensions.Logging;
using iText.Commons;
using iText.Pdfoptimizer.Report.Decorator;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Report.Builder;

public class LogReportBuilder : DefaultReportBuilder
{
	private static readonly ILogger LOGGER = ITextLogManager.GetLogger(typeof(LogReportBuilder));

	private readonly IMessageDecorator messageDecorator;

	public LogReportBuilder(SeverityLevel minimalLevel, IMessageDecorator decorator)
		: base(minimalLevel)
	{
		messageDecorator = decorator;
	}

	protected internal override void ProcessMessage(ReportMessage reportMessage)
	{
		if (reportMessage != null)
		{
			SeverityLevel level = reportMessage.GetLevel();
			if (level == SeverityLevel.INFO)
			{
				LoggerExtensions.LogInformation(LOGGER, messageDecorator.DecorateMessage(reportMessage), Array.Empty<object>());
			}
			else if (level == SeverityLevel.WARNING)
			{
				LoggerExtensions.LogWarning(LOGGER, messageDecorator.DecorateMessage(reportMessage), Array.Empty<object>());
			}
			else if (level == SeverityLevel.ERROR)
			{
				LoggerExtensions.LogError(LOGGER, messageDecorator.DecorateMessage(reportMessage), Array.Empty<object>());
			}
		}
	}
}
