using GameCodersToolkit.Configuration;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Text;
using System.Threading.Tasks;
using GameCodersToolkitShared.Utils;
using GameCodersToolkit.Utils;

namespace GameCodersToolkit.AutoDataExposerModule
{
	[Command(PackageGuids.AutoDataExposerSet_GuidString, PackageIds.ExposeToDataCommand)]
	internal sealed class ImplementRegistrationCodeCommand : BaseCommand<ImplementRegistrationCodeCommand>
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

		const string c_propertyDictionaryIdentifier = "dataExposerEntry";

		private CAutoDataExposerUserConfig UserConfig { get { return GameCodersToolkitPackage.AutoDataExposerConfig.GetConfig<CAutoDataExposerUserConfig>(); } }
		private CAutoDataExposerConfig MainConfig { get { return GameCodersToolkitPackage.AutoDataExposerConfig.GetConfig<CAutoDataExposerConfig>(); } }
		private List<OleMenuCommand> MenuCommands { get; set; } = [];
		private bool SubscribedToDataExposer { get; set; } = false;

		protected override void Execute(object sender, EventArgs e)
		{
			if (sender is OleMenuCommand command)
			{
				string entryName = command.Properties[c_propertyDictionaryIdentifier] as string;

				CAutoDataExposerEntry entry = MainConfig.FindExposerEntryByName(entryName);
				if (entry != null)
				{
					ThreadHelper.JoinableTaskFactory.Run(() => GenerateExposeCodeAsync(entry));
				}
			}
		}

		List<FunctionArgument> ExtractParameters(string functionSignatureLine, string currentNamespace, CAutoDataExposerEntry entry)
		{
			List<FunctionArgument> parameters = [];
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
				FunctionArgument argument = new()
				{
					IsOut = true,
					Name = "Result",
					RawType = returnValueType,
					Type = returnValueType,
					Index = currentArgumentIndex++
				};

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
				string[] parts = param.Trim().Split([' '], StringSplitOptions.RemoveEmptyEntries);

				if (parts.Length < 2)
				{
					throw new ArgumentException($"Invalid parameter format: '{param.Trim()}'");
				}

				FunctionArgument argument = new()
				{
					Name = parts[parts.Length - 1],
					RawType = string.Join(" ", parts, 0, parts.Length - 1),
				};

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

		private async Task GenerateExposeCodeAsync(CAutoDataExposerEntry entry)
		{
			try
			{
				DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
				if (documentView == null || documentView.TextView == null)
				{
					throw new Exception("Unable to load active document/text. You need to have a document opened for this command to work.");
				}

				ITextSnapshot snapshot = documentView.TextView.TextBuffer.CurrentSnapshot;
				using ITextEdit edit = documentView.TextView.TextBuffer.CreateEdit();
				string documentText = snapshot.GetText();

				Regex registrationFunctionRegex = new(entry.TargetFunctionRegex, RegexOptions.Multiline);
				Match registrationFunctionMatch = registrationFunctionRegex.Match(documentText);
				if (registrationFunctionMatch.Success)
				{
					int endBraceIndex = CodeParseUtils.FindMatchingBrace(documentText, registrationFunctionMatch.Index);
					if (endBraceIndex != -1)
					{
						SnapshotPoint caretSnapshotPoint = documentView.TextView.Caret.Position.BufferPosition;
						ITextSnapshotLine caretLineSnapshot = caretSnapshotPoint.GetContainingLine();
						string currentSelectedLine = caretLineSnapshot.GetText().Trim();
						string registerFunctionNamespace = CodeParseUtils.FindNamespaceAtIndex(documentText, endBraceIndex);
						string selectedFunctionNamespace = CodeParseUtils.FindNamespaceAtIndex(documentText, caretSnapshotPoint.Position);
						var arguments = ExtractParameters(currentSelectedLine, registerFunctionNamespace, entry);

						ExposedFunctionInfo info = new()
						{
							Arguments = arguments
						};
						FillFunctionNames(currentSelectedLine, registerFunctionNamespace, selectedFunctionNamespace, entry, info);

						string exposeString = GetTokenizedString(entry.ExposeString, entry, info);
						int indentLevel = CodeParseUtils.CountLeadingTabs(documentText, endBraceIndex);
						exposeString = CodeParseUtils.IndentAllLines(exposeString, indentLevel + 1);
						exposeString += Environment.NewLine;
						exposeString = exposeString.Substring(indentLevel); //Get rid again of tab symbols at the beginning

						for (int i = 0; i < indentLevel; i++)
						{
							exposeString += '\t';
						}

						if (UserConfig.JumpToGeneratedCode)
						{
							documentView.TextView.Caret.MoveTo(new SnapshotPoint(snapshot, endBraceIndex));
							documentView.TextView.ViewScroller.EnsureSpanVisible(new SnapshotSpan(snapshot, endBraceIndex, 1));
						}

						edit.Insert(endBraceIndex, exposeString);
						edit.Apply();
					}
					else
					{
						throw new Exception("Couldn't find a matching closing bracket for the registration function. Your code cannot be malformatted for the AutoDataExposer to work reliably.");
					}
				}
				else
				{
					throw new Exception("Unable to find Register function in this file. You need a parameterless void function that contain the word 'register' in its name somewhere.");
				}
			}
			catch (Exception e)
			{
				await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"Exception while trying to generate code",
					e);
			}
		}

		private void FillFunctionNames(string functionSignatureLine, string registerFunctionNamespace, string selectedFunctionNamespace, CAutoDataExposerEntry entry, ExposedFunctionInfo info)
		{
			Match returnValueRegex = Regex.Match(functionSignatureLine, entry.FunctionNameRegex);
			if (!returnValueRegex.Success)
			{
				throw new ArgumentException($"Failed to parse function name: {functionSignatureLine}");
			}

			if (!string.IsNullOrWhiteSpace(selectedFunctionNamespace) && !registerFunctionNamespace.StartsWith(selectedFunctionNamespace))
			{
				info.FullyQualifiedFunctionName = selectedFunctionNamespace + "::" + returnValueRegex.Groups[1].Value + returnValueRegex.Groups[2].Value;
			}
			else
			{
				info.FullyQualifiedFunctionName = returnValueRegex.Groups[1].Value + returnValueRegex.Groups[2].Value;
			}

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
			var defaultValue = MainConfig.DefaultValues?.Where(val => val.TypeName == arg.Type).FirstOrDefault();
			if (defaultValue != null)
			{
				return defaultValue.DefaultValue;
			}

			return entry.DefaultValueFormat.Replace("##TYPE##", arg.Type);
		}

		private void OnAutoDataExposerConfigLoaded(object sender, EventArgs args)
		{
			OleMenuCommandService mcs = Package.GetService<IMenuCommandService, OleMenuCommandService>();
			foreach (var menuCommand in MenuCommands)
			{
				mcs.RemoveCommand(menuCommand);
			}

			Command.Enabled = false;
			Command.Text = "No config loaded";

			MenuCommands.Clear();
		}

		protected override void BeforeQueryStatus(EventArgs e)
		{
			if (!SubscribedToDataExposer)
			{
				GameCodersToolkitPackage.AutoDataExposerConfig.OnPreConfigLoad += OnAutoDataExposerConfigLoaded;
				SubscribedToDataExposer = true;
			}

			var entries = MainConfig.AutoDataExposerEntries;

			if (entries.Count == 0)
			{
				Command.Enabled = false;
				Command.Text = "No config loaded";
				return;
			}

			DetermineVisibility(Command, new EventArgs());

			if (MenuCommands.Count > 0)
			{
				return;
			}

			OleMenuCommandService mcs = Package.GetService<IMenuCommandService, OleMenuCommandService>();

			SetupCommand(Command, entries.First());

			for (int i = 1; i < entries.Count; i++)
			{
				Configuration.CAutoDataExposerEntry entry = MainConfig.AutoDataExposerEntries[i];

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

				CAutoDataExposerEntry entry = MainConfig.FindExposerEntryByName(entryName);
				if (entry != null)
				{
					command.Enabled = entry != null && ThreadHelper.JoinableTaskFactory.Run(() => IsLineMatchingRegexAsync(entry.LineValidityRegex));
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

			if (command != Command)
				MenuCommands.Add(command);
		}

		private static async Task<bool> IsLineMatchingRegexAsync(string pattern)
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