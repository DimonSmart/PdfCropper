using System.Collections.Generic;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace iText.Pdfoptimizer.Handlers.Converters;

internal class IdleEventListener : IEventListener
{
	public virtual void EventOccurred(IEventData data, EventType type)
	{
	}

	public virtual ICollection<EventType> GetSupportedEvents()
	{
		return null;
	}
}
