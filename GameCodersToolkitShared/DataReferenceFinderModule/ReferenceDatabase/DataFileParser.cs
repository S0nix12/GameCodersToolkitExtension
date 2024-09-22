using GameCodersToolkit.ExtensionMethods;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase
{
	public class DataParsingErrorList
	{
		public void DumpToOutput(System.IO.TextWriter output)
		{
			if (ErrorMessages.Count > 0 || WarningMessages.Count > 0)
			{
				output.WriteLine("Issues parsing file: " + FilePath);
				foreach (var message in WarningMessages)
				{
					output.WriteLine(message);
				}

				foreach (var message in ErrorMessages)
				{
					output.WriteLine(message);
				}
			}
		}

		public bool HasEntries()
		{
			return WarningMessages.Count > 0 || ErrorMessages.Count > 0;
		}

		public void PushContext(string context)
		{
			ContextStack.Push(context);
		}

		public void PopContext()
		{
			ContextStack.Pop();
		}

		public void Warning(int lineNumber, string message)
		{
			string context = ContextStack.Count > 0 ? ContextStack.Peek() : "Data Element";
			WarningMessages.Add(string.Format("[Warning] parsing {0}. File: {1} | Line: {2}. {3}", context, FilePath, lineNumber, message));
		}

		public void Error(int lineNumber, string message)
		{
			string context = ContextStack.Count > 0 ? ContextStack.Peek() : "Data Element";
			ErrorMessages.Add(string.Format("[Error] parsing {0}. File: {1} | Line: {2}. {3}", context, FilePath, lineNumber, message));
		}

		public string FilePath { get; set; }
		List<string> WarningMessages { get; set; } = new List<string>();
		List<string> ErrorMessages { get; set; } = new List<string>();
		Stack<string> ContextStack { get; set; } = new Stack<string>();
	}

	public class DataParsingDescription
	{
		// Name to identify this parsing description
		public string Name { get; set; }

		// Type of data parsed with this description. If empty uses the name of the parsing Description
		public string TypeName { get; set; }

		// All expressions are using XPath syntax. After the BaseExpression all others are relative to the base Element
		// All expressions should evaluate to a string value except the BaseExpression which should select elements

		// Gather all Elements which contain a data entry for this description
		public string BaseExpression { get; set; }

		// Path from the Base Element to get the Identifier of the Data Element. Either provide an expression that selects a single Guid.
		// Or an expression that selects multiple elements for which the Text values is accquired and combine for a Hashed String Identifier
		// If kept empty the Line Number of the Base Element is combined with the filename as Hashed String Identifier
		public string EntryIdentifierExpression { get; set; }

		// Expressions to select Elements that for an Identifier to an referenced Data Entry.
		// As all Identifier expressions these need to either select a single Guid or one or more other elements for which the Text values are combined as a Hashed String
		public string[] ReferencedIdentifierExpressions { get; set; }

		// Expression to get the Name of the Data Entry relative. Can refer to multiple Elements for which the Text gets combined
		// If left empty uses the name of the Base Element. 
		public string NameExpression { get; set; }

		// Optional expression to get an Identifier for a Parent Data Entry. This Entry needs to be in the same file to get resolved properly.
		public string ParentIdentifierExpression { get; set; }

		// Optional expression to get a Parent Name. Can be used instead of a ParentIdentifier if the Parent is not a DataEntry itself and just used for better context
		public string ParentNameExpression { get; set; }

		// Optional expression to get the Sub Type of this Data Element. Used to group entries by in the Data Explorer
		public string SubTypeExpression { get; set; }
	}

	internal class DataFileParser
	{
		public DataFileParser(string content, string filePath, List<DataParsingDescription> dataParsingDescriptions)
		{
			m_fileContent = content;
			FilePath = filePath;
			m_parsingDescriptions = dataParsingDescriptions;
		}

		public List<DataEntry> Parse(DataParsingErrorList errorOutput)
		{
			List<DataEntry> outEntries = new List<DataEntry>();
			XDocument xmlDocument = XDocument.Parse(m_fileContent, LoadOptions.SetLineInfo);
			foreach (DataParsingDescription parsingDescription in m_parsingDescriptions)
			{
				outEntries.AddRange(Parse(xmlDocument, parsingDescription, errorOutput));
			}

			// Resolve Parent Entries
			foreach (var dataEntry in outEntries.Where(entry => entry.Parent != null))
			{
				DataEntry parent = outEntries.Find(entry => entry.Identifier.Equals(dataEntry.Parent.Identifier));
				if (parent != null)
				{
					dataEntry.Parent = parent;
				}
				else
				{
					dataEntry.Parent = null;
				}
			}

			return outEntries;
		}

		private List<DataEntry> Parse(XDocument xmlDocument, DataParsingDescription parsingDescription, DataParsingErrorList errorOutput)
		{
			List<DataEntry> outEntries = new List<DataEntry>();
			if (parsingDescription.BaseExpression == null)
			{
				return outEntries;
			}

			IEnumerable<XElement> baseElements = xmlDocument.XPathSelectElements(parsingDescription.BaseExpression);

			foreach (XElement baseElement in baseElements)
			{
				DataEntry parsedEntry = ParseDataElement(baseElement, parsingDescription, errorOutput);
				parsedEntry.BaseType = string.IsNullOrEmpty(parsingDescription.TypeName) ? parsingDescription.Name : parsingDescription.TypeName;
				if (parsedEntry != null)
				{
					outEntries.Add(parsedEntry);
				}
			}

			return outEntries;
		}

		private DataEntry ParseDataElement(XElement element, DataParsingDescription parsingDescription, DataParsingErrorList errorOutput)
		{
			DataEntry outEntry = new DataEntry();
			IXmlLineInfo lineInfo = element;
			outEntry.SourceLineNumber = lineInfo.LineNumber;
			outEntry.SourceFile = FilePath;

			// Parse identifier
			if (string.IsNullOrWhiteSpace(parsingDescription.EntryIdentifierExpression))
			{
				string identifierString = FilePath + "_" + lineInfo.LineNumber;
				outEntry.Identifier = new GenericDataIdentifier(identifierString);
			}
			else
			{
				errorOutput.PushContext("Entry Identifier");
				outEntry.Identifier = ParseIdentifierObject(element, parsingDescription.EntryIdentifierExpression, errorOutput);
				errorOutput.PopContext();
			}

			if (outEntry.Identifier == null)
			{
				errorOutput.Error(lineInfo.LineNumber, "No Identifier found for Element");
				return null;
			}

			for (int i = 0; i < parsingDescription.ReferencedIdentifierExpressions.Length; i++)
			{
				string referenceExpression = parsingDescription.ReferencedIdentifierExpressions[i];
				errorOutput.PushContext("Reference Identifier_" + i);

				GenericDataIdentifier referencedIdentifier = ParseIdentifierObject(element, referenceExpression, null);
				if (referencedIdentifier != null)
				{
					outEntry.References.Add(referencedIdentifier);
				}

				errorOutput.PopContext();
			}

			// Parse Data Entry name
			if (parsingDescription.NameExpression != null)
			{
				string nameResult = XmlExtensionMethods.GetSingleValueFromXPathResult(element.XPathEvaluate(parsingDescription.NameExpression));
				if (nameResult != null)
				{
					outEntry.Name = nameResult;
				}
				else
				{
					errorOutput.Error(lineInfo.LineNumber, "Name Expression did not find anything");
				}
			}
			else
			{
				outEntry.Name = element.Name.LocalName;
				string elementValue = element.Attribute("value")?.Value;
				if (!string.IsNullOrWhiteSpace(elementValue))
				{
					outEntry.Name += "_" + elementValue;
				}
			}


			errorOutput.PushContext("Parent");
			if (parsingDescription.ParentIdentifierExpression != null)
			{
				DataEntry parentPrototype = new DataEntry();
				parentPrototype.Identifier = ParseIdentifierObject(element, parsingDescription.ParentIdentifierExpression, null);
				if (parentPrototype.Identifier != null)
				{
					outEntry.Parent = parentPrototype;
				}
			}
			else if (parsingDescription.ParentNameExpression != null)
			{
				string parentName = XmlExtensionMethods.GetSingleValueFromXPathResult(element.XPathEvaluate(parsingDescription.ParentNameExpression));
				if (parentName != null)
				{
					outEntry.ParentName = parentName;
				}
				else
				{
					errorOutput.Error(lineInfo.LineNumber, "ParentName failed to parse");
				}
			}
			errorOutput.PopContext();

			errorOutput.PushContext("SubType");
			if (!string.IsNullOrEmpty(parsingDescription.SubTypeExpression))
			{
				string subTypeName = XmlExtensionMethods.GetSingleValueFromXPathResult(element.XPathEvaluate(parsingDescription.SubTypeExpression));
				if (subTypeName != null)
				{
					outEntry.SubType = subTypeName;
				}
				else
				{
					errorOutput.Error(lineInfo.LineNumber, "SubType failed to parse");
				}
			}

			return outEntry;
		}

		private GenericDataIdentifier? ParseIdentifierObject(XElement element, string expression, DataParsingErrorList? errorOutput)
		{
			IXmlLineInfo lineInfo = element;

			string[] multiExpressions = expression.Split('|');
			string combinedIdentifier = "";
			foreach (string singleExpression in multiExpressions)
			{
				string result = XmlExtensionMethods.GetSingleValueFromXPathResult(element.XPathEvaluate(singleExpression));
				if (result != null)
				{
					combinedIdentifier += result;
				}
			}

			if (string.IsNullOrWhiteSpace(combinedIdentifier))
			{
				errorOutput?.Error(lineInfo.LineNumber, "No Identifier Elements found");
				return null;
			}

			if (Guid.TryParse(combinedIdentifier, out Guid guid))
			{
				return new GenericDataIdentifier(guid);
			}
			else
			{
				return new GenericDataIdentifier(combinedIdentifier);
			}
		}

		string m_fileContent;
		List<DataParsingDescription> m_parsingDescriptions;

		public string FilePath { get; private set; }
	}
}