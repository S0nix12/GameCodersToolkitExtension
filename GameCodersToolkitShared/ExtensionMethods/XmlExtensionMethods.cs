using Microsoft.VisualStudio.PlatformUI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;

namespace GameCodersToolkit.ExtensionMethods
{
	internal static class XmlExtensionMethods
	{
		public static IEnumerable<string> GetXPathValues(this XNode node, string xpath)
		{
			foreach (XObject xObject in (IEnumerable)node.XPathEvaluate(xpath))
			{
				if (xObject is XElement)
					yield return ((XElement)xObject).Value;
				else if (xObject is XAttribute)
					yield return ((XAttribute)xObject).Value;
			}
		}

		public static IEnumerable<string> GetValuesFromXPathResult(object xpathResult)
		{
			if (xpathResult is null)
			{
				yield break;
			}

			foreach (XObject xObject in (IEnumerable)xpathResult)
			{
				if (xObject is XElement)
					yield return ((XElement)xObject).Value;
				else if (xObject is XAttribute)
					yield return ((XAttribute)xObject).Value;
			}
		}

		public static string GetSingleValueFromXPathResult(object xpathResult)
		{
			if (xpathResult is string)
				return (string)xpathResult;

			IEnumerable xPathEnumerator = xpathResult as IEnumerable;
			foreach (XObject xObject in xPathEnumerator)
			{
				if (xObject is XAttribute attribute)
					return attribute.Value;

				if (xObject is XElement element)
					return element.Value;
			}

			return null;
		}
	}
}
