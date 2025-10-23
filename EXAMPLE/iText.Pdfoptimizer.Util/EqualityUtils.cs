using System.Collections.Generic;
using iText.Commons.Utils;
using iText.Kernel.Pdf;

namespace iText.Pdfoptimizer.Util;

public sealed class EqualityUtils
{
	private const int DEFAULT_HASH = 31;

	private static readonly ICollection<byte> SIMPLE_OBJECTS;

	static EqualityUtils()
	{
		SIMPLE_OBJECTS = new HashSet<byte>();
		SIMPLE_OBJECTS.Add(7);
		SIMPLE_OBJECTS.Add(2);
		SIMPLE_OBJECTS.Add(4);
		SIMPLE_OBJECTS.Add(10);
		SIMPLE_OBJECTS.Add(6);
	}

	private EqualityUtils()
	{
	}

	public static int GetHashCode(PdfObject @object)
	{
		return GetHashCodeAvoidRecursion(@object, new HashSet<PdfIndirectReference>(), new Dictionary<PdfIndirectReference, int?>());
	}

	public static bool AreEqual(PdfObject obj1, PdfObject obj2)
	{
		return AreEqualAvoidRecursion(obj1, obj2, new HashSet<SymmetricPair>());
	}

	private static int GetHashCodeAvoidRecursion(PdfObject @object, ICollection<PdfIndirectReference> calculating, IDictionary<PdfIndirectReference, int?> calculated)
	{
		if (@object == null)
		{
			return 0;
		}
		PdfIndirectReference val = null;
		if (@object.IsIndirect())
		{
			val = @object.GetIndirectReference();
			if (val == null)
			{
				return 31;
			}
			if (calculated.Get(val).HasValue)
			{
				return calculated.Get(val).Value;
			}
			if (calculating.Contains(val))
			{
				return 31;
			}
			calculating.Add(val);
		}
		int hashCodePlain = GetHashCodePlain(@object, calculating, calculated);
		if (@object.IsIndirect())
		{
			calculating.Remove(val);
			calculated.Put(val, hashCodePlain);
		}
		return hashCodePlain;
	}

	private static int GetHashCodePlain(PdfObject @object, ICollection<PdfIndirectReference> calculating, IDictionary<PdfIndirectReference, int?> calculated)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Expected O, but got Unknown
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a4: Expected O, but got Unknown
		byte objectType = @object.GetObjectType();
		int num;
		if (SIMPLE_OBJECTS.Contains(objectType))
		{
			num = ((object)@object).GetHashCode();
		}
		else
		{
			switch (objectType)
			{
			case 8:
			{
				long num2 = JavaUtil.DoubleToLongBits(((PdfNumber)@object).GetValue());
				num = (int)(num2 ^ (num2 >>> 32));
				break;
			}
			case 1:
			{
				PdfArray val2 = (PdfArray)@object;
				num = 1;
				foreach (PdfObject item in val2)
				{
					num = 31 * num + GetHashCodeAvoidRecursion(item, calculating, calculated);
				}
				break;
			}
			case 3:
				num = GetHashForDictionary((PdfDictionary)@object, calculating, calculated);
				break;
			case 9:
			{
				PdfStream val = (PdfStream)@object;
				num = GetHashForDictionary((PdfDictionary)val, calculating, calculated);
				byte[] bytes = val.GetBytes();
				num = 31 * num + ((bytes != null) ? JavaUtil.ArraysHashCode<byte>(bytes) : 0);
				break;
			}
			default:
				num = 0;
				break;
			}
		}
		return num;
	}

	private static int GetHashForDictionary(PdfDictionary dict, ICollection<PdfIndirectReference> calculating, IDictionary<PdfIndirectReference, int?> calculated)
	{
		int num = 0;
		foreach (PdfName item in dict.KeySet())
		{
			num += GetHashCodeAvoidRecursion((PdfObject)(object)item, calculating, calculated) ^ GetHashCodeAvoidRecursion(dict.Get(item), calculating, calculated);
		}
		return num;
	}

	private static bool AreEqualAvoidRecursion(PdfObject obj1, PdfObject obj2, ICollection<SymmetricPair> calculated)
	{
		if (obj1 == obj2)
		{
			return true;
		}
		if ((obj1 == null) ^ (obj2 == null))
		{
			return false;
		}
		if (obj1.GetObjectType() != obj2.GetObjectType())
		{
			return false;
		}
		if (obj1.IsIndirect() && obj2.IsIndirect())
		{
			PdfIndirectReference indirectReference = obj1.GetIndirectReference();
			PdfIndirectReference indirectReference2 = obj2.GetIndirectReference();
			if (indirectReference == null || indirectReference2 == null)
			{
				return false;
			}
			SymmetricPair item = new SymmetricPair(indirectReference, indirectReference2);
			if (calculated.Contains(item))
			{
				return true;
			}
			calculated.Add(item);
		}
		return AreEqualPlain(obj1, obj2, calculated);
	}

	private static bool AreEqualPlain(PdfObject obj1, PdfObject obj2, ICollection<SymmetricPair> calculated)
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Expected O, but got Unknown
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Expected O, but got Unknown
		//IL_0059: Expected O, but got Unknown
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Expected O, but got Unknown
		//IL_0072: Expected O, but got Unknown
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected O, but got Unknown
		byte objectType = obj1.GetObjectType();
		if (SIMPLE_OBJECTS.Contains(objectType))
		{
			return ((object)obj1).Equals((object)obj2);
		}
		switch (objectType)
		{
		case 8:
		{
			PdfNumber val4 = (PdfNumber)obj1;
			PdfNumber val5 = (PdfNumber)obj2;
			return JavaUtil.DoubleCompare(val4.GetValue(), val5.GetValue()) == 0;
		}
		case 1:
		{
			PdfArray val3 = (PdfArray)obj1;
			PdfArray array = (PdfArray)obj2;
			return AreEqualPdfArrays(val3, array, calculated);
		}
		case 3:
		{
			PdfDictionary val6 = (PdfDictionary)obj1;
			PdfDictionary dict = (PdfDictionary)obj2;
			return AreEqualPdfDictionaries(val6, dict, calculated);
		}
		case 9:
		{
			PdfStream val = (PdfStream)obj1;
			PdfStream val2 = (PdfStream)obj2;
			if (!AreEqualPdfDictionaries((PdfDictionary)(object)val, (PdfDictionary)(object)val2, calculated))
			{
				return false;
			}
			return JavaUtil.ArraysEquals<byte>(val.GetBytes(), val2.GetBytes());
		}
		default:
			return false;
		}
	}

	private static bool AreEqualPdfArrays(PdfArray array1, PdfArray array2, ICollection<SymmetricPair> calculated)
	{
		if (array1.Size() != array2.Size())
		{
			return false;
		}
		for (int i = 0; i < array1.Size(); i++)
		{
			if (!AreEqualAvoidRecursion(array1.Get(i), array2.Get(i), calculated))
			{
				return false;
			}
		}
		return true;
	}

	private static bool AreEqualPdfDictionaries(PdfDictionary dict1, PdfDictionary dict2, ICollection<SymmetricPair> calculated)
	{
		ICollection<PdfName> collection = dict1.KeySet();
		ICollection<PdfName> collection2 = dict2.KeySet();
		if (collection.Count != collection2.Count)
		{
			return false;
		}
		foreach (PdfName item in collection)
		{
			if (!AreEqualAvoidRecursion(dict1.Get(item), dict2.Get(item), calculated))
			{
				return false;
			}
		}
		return true;
	}
}
