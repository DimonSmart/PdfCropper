namespace iText.Pdfoptimizer.Report.Decorator;

public interface IReportDecorator : IMessageDecorator
{
	string GetHeader();

	string GetFooter();

	string GetSeparator();
}
