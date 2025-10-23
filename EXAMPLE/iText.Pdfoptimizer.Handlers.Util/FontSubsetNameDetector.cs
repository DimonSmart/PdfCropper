using System.Text.RegularExpressions;
using iText.Commons.Utils;

namespace iText.Pdfoptimizer.Handlers.Util;

public sealed class FontSubsetNameDetector
{
	private const string SUBSET_PREFIX_REGEX = "^[A-Z]{6}\\+.*$";

	private static readonly Regex SUBSET_PREFIX_PATTERN = StringUtil.RegexCompile("^[A-Z]{6}\\+.*$");

	private FontSubsetNameDetector()
	{
	}

	public static bool IsFontSubsetName(string fontName)
	{
		return Matcher.Match(SUBSET_PREFIX_PATTERN, fontName).Matches();
	}
}
