using GameCodersToolkit.Configuration;
using GameCodersToolkit.Utils;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using static System.Net.Mime.MediaTypeNames;
using System.Reflection;
using GameCodersToolkitShared.Utils;

namespace GameCodersToolkit.AutoDataExposerModule
{
	public class FunctionArgument
	{
		public int Index { get; set; }
		public bool IsOut { get; set; }
		public string RawType { get; set; }
		public string Type { get; set; }
		public string Name { get; set; }
	}

	public class ExposedFunctionInfo
	{
		public string FullyQualifiedFunctionName { get; set; }
		public string FunctionName { get; set; }
		public List<FunctionArgument> Arguments { get; set; }
	}

	[Command(PackageGuids.AutoDataExposerSet_GuidString, PackageIds.ExposeToDataCommand)]
	internal sealed class ImplementRegistrationCodeCommand : BaseCommand<ImplementRegistrationCodeCommand>
	{
		const string c_propertyDictionaryIdentifier = "dataExposerEntry";
		private List<OleMenuCommand> menuCommands = new List<OleMenuCommand>();


		private CAutoDataExposerConfiguration Config { get { return GameCodersToolkitPackage.AutoDataExposerConfig; } }

		protected override void Execute(object sender, EventArgs e)
		{
			if (sender is OleMenuCommand command)
			{
				string entryName = command.Properties[c_propertyDictionaryIdentifier] as string;

				CAutoDataExposerEntry entry = Config.FindExposerEntryByName(entryName);
				if (entry != null)
				{
					ThreadHelper.JoinableTaskFactory.Run(() => GenerateExposeCodeAsync(entry));
				}
			}
		}

		private int FindMatchingBrace(string text, int startIndex)
		{
			// Find the first '{' after or at startIndex
			int openBraceIndex = text.IndexOf('{', startIndex);
			if (openBraceIndex == -1)
			{
				Console.WriteLine("No '{' found after the given index.");
				return -1;
			}

			int depth = 0; // Stack counter for nested braces

			for (int i = openBraceIndex; i < text.Length; i++)
			{
				if (text[i] == '{')
				{
					depth++; // Push onto stack
				}
				else if (text[i] == '}')
				{
					depth--; // Pop from stack
					if (depth == 0)
					{
						return i; // Found the matching '}'
					}
				}
			}

			return -1; // No matching '}' found
		}

		List<FunctionArgument> ExtractParameters(string functionSignatureLine, string currentNamespace, CAutoDataExposerEntry entry)
		{
			List<FunctionArgument> parameters = new List<FunctionArgument>();
			int currentArgumentIndex = 0;

			// Return value
			Match returnValueRegex = Regex.Match(functionSignatureLine, entry.FunctionReturnValueRegex);
			if (!returnValueRegex.Success)
			{
				throw new ArgumentException($"Failed to parse function return value: {functionSignatureLine}");
			}

			string returnValueType = returnValueRegex.Groups[1].Value.Trim();
			if (returnValueType != "void")
			{
				FunctionArgument argument = new FunctionArgument();
				argument.IsOut = true;
				argument.Name = "Result";
				argument.RawType = returnValueType;
				argument.Type = returnValueType;
				argument.Index = currentArgumentIndex++;

				if (argument.Type.StartsWith(currentNamespace))
				{
					argument.Type = argument.Type.Substring(currentNamespace.Length + 2); //Take :: into account
				}

				parameters.Add(argument);
			}

			// Arguments
			Match argumentRegex = Regex.Match(functionSignatureLine, entry.FunctionArgumentsRegex);

			if (!argumentRegex.Success)
			{
				throw new ArgumentException($"Failed to parse function arguments: {functionSignatureLine}");
			}

			string parametersText = argumentRegex.Groups[1].Value.Trim();

			if (string.IsNullOrEmpty(parametersText))
			{
				return parameters;
			}

			string[] paramList = parametersText.Split(',');

			foreach (string param in paramList)
			{
				string[] parts = param.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				if (parts.Length < 2)
				{
					throw new ArgumentException($"Invalid parameter format: '{param.Trim()}'");
				}

				FunctionArgument argument = new FunctionArgument();

				argument.Name = parts[parts.Length - 1];
				argument.RawType = string.Join(" ", parts, 0, parts.Length - 1);
				argument.IsOut = argument.RawType.Contains("&") && !argument.RawType.Contains("const");
				argument.Type = argument.RawType.Replace("const", "").Replace("*", "").Replace("&", "").Trim();
				argument.Index = currentArgumentIndex++;

				if (argument.Type.StartsWith(currentNamespace))
				{
					argument.Type = argument.Type.Substring(currentNamespace.Length + 2); //Take :: into account
				}

				parameters.Add(argument);
			}

			parameters = parameters.OrderBy(arg => arg.Index).ToList();
			return parameters;
		}

		private async Task<bool> GenerateExposeCodeAsync(CAutoDataExposerEntry entry)
		{
			try
			{
				DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
				if (documentView == null || documentView.TextView == null)
					return false;

				ITextSnapshot snapshot = documentView.TextView.TextBuffer.CurrentSnapshot;
				using ITextEdit edit = documentView.TextView.TextBuffer.CreateEdit();
				string documentText = snapshot.GetText();

				Regex registrationFunctionRegex = new Regex(entry.TargetFunctionRegex, RegexOptions.Multiline);
				Match registrationFunctionMatch = registrationFunctionRegex.Match(documentText);
				if (registrationFunctionMatch.Success)
				{
					int endBraceIndex = FindMatchingBrace(documentText, registrationFunctionMatch.Index);
					if (endBraceIndex != -1)
					{
						SnapshotPoint caretSnapshotPoint = documentView.TextView.Caret.Position.BufferPosition;
						ITextSnapshotLine caretLineSnapshot = caretSnapshotPoint.GetContainingLine();
						string currentSelectedLine = caretLineSnapshot.GetText().Trim();
						string registerFunctionNamespace = CodeParseUtils.FindNamespaceAtIndex(documentText, endBraceIndex);
						var arguments = ExtractParameters(currentSelectedLine, registerFunctionNamespace, entry);

						ExposedFunctionInfo info = new ExposedFunctionInfo();
						info.Arguments = arguments;
						FillFunctionNames(currentSelectedLine, entry, info);

						string exposeString = GetTokenizedString(entry.ExposeString, entry, info);
						int indentLevel = CodeParseUtils.CountLeadingTabs(documentText, endBraceIndex);
						exposeString = CodeParseUtils.IndentAllLines(exposeString, indentLevel + 1);
						exposeString += Environment.NewLine;

						if (indentLevel > 0)
						{
							exposeString = exposeString.Substring(indentLevel);
						}

						for (int i = 0; i < indentLevel; i++)
						{
							exposeString += '\t';
						}

						edit.Insert(endBraceIndex, exposeString);
						edit.Apply();
				
						return true;
					}
				}

				return false;
			}
			catch (Exception e)
			{
				await VS.MessageBox.ShowAsync(e.Message);
				return false;
			}
		}

		private void FillFunctionNames(string functionSignatureLine, CAutoDataExposerEntry entry, ExposedFunctionInfo info)
		{
			Match returnValueRegex = Regex.Match(functionSignatureLine, entry.FunctionNameRegex);
			if (!returnValueRegex.Success)
			{
				throw new ArgumentException($"Failed to parse function name: {functionSignatureLine}");
			}

			info.FullyQualifiedFunctionName = returnValueRegex.Groups[1].Value + returnValueRegex.Groups[2].Value;
			info.FunctionName = returnValueRegex.Groups[2].Value;
		}

		private string GetTokenizedString(string str, CAutoDataExposerEntry entry, ExposedFunctionInfo info)
		{
			string authorName = GameCodersToolkitPackage.FileTemplateCreatorConfig.UserConfig.AuthorName;
			if (string.IsNullOrWhiteSpace(authorName))
			{
				authorName = "AuthorName";
			}

			str = str.Replace("##FUNCTIONNAME##", info.FullyQualifiedFunctionName);
			str = str.Replace("##GUID##", Guid.NewGuid().ToString());
			str = str.Replace("##AUTHORNAME##", authorName);
			str = str.Replace("##PARAMS##", GenerateParamString(entry, info));

			return str;
		}

		private string GenerateParamString(CAutoDataExposerEntry entry, ExposedFunctionInfo info)
		{
			string result = "";

			foreach (var argument in info.Arguments)
			{
				string paramLine = argument.IsOut ? entry.OutParamLine : entry.InParamLine;

				paramLine = paramLine.Replace("##PARAMINDEX##", argument.Index.ToString());
				paramLine = paramLine.Replace("##PARAMNAME##", argument.Name.FirstLetterToUpper());
				paramLine = paramLine.Replace("##RAWPARAMNAME##", argument.Name);
				paramLine = paramLine.Replace("##PARAMDEFAULTVALUE##", GetParamDefaultValue(entry, argument));
				result += paramLine + Environment.NewLine;
			}

			return result;
		}

		private string GetParamDefaultValue(CAutoDataExposerEntry entry, FunctionArgument arg)
		{
			var defaultValue = GameCodersToolkitPackage.AutoDataExposerConfig.ExposerConfig.DefaultValues?.Where(val => val.TypeName == arg.Type).FirstOrDefault();
			if (defaultValue != null)
			{
				return defaultValue.DefaultValue;
			}

			return entry.DefaultValueFormat.Replace("##TYPE##", arg.Type);
		}

		protected override void BeforeQueryStatus(EventArgs e)
		{
			var entries = GameCodersToolkitPackage.AutoDataExposerConfig.ExposerConfig.AutoDataExposerEntries;

			if (entries.Count == 0)
			{
				return;
			}

			DetermineVisibility(Command, new EventArgs());

			if (menuCommands.Count > 0)
			{
				return;
			}

			OleMenuCommandService mcs = Package.GetService<IMenuCommandService, OleMenuCommandService>();

			SetupCommand(Command, entries.First());

			for (int i = 1; i < entries.Count; i++)
			{
				Configuration.CAutoDataExposerEntry entry = GameCodersToolkitPackage.AutoDataExposerConfig.ExposerConfig.AutoDataExposerEntries[i];

				CommandID cmdId = new(PackageGuids.AutoDataExposerSet_Guid, PackageIds.ExposeToDataCommand + i);

				OleMenuCommand command = new(Execute, null, DetermineVisibility, cmdId);
				SetupCommand(command, entry);
				mcs.AddCommand(command);
			}
		}

		private void DetermineVisibility(object sender, EventArgs e)
		{
			if (sender is OleMenuCommand command)
			{
				string entryName = command.Properties[c_propertyDictionaryIdentifier] as string;

				CAutoDataExposerEntry entry = Config.FindExposerEntryByName(entryName);
				if (entry != null)
				{
					command.Enabled = entry != null ? ThreadHelper.JoinableTaskFactory.Run(() => IsLineMatchingRegex(entry.LineValidityRegex)) : false;
				}
				else
				{
					command.Enabled = false;
				}
			}
		}

		private void SetupCommand(OleMenuCommand command, CAutoDataExposerEntry entry)
		{
			command.Text = entry.Name;
			command.Visible = true;
			command.Enabled = true;
			command.Properties[c_propertyDictionaryIdentifier] = entry.Name;
			menuCommands.Add(command);
		}

		private static async Task<bool> IsLineMatchingRegex(string pattern)
		{
			if (string.IsNullOrWhiteSpace(pattern))
			{
				return true;
			}

			DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
			if (documentView == null || documentView.TextView == null)
				return false;

			SnapshotPoint caretSnapshotPoint = documentView.TextView.Caret.Position.BufferPosition;
			ITextSnapshotLine caretLineSnapshot = caretSnapshotPoint.GetContainingLine();

			string line = caretLineSnapshot.GetText().Trim();
			return Regex.IsMatch(line, pattern);
		}
	}
}