using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Report.Decorator;

public class DefaultMessageDecorator : IMessageDecorator
{
	public virtual string DecorateMessage(ReportMessage message)
	{
		return "[" + message.GetLevel().ToString() + "] " + message.GetLocation() + ": " + message.GetMessage();
	}
}
