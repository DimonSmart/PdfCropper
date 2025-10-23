using iText.Commons.Utils;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors.Utils;

public sealed class HashableArray
{
	private readonly long[] array;

	public HashableArray(long[] array)
	{
		if (array == null)
		{
			this.array = new long[0];
		}
		else
		{
			this.array = JavaUtil.ArraysCopyOf<long>(array, array.Length);
		}
	}

	public long[] GetArray()
	{
		return JavaUtil.ArraysCopyOf<long>(array, array.Length);
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
		HashableArray hashableArray = (HashableArray)o;
		return JavaUtil.ArraysEquals<long>(array, hashableArray.array);
	}

	public override int GetHashCode()
	{
		return JavaUtil.ArraysHashCode<long>(array);
	}
}
