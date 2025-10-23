using iText.Commons.Actions;
using iText.Commons.Actions.Confirmations;
using iText.Commons.Actions.Contexts;
using iText.Commons.Actions.Sequence;
using iText.Pdfoptimizer.Actions.Data;

namespace iText.Pdfoptimizer.Actions.Events;

public sealed class PdfOptimizerProductEvent : AbstractProductProcessITextEvent
{
	public const string OPTIMIZE_PDF = "optimize-pdf";

	private readonly string eventType;

	private PdfOptimizerProductEvent(SequenceId sequenceId, IMetaInfo metaInfo, string eventType)
		: base(sequenceId, PdfOptimizerProductData.GetInstance(), metaInfo, (EventConfirmationType)1)
	{
		this.eventType = eventType;
	}

	public static PdfOptimizerProductEvent CreateOptimizePdfEvent(SequenceId sequenceId, IMetaInfo metaInfo)
	{
		return new PdfOptimizerProductEvent(sequenceId, metaInfo, "optimize-pdf");
	}

	public override string GetEventType()
	{
		return eventType;
	}
}
