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

namespace GameCodersToolkit.AutoDataExposerModule
{
    [Command(PackageGuids.AutoDataExposerSet_GuidString, PackageIds.ExposeToDataCommand)]
    internal sealed class ImplementRegistrationCodeCommand : BaseCommand<ImplementRegistrationCodeCommand>
    {
        const string c_propertyDictionaryIdentifier = "dataExposerEntry";
        private List<OleMenuCommand> menuCommands = new List<OleMenuCommand>();

        protected override void Execute(object sender, EventArgs e)
        {
            if (sender is OleMenuCommand command)
            {
                CAutoDataExposerEntry entry = command.Properties[c_propertyDictionaryIdentifier] as CAutoDataExposerEntry;

                string newContent = ThreadHelper.JoinableTaskFactory.Run(() => GenerateExposeCodeAsync(entry));
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

        List<(string Type, string Name)> ExtractParameters(string functionSignature)
        {
            Match match = Regex.Match(functionSignature, @"\((.*?)\)$");

            if (!match.Success)
            {
                throw new ArgumentException($"Failed to parse function signature: {functionSignature}");
            }

            string parametersText = match.Groups[1].Value.Trim();
            List<(string Type, string Name)> parameters = new List<(string, string)>();

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

                string type = string.Join(" ", parts, 0, parts.Length - 1);
                string name = parts[parts.Length - 1];

                parameters.Add((type, name));
            }

            return parameters;
        }

        static string GetLineAtIndex(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Index is out of range.");
            }

            int lineStart = text.LastIndexOfAny(new[] { '\r', '\n' }, index) + 1; // Start of the line
            int lineEnd = text.IndexOfAny(new[] { '\r', '\n' }, index); // End of the line

            if (lineEnd == -1)
            {
                return text.Substring(lineStart); // No newline found, return till end
            }

            return text.Substring(lineStart, lineEnd - lineStart);
        }

        private async Task<string> GenerateExposeCodeAsync(CAutoDataExposerEntry entry)
        {
            try
            {
                DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
                if (documentView == null || documentView.TextView == null)
                    return "";

                ITextSnapshot snapshot = documentView.TextView.TextBuffer.CurrentSnapshot;
                string documentText = snapshot.GetText();

                Regex regex = new Regex(entry.TargetFunctionRegex, RegexOptions.Multiline);
                Match match = regex.Match(documentText);
                if (match.Success)
                {
                    int index = FindMatchingBrace(documentText, match.Index);
                    if (index != -1)
                    {
                        string exposedFunctionLine = GetLineAtIndex(documentText, match.Index);

                        await VS.MessageBox.ShowAsync(exposedFunctionLine);
                    }
                }

                return "";
            }
            catch (Exception e)
            {
                await VS.MessageBox.ShowAsync(e.Message);
                return "";
            }
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
                CAutoDataExposerEntry entry = command.Properties[c_propertyDictionaryIdentifier] as CAutoDataExposerEntry;

                command.Enabled = entry != null ? ThreadHelper.JoinableTaskFactory.Run(() => IsLineMatchingRegex(entry.LineValidityRegex)) : false;
            }
        }

        private void SetupCommand(OleMenuCommand command, CAutoDataExposerEntry entry)
        {
            command.Text = entry.Name;
            command.Visible = true;
            command.Enabled = true;
            command.Properties[c_propertyDictionaryIdentifier] = entry;
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