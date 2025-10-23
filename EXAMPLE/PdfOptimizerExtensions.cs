using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

internal static class PdfOptimizerExtensions
{
	public static bool HasNextLine(this StreamReader str)
	{
		return !str.EndOfStream;
	}

	public static byte[] GetBytes(this string str, Encoding encoding)
	{
		return encoding.GetBytes(str);
	}

	public static void Write(this Stream stream, byte[] buffer)
	{
		stream.Write(buffer, 0, buffer.Length);
	}

	public static bool IsEmpty<T1, T2>(this ICollection<KeyValuePair<T1, T2>> collection)
	{
		return collection.Count == 0;
	}

	public static bool IsEmpty<T>(this ICollection<T> collection)
	{
		return collection.Count == 0;
	}

	public static void AddAll<T>(this ICollection<T> c, IEnumerable<T> collectionToAdd)
	{
		foreach (T item in collectionToAdd)
		{
			c.Add(item);
		}
	}

	public static void AddAll<TKey, TValue>(this IDictionary<TKey, TValue> c, IDictionary<TKey, TValue> collectionToAdd)
	{
		foreach (KeyValuePair<TKey, TValue> item in collectionToAdd)
		{
			c[item.Key] = item.Value;
		}
	}

	public static void AddAll<T>(this Stack<T> c, Stack<T> collectionToAdd)
	{
		foreach (T item in collectionToAdd)
		{
			c.Push(item);
		}
	}

	public static TValue Get<TKey, TValue>(this IDictionary<TKey, TValue> col, TKey key)
	{
		TValue value = default(TValue);
		if (key != null)
		{
			col.TryGetValue(key, out value);
		}
		return value;
	}

	public static TValue Put<TKey, TValue>(this IDictionary<TKey, TValue> col, TKey key, TValue value)
	{
		TValue result = col.Get(key);
		col[key] = value;
		return result;
	}

	public static object Get(this IDictionary col, object key)
	{
		object result = null;
		if (key != null)
		{
			result = col[key];
		}
		return result;
	}

	public static void Put(this IDictionary col, object key, object value)
	{
		if (key != null)
		{
			col[key] = value;
		}
	}

	public static T[] ToArray<T>(this ICollection<T> col, T[] toArray)
	{
		int count = col.Count;
		T[] array;
		if (count <= toArray.Length)
		{
			col.CopyTo(toArray, 0);
			if (count != toArray.Length)
			{
				toArray[count] = default(T);
			}
			array = toArray;
		}
		else
		{
			array = new T[count];
			col.CopyTo(array, 0);
		}
		return array;
	}

	public static bool Add<T>(this LinkedList<T> list, T elem)
	{
		list.AddLast(elem);
		return true;
	}

	public static T JGetLast<T>(this LinkedList<T> list)
	{
		return list.Last.Value;
	}

	public static Assembly GetAssembly(this Type type)
	{
		return type.Assembly;
	}

	public static Attribute GetCustomAttribute(this Assembly assembly, Type attributeType)
	{
		object[] customAttributes = assembly.GetCustomAttributes(attributeType, inherit: false);
		if (customAttributes.Length != 0 && customAttributes[0] is Attribute)
		{
			return customAttributes[0] as Attribute;
		}
		return null;
	}
}
