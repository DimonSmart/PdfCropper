using System;
using System.Collections.Generic;
using iText.Commons.Actions;
using iText.Commons.Utils;

namespace iText.Pdfoptimizer.Statistics;

public class ProfileNameStatisticsAggregator : AbstractStatisticsAggregator
{
	private readonly object Lock = new object();

	private readonly IDictionary<PdfOptimizerProfile, long?> numberOfDocuments = (IDictionary<PdfOptimizerProfile, long?>)new LinkedDictionary<PdfOptimizerProfile, long?>();

	public override void Aggregate(AbstractStatisticsEvent @event)
	{
		if (!(@event is ProfileNameStatisticsEvent))
		{
			return;
		}
		PdfOptimizerProfile pdfOptimizerProfile = ((ProfileNameStatisticsEvent)(object)@event).GetPdfOptimizerProfile();
		lock (Lock)
		{
			long? num = numberOfDocuments.Get(pdfOptimizerProfile);
			long? value = ((!num.HasValue) ? 1 : (num.Value + 1));
			numberOfDocuments.Put(pdfOptimizerProfile, value);
		}
	}

	public override object RetrieveAggregation()
	{
		return JavaCollectionsUtil.UnmodifiableMap<PdfOptimizerProfile, long?>(numberOfDocuments);
	}

	public override void Merge(AbstractStatisticsAggregator aggregator)
	{
		if (!(aggregator is ProfileNameStatisticsAggregator))
		{
			return;
		}
		IDictionary<PdfOptimizerProfile, long?> dictionary = ((ProfileNameStatisticsAggregator)(object)aggregator).numberOfDocuments;
		lock (Lock)
		{
			MapUtil.Merge<PdfOptimizerProfile, long?>(numberOfDocuments, dictionary, (Func<long?, long?, long?>)((long? el1, long? el2) => (!el2.HasValue) ? el1 : (el1 + el2)));
		}
	}
}
