using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using iText.Commons;
using iText.Commons.Utils;
using iText.IO.Util;
using iText.Pdfoptimizer.Report.Decorator;
using iText.Pdfoptimizer.Report.Message;

namespace iText.Pdfoptimizer.Report.Publisher;

public class FileReportPublisher : IReportPublisher
{
	private static readonly ILogger LOGGER = ITextLogManager.GetLogger(typeof(FileReportPublisher));

	private IReportDecorator decorator;

	private FileInfo file;

	public FileReportPublisher(FileInfo file)
		: this(new DefaultReportDecorator(), file)
	{
	}

	public FileReportPublisher(IReportDecorator decorator, FileInfo file)
	{
		this.decorator = decorator;
		this.file = file;
	}

	public virtual IReportDecorator GetDecorator()
	{
		return decorator;
	}

	public virtual void SetDecorator(IReportDecorator decorator)
	{
		this.decorator = decorator;
	}

	public virtual FileInfo GetFile()
	{
		return file;
	}

	public virtual void SetFile(FileInfo file)
	{
		this.file = file;
	}

	public virtual void PublishReport(IList<ReportMessage> messages)
	{
		using (FileStream stream = new FileStream(file.FullName, FileMode.Create))
		{
			string header = decorator.GetHeader();
			if (header != null && header.Length > 0)
			{
				stream.Write(header.GetBytes(Encoding.UTF8));
				stream.Write(decorator.GetSeparator().GetBytes(Encoding.UTF8));
			}
			foreach (ReportMessage message in messages)
			{
				stream.Write(decorator.DecorateMessage(message).GetBytes(Encoding.UTF8));
				stream.Write(decorator.GetSeparator().GetBytes(Encoding.UTF8));
			}
			string footer = decorator.GetFooter();
			if (footer != null && footer.Length > 0)
			{
				stream.Write(footer.GetBytes(Encoding.UTF8));
				stream.Write(decorator.GetSeparator().GetBytes(Encoding.UTF8));
			}
		}
		LoggerExtensions.LogInformation(LOGGER, MessageFormatUtil.Format("PDF optimization report generated, see: {0}", new object[1] { UrlUtil.GetNormalizedFileUriString(GetFile().ToString()) }), Array.Empty<object>());
	}
}
