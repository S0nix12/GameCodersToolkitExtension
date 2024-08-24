using System.Collections.ObjectModel;

namespace System.Collections.Generic
{
	internal static class ExtensionMethodsCollections
	{
		public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
		where TValue : new()
		{
			if (!dict.TryGetValue(key, out TValue val))
			{
				val = new TValue();
				dict.Add(key, val);
			}

			return val;
		}

		public static void RemoveAll<T>(this ObservableCollection<T> collection,
													   Func<T, bool> condition)
		{
			for (int i = collection.Count - 1; i >= 0; i--)
			{
				if (condition(collection[i]))
				{
					collection.RemoveAt(i);
				}
			}
		}
	}
}
