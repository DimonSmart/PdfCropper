using System.Collections.Generic;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Report;

public class OptimizationResult
{
	private readonly IList<ReportMessage> messages;

	public OptimizationResult(IList<ReportMessage> messages)
	{
		this.messages = new List<ReportMessage>(messages);
	}

	public virtual IList<ReportMessage> GetMessages()
	{
		return new List<ReportMessage>(messages);
	}
}
