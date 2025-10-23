using System.Collections.Generic;
using iText.Commons.Actions;
using iText.Commons.Actions.Data;
using iText.Commons.Utils;

namespace iText.Pdfoptimizer.Statistics;

public class ProfileNameStatisticsEvent : AbstractStatisticsEvent
{
	private const string PROFILE_NAME_STATISTICS = "profileName";

	private readonly PdfOptimizerProfile pdfOptimizerProfile;

	public ProfileNameStatisticsEvent(PdfOptimizerProfile pdfOptimizerProfile, ProductData productData)
		: base(productData)
	{
		this.pdfOptimizerProfile = pdfOptimizerProfile;
	}

	public override AbstractStatisticsAggregator CreateStatisticsAggregatorFromName(string statisticsName)
	{
		if ("profileName".Equals(statisticsName))
		{
			return (AbstractStatisticsAggregator)(object)new ProfileNameStatisticsAggregator();
		}
		return ((AbstractStatisticsEvent)this).CreateStatisticsAggregatorFromName(statisticsName);
	}

	public override IList<string> GetStatisticsNames()
	{
		return JavaCollectionsUtil.SingletonList<string>("profileName");
	}

	public virtual PdfOptimizerProfile GetPdfOptimizerProfile()
	{
		return pdfOptimizerProfile;
	}
}
