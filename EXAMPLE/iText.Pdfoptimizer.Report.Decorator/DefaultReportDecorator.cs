namespace iText.Pdfoptimizer.Report.Decorator;

public class DefaultReportDecorator : DefaultMessageDecorator, IReportDecorator, IMessageDecorator
{
	private const string SEPARATOR = "\r\n";

	public virtual string GetHeader()
	{
		return null;
	}

	public virtual string GetFooter()
	{
		return null;
	}

	public virtual string GetSeparator()
	{
		return "\r\n";
	}
}
