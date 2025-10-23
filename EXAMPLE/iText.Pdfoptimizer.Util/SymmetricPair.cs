namespace iText.Pdfoptimizer.Util;

public class SymmetricPair
{
	private readonly object obj1;

	private readonly object obj2;

	public SymmetricPair(object obj1, object obj2)
	{
		this.obj1 = obj1;
		this.obj2 = obj2;
	}

	public override bool Equals(object o)
	{
		if (this == o)
		{
			return true;
		}
		if (o == null || GetType() != o.GetType())
		{
			return false;
		}
		SymmetricPair symmetricPair = (SymmetricPair)o;
		if (!object.Equals(obj1, symmetricPair.obj1) || !object.Equals(obj2, symmetricPair.obj2))
		{
			if (object.Equals(obj1, symmetricPair.obj2))
			{
				return object.Equals(obj2, symmetricPair.obj1);
			}
			return false;
		}
		return true;
	}

	public override int GetHashCode()
	{
		return 31 * ((obj1 == null) ? 1 : obj1.GetHashCode()) * ((obj2 == null) ? 1 : obj2.GetHashCode());
	}
}
