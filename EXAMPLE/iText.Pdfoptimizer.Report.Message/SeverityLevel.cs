namespace iText.Pdfoptimizer.Report.Message;

public sealed class SeverityLevel
{
	public static readonly SeverityLevel INFO = new SeverityLevel();

	public static readonly SeverityLevel WARNING = new SeverityLevel();

	public static readonly SeverityLevel ERROR = new SeverityLevel();

	public bool IsAccepted(SeverityLevel minimalLevel)
	{
		if (minimalLevel == null)
		{
			return true;
		}
		return GetLevelAsInt() >= minimalLevel.GetLevelAsInt();
	}

	public override string ToString()
	{
		if (this == INFO)
		{
			return "INFO";
		}
		if (this == WARNING)
		{
			return "WARNING";
		}
		return "ERROR";
	}

	private int GetLevelAsInt()
	{
		if (this == INFO)
		{
			return 1;
		}
		if (this == WARNING)
		{
			return 2;
		}
		return 3;
	}
}
