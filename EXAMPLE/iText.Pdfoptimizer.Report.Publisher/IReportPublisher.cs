using System.Collections.Generic;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Report.Publisher;

public interface IReportPublisher
{
	void PublishReport(IList<ReportMessage> messages);
}
