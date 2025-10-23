using System.Collections.Generic;
using System.Text;

namespace iText.Pdfoptimizer.Report.Location;

public class LocationStack
{
	private const string DELIMITER = "/";

	private readonly LinkedList<string> stack = new LinkedList<string>();

	public virtual void EnterLocation(string location)
	{
		stack.Add(location);
	}

	public virtual void LeaveLocation()
	{
		stack.RemoveLast();
	}

	public virtual string GetCurrentLocation()
	{
		if (stack.IsEmpty())
		{
			return "";
		}
		return stack.JGetLast();
	}

	public virtual string GetFullStack()
	{
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		int count = stack.Count;
		foreach (string item in stack)
		{
			stringBuilder.Append(item);
			num++;
			if (num < count)
			{
				stringBuilder.Append("/");
			}
		}
		return stringBuilder.ToString();
	}
}
