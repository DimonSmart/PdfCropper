using System.Collections.Generic;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Fontduplication.Rules;
using iText.Pdfoptimizer.Util;

namespace iText.Pdfoptimizer.Handlers.Fontduplication;

public class PdfDictionaryEqualityCalculator
{
	private readonly IList<IValueUpdateRule> rules;

	public PdfDictionaryEqualityCalculator(IList<IValueUpdateRule> rules)
	{
		this.rules = rules;
	}

	public virtual int GetHashCode(PdfDictionary dict)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		if (dict == null)
		{
			return 0;
		}
		PdfDictionary val = new PdfDictionary(dict);
		foreach (IValueUpdateRule rule in rules)
		{
			rule.Update(val);
		}
		return EqualityUtils.GetHashCode((PdfObject)(object)val);
	}

	public virtual bool AreEqual(PdfDictionary dict1, PdfDictionary dict2)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected O, but got Unknown
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		if (dict1 == dict2)
		{
			return true;
		}
		if ((dict1 == null) ^ (dict2 == null))
		{
			return false;
		}
		PdfDictionary val = new PdfDictionary(dict1);
		PdfDictionary val2 = new PdfDictionary(dict2);
		foreach (IValueUpdateRule rule in rules)
		{
			rule.Update(val);
			rule.Update(val2);
		}
		return EqualityUtils.AreEqual((PdfObject)(object)val, (PdfObject)(object)val2);
	}
}
