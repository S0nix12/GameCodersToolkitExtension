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

				MessageBox box = new MessageBox();
				box.Show(entry.Name);
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

				command.Enabled = entry != null ? ThreadHelper.JoinableTaskFactory.Run(() => IsLineMatchingRegex(entry.Regex)) : false;
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
			DocumentView documentView = await VS.Documents.GetActiveDocumentViewAsync();
			if (documentView == null || documentView.TextView == null)
				return false;

			SnapshotPoint caretSnapshotPoint = documentView.TextView.Caret.Position.BufferPosition;
			ITextSnapshotLine caretLineSnapshot = caretSnapshotPoint.GetContainingLine();

			return Regex.IsMatch(caretLineSnapshot.GetText().Trim(), pattern);
		}
	}
}
