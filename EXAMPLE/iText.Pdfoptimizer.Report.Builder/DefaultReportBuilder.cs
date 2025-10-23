using System;
using System.Collections.Generic;
using iText.Commons.Utils;
using iText.Pdfoptimizer.Report.Location;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Report.Builder;

public class DefaultReportBuilder
{
	private readonly List<ReportMessage> messages = new List<ReportMessage>();

	private readonly SeverityLevel minimalLevel;

	public DefaultReportBuilder(SeverityLevel minimalLevel)
	{
		this.minimalLevel = minimalLevel;
	}

	public ReportMessage Log(SeverityLevel level, DateTime time, LocationStack location, string message, params object[] @params)
	{
		ReportMessage reportMessage = null;
		if (level.IsAccepted(minimalLevel))
		{
			reportMessage = new ReportMessage(level, time, location.GetFullStack(), MessageFormatUtil.Format(message, @params));
			messages.Add(reportMessage);
		}
		ProcessMessage(reportMessage);
		return reportMessage;
	}

	public virtual OptimizationResult Build()
	{
		OptimizationResult result = new OptimizationResult(new List<ReportMessage>(messages));
		messages.Clear();
		return result;
	}

	protected internal virtual List<ReportMessage> GetMessages()
	{
		return messages;
	}

	protected internal virtual void ProcessMessage(ReportMessage message)
	{
	}
}
