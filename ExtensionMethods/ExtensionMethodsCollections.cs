using System.Collections.ObjectModel;
using System.IO;

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

		public static IEnumerable<string> SplitIntoLines(this string input)
		{
			if (input == null)
			{
				yield break;
			}

			using (IO.StringReader reader = new IO.StringReader(input))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					yield return line;
				}
			}
		}

		public static bool IsValidFileName(this string input)
		{
			return !string.IsNullOrWhiteSpace(input) && input.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
		}

		public static string MakeRelativePath(this string pathOrigin, string targetPath)
		{
			Uri originUri = new Uri(pathOrigin);
			Uri targetUri = new Uri(targetPath);
			return originUri.MakeRelativeUri(targetUri).ToString();
		}

		public static bool IsFileWritable(this string filePath)
		{
			if (File.Exists(filePath))
			{
				FileAttributes attributes = File.GetAttributes(filePath);
				return !attributes.HasFlag(FileAttributes.ReadOnly);
			}

			return false;
		}

		public static bool MakeFileWritable(this string filePath)
		{
			if (File.Exists(filePath))
			{
				FileAttributes attributes = File.GetAttributes(filePath);
				File.SetAttributes(filePath, attributes &~ FileAttributes.ReadOnly);
				return true;
			}

			return false;
		}
	}
}
