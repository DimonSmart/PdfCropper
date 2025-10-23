using System.Collections.Generic;
using iText.Kernel.Pdf;
using iText.Pdfoptimizer.Handlers.Fontduplication;
using iText.Pdfoptimizer.Util.Traversing;

namespace iText.Pdfoptimizer.Util;

public sealed class DocumentStructureUtils
{
	private DocumentStructureUtils()
	{
	}

	public static IList<PdfObject> Search(PdfDocument document, IPdfObjectPredicate predicate)
	{
		SearchAction searchAction = new SearchAction(predicate);
		Traverse(document, searchAction);
		return searchAction.GetFoundObjects();
	}

	public static void Traverse(PdfDocument document, IAction action)
	{
		foreach (PdfIndirectReference item in document.ListIndirectReferences())
		{
			PdfObject refersTo = item.GetRefersTo();
			action.ProcessIndirectObjectDefinition(refersTo);
			if (refersTo != null)
			{
				TraverseRecursively(refersTo, action);
			}
		}
	}

	public static IDictionary<PdfObject, PdfObject> GetSimilarDictionaries(PdfDocument document, ICollection<PdfObject> objects, PdfDictionaryEqualityCalculator eqCalculator)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		IDictionary<int, IList<PdfDictionary>> col = new Dictionary<int, IList<PdfDictionary>>();
		IDictionary<PdfObject, PdfObject> dictionary = new Dictionary<PdfObject, PdfObject>();
		foreach (PdfDictionary @object in objects)
		{
			PdfDictionary val = @object;
			int hashCode = eqCalculator.GetHashCode(val);
			IList<PdfDictionary> list = col.Get(hashCode);
			if (list == null)
			{
				list = new List<PdfDictionary>();
			}
			PdfDictionary val2 = FindCopy(val, list, eqCalculator);
			if (val2 == null)
			{
				list.Add(val);
				col.Put(hashCode, list);
			}
			else
			{
				dictionary.Put((PdfObject)(object)val, (PdfObject)(object)val2);
				((PdfObject)val2).MakeIndirect(document);
			}
		}
		return dictionary;
	}

	public static IList<IList<PdfObject>> GetSimilarDictionariesList(ICollection<PdfObject> objects, PdfDictionaryEqualityCalculator eqCalculator)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Expected O, but got Unknown
		IDictionary<int, IList<PdfObject>> dictionary = new Dictionary<int, IList<PdfObject>>();
		foreach (PdfDictionary @object in objects)
		{
			PdfDictionary val = @object;
			int hashCode = eqCalculator.GetHashCode(val);
			IList<PdfObject> list = dictionary.Get(hashCode);
			if (list == null)
			{
				list = new List<PdfObject>();
			}
			list.Add((PdfObject)(object)val);
			dictionary.Put(hashCode, list);
		}
		IList<IList<PdfObject>> list2 = new List<IList<PdfObject>>();
		foreach (IList<PdfObject> value in dictionary.Values)
		{
			if (value.Count > 1)
			{
				list2.Add(value);
			}
		}
		return list2;
	}

	private static PdfDictionary FindCopy(PdfDictionary font, IList<PdfDictionary> list, PdfDictionaryEqualityCalculator eqCalculator)
	{
		foreach (PdfDictionary item in list)
		{
			if (eqCalculator.AreEqual(font, item))
			{
				return item;
			}
		}
		return null;
	}

	private static void TraverseRecursively(PdfObject @object, IAction action)
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Expected O, but got Unknown
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Expected O, but got Unknown
		switch (@object.GetObjectType())
		{
		case 1:
			TraverseInArrayRecursively((PdfArray)@object, action);
			break;
		case 3:
		case 9:
			TraverseInDictionaryRecursively((PdfDictionary)@object, action);
			break;
		}
	}

	private static void TraverseInArrayRecursively(PdfArray array, IAction action)
	{
		for (int i = 0; i < array.Size(); i++)
		{
			PdfObject val = array.Get(i);
			PdfObject val2 = action.ProcessObject(val);
			if (val != val2)
			{
				array.Set(i, val2);
			}
			if (val2 != null && !val2.IsIndirect())
			{
				TraverseRecursively(val2, action);
			}
		}
	}

	private static void TraverseInDictionaryRecursively(PdfDictionary dictionary, IAction action)
	{
		PdfName[] toArray = (PdfName[])(object)new PdfName[dictionary.KeySet().Count];
		toArray = dictionary.KeySet().ToArray(toArray);
		PdfName[] array = toArray;
		foreach (PdfName val in array)
		{
			PdfObject val2 = dictionary.Get(val);
			PdfObject val3 = action.ProcessObject(val2);
			if (val2 != val3)
			{
				dictionary.Put(val, val3);
			}
			if (val3 != null && !val3.IsIndirect())
			{
				TraverseRecursively(val3, action);
			}
		}
	}
}
