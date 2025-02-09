using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GameCodersToolkitShared.Utils
{
	public static class CodeParseUtils
	{
		public static string FirstLetterToUpper(this string str)
		{
			if (str == null)
				return null;

			if (str.Length > 1)
				return char.ToUpper(str[0]) + str.Substring(1);

			return str.ToUpper();
		}

		public static int FindMatchingBrace(string text, int startIndex)
		{
			int openBraceIndex = text.IndexOf('{', startIndex);
			if (openBraceIndex == -1)
			{
				return -1;
			}

			int depth = 0;

			for (int i = openBraceIndex; i < text.Length; i++)
			{
				if (text[i] == '{')
				{
					depth++;
				}
				else if (text[i] == '}')
				{
					depth--;
					if (depth == 0)
					{
						return i;
					}
				}
			}

			return -1;
		}

		public static int CountLeadingTabs(string text, int index)
		{
			if (string.IsNullOrEmpty(text) || index <= 0)
				return 0;

			int tabCount = 0;
			int i = index - 1;

			while (i >= 0 && text[i] == '\t')
			{
				tabCount++;
				i--;
			}

			return tabCount;
		}

		public static string IndentAllLines(string text, int tabCount)
		{
			if (string.IsNullOrEmpty(text) || tabCount <= 0)
				return text;

			string tabs = new string('\t', tabCount);
			return string.Join("\n", text.Split('\n').Select(line => tabs + line));
		}

		public static string FindNamespaceAtIndex(string cppCode, int index)
		{
			if (string.IsNullOrEmpty(cppCode) || index < 0 || index >= cppCode.Length)
				return string.Empty;

			Stack<(string Name, int BraceDepth)> namespaceStack = new();
			Regex namespaceRegex = new(@"namespace\s+([A-Za-z_][A-Za-z0-9_]*)\s*(\{)?", RegexOptions.Compiled);
			Regex braceRegex = new(@"[{}]", RegexOptions.Compiled);

			int braceDepth = 0; // Tracks ALL `{` and `}`
			string pendingNamespace = null;

			MatchCollection namespaceMatches = namespaceRegex.Matches(cppCode);
			MatchCollection braceMatches = braceRegex.Matches(cppCode);

			int nsIndex = 0, braceIndex = 0;
			int position = 0;

			while (position < index)
			{
				bool nsFound = nsIndex < namespaceMatches.Count && namespaceMatches[nsIndex].Index <= position;
				bool braceFound = braceIndex < braceMatches.Count && braceMatches[braceIndex].Index <= position;

				if (nsFound && (!braceFound || namespaceMatches[nsIndex].Index < braceMatches[braceIndex].Index))
				{
					pendingNamespace = namespaceMatches[nsIndex].Groups[1].Value;

					if (namespaceMatches[nsIndex].Groups[2].Success)
					{
						namespaceStack.Push((pendingNamespace, braceDepth));
						pendingNamespace = null;
					}

					nsIndex++;
				}
				else if (braceFound)
				{
					if (cppCode[braceMatches[braceIndex].Index] == '{')
					{
						if (pendingNamespace != null)
						{
							namespaceStack.Push((pendingNamespace, braceDepth));
							pendingNamespace = null;
						}
						braceDepth++;
					}
					else
					{
						braceDepth--;

						if (namespaceStack.Count > 0 && namespaceStack.Peek().BraceDepth == braceDepth)
						{
							namespaceStack.Pop();
						}
					}

					braceIndex++;
				}
				else
				{
					position++;
				}
			}

			return namespaceStack.Count > 0 ? string.Join("::", namespaceStack.Reverse().Select(ns => ns.Name)) : string.Empty;
		}
	}
}
