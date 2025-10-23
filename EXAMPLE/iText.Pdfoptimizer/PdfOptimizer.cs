using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using iText.Commons;
using iText.Commons.Actions;
using iText.Commons.Actions.Contexts;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Actions.Data;
using iText.Pdfoptimizer.Actions.Events;
using iText.Pdfoptimizer.Report;
using iText.Pdfoptimizer.Report.Builder;
using iText.Pdfoptimizer.Report.Message;
using iText.Pdfoptimizer.Statistics;

namespace iText.Pdfoptimizer;

public class PdfOptimizer
{
	private class PdfOptimizerMetaInfo : IMetaInfo
	{
	}

	private static readonly ILogger LOGGER = ITextLogManager.GetLogger(typeof(PdfOptimizer));

	private readonly List<AbstractOptimizationHandler> handlers = new List<AbstractOptimizationHandler>();

	private DefaultReportBuilder reportBuilder;

	private PdfOptimizerProfile profile;

	public PdfOptimizer()
		: this(PdfOptimizerProfile.CUSTOM, null)
	{
	}

	internal PdfOptimizer(PdfOptimizerProfile profile, IList<AbstractOptimizationHandler> handlers)
	{
		this.profile = profile;
		if (handlers != null)
		{
			this.handlers.AddAll(handlers);
		}
	}

	public virtual PdfOptimizer AddOptimizationHandler(AbstractOptimizationHandler handler)
	{
		profile = PdfOptimizerProfile.CUSTOM;
		handlers.Add(handler);
		return this;
	}

	public virtual void SetReportBuilder(DefaultReportBuilder reportBuilder)
	{
		this.reportBuilder = reportBuilder;
	}

	public virtual OptimizationResult Optimize(FileInfo inputFile, FileInfo outputFile)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		PdfReader val = new PdfReader(inputFile);
		try
		{
			using FileStream outputStream = new FileStream(outputFile.FullName, FileMode.Create);
			return Optimize(val, outputStream);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public virtual OptimizationResult Optimize(FileInfo inputFile, Stream outputStream)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		PdfReader val = new PdfReader(inputFile);
		try
		{
			return Optimize(val, outputStream);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public virtual OptimizationResult Optimize(Stream inputStream, FileInfo outputFile)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		PdfReader val = new PdfReader(inputStream);
		try
		{
			using FileStream outputStream = new FileStream(outputFile.FullName, FileMode.Create);
			return Optimize(val, outputStream);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public virtual OptimizationResult Optimize(Stream inputStream, Stream outputStream)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected O, but got Unknown
		PdfReader val = new PdfReader(inputStream);
		try
		{
			return Optimize(val, outputStream);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	public virtual OptimizationResult Optimize(PdfReader reader, FileInfo outputFile)
	{
		using FileStream outputStream = new FileStream(outputFile.FullName, FileMode.Create);
		return Optimize(reader, outputStream);
	}

	public virtual OptimizationResult Optimize(PdfReader reader, Stream outputStream)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Expected O, but got Unknown
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected O, but got Unknown
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		WriterProperties val = new WriterProperties();
		IMetaInfo val2 = (IMetaInfo)(object)new PdfOptimizerMetaInfo();
		PdfWriter val3 = new PdfWriter(outputStream, val);
		OptimizationResult result;
		try
		{
			PdfDocument val4 = new PdfDocument(reader, val3, (StampingProperties)((DocumentProperties)new StampingProperties()).SetEventCountingMetaInfo(val2));
			try
			{
				EventManager.GetInstance().OnEvent((IEvent)(object)PdfOptimizerProductEvent.CreateOptimizePdfEvent(val4.GetDocumentIdWrapper(), val2));
				DefaultReportBuilder defaultReportBuilder = reportBuilder;
				if (defaultReportBuilder == null)
				{
					defaultReportBuilder = GetDefaultReportBuilder();
				}
				OptimizationSession optimizationSession = new OptimizationSession(defaultReportBuilder);
				optimizationSession.GetLocationStack().EnterLocation(GetType().Name);
				optimizationSession.StoreValue("writer-properties-key", val);
				if (handlers.IsEmpty())
				{
					LoggerExtensions.LogWarning(LOGGER, "PdfOptimizer is used without any optimization handlers. So no operations with PDF document will be performed.", Array.Empty<object>());
				}
				foreach (AbstractOptimizationHandler handler in handlers)
				{
					handler.PrepareAndRunOptimization(val4, optimizationSession);
				}
				optimizationSession.GetLocationStack().LeaveLocation();
				result = defaultReportBuilder.Build();
			}
			finally
			{
				((IDisposable)val4)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val3)?.Dispose();
		}
		EventManager.GetInstance().OnEvent((IEvent)(object)new ProfileNameStatisticsEvent(profile, PdfOptimizerProductData.GetInstance()));
		return result;
	}

	private static DefaultReportBuilder GetDefaultReportBuilder()
	{
		return new DefaultReportBuilder(SeverityLevel.INFO);
	}
}
