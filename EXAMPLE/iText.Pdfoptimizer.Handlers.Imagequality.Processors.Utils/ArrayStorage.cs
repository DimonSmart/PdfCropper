using System.Collections.Generic;

namespace iText.Pdfoptimizer.Handlers.Imagequality.Processors.Utils;

public class ArrayStorage
{
	private int currentIndexPixel;

	private readonly Dictionary<HashableArray, int?> map = new Dictionary<HashableArray, int?>();

	public virtual void Add(long[] array)
	{
		HashableArray key = new HashableArray(array);
		if (!map.Get(key).HasValue)
		{
			map.Put(key, currentIndexPixel);
			currentIndexPixel++;
		}
	}

	public virtual int? Get(long[] array)
	{
		return map.Get(new HashableArray(array));
	}

	public virtual int Size()
	{
		return currentIndexPixel;
	}

	public virtual ICollection<KeyValuePair<HashableArray, int?>> GetAll()
	{
		return map;
	}
}
