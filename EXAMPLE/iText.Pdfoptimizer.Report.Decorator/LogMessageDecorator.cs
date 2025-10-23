using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Report.Decorator;

public class LogMessageDecorator : IMessageDecorator
{
	public virtual string DecorateMessage(ReportMessage message)
	{
		if (message.GetLocation() != null && !string.IsNullOrEmpty(message.GetLocation()))
		{
			return message.GetLocation() + ": " + message.GetMessage();
		}
		return message.GetMessage();
	}
}
