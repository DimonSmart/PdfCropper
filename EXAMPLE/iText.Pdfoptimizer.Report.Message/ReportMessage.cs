using System;

namespace iText.Pdfoptimizer.Report.Message;

public class ReportMessage
{
	private readonly SeverityLevel level;

	private readonly DateTime time;

	private readonly string location;

	private readonly string message;

	public ReportMessage(SeverityLevel level, DateTime time, string location, string message)
	{
		this.level = level;
		this.time = time;
		this.location = location;
		this.message = message;
	}

	public virtual SeverityLevel GetLevel()
	{
		return level;
	}

	public virtual DateTime GetTime()
	{
		return time;
	}

	public virtual string GetLocation()
	{
		return location;
	}

	public virtual string GetMessage()
	{
		return message;
	}
}
