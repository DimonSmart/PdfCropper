using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Report.Decorator;

public interface IMessageDecorator
{
	string DecorateMessage(ReportMessage message);
}
