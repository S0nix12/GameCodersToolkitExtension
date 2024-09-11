using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.ReferenceFinder;
using GameCodersToolkit.ReferenceFinder.ToolWindows;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace GameCodersToolkit.DataReferenceFinderModule
{
	[Command(PackageGuids.DataReferenceFinderCommandSet_GuidString, PackageIds.FindGuidReferencesInDatabase)]
	internal sealed class FindGuidReferencesInDatabaseCommand : BaseCommand<FindGuidReferencesInDatabaseCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			var textWriter = await GameCodersToolkitPackage.ExtensionOutput.CreateOutputPaneTextWriterAsync();

			string searchText = await TextUtilFunctions.FindGuidUnderCaretAsync();

			if (string.IsNullOrEmpty(searchText))
			{
				await GameCodersToolkitPackage.ExtensionOutput.ActivateAsync();
				await textWriter.WriteLineAsync("No Guid selection found");
				return;
			}

			try
			{
				await ReferenceDatabaseUtils.ExecuteFindOperationOnDatabaseAsync(new GenericDataIdentifier(Guid.Parse(searchText)), searchText);
			}
			catch (Exception ex)
			{
				await GameCodersToolkitPackage.ExtensionOutput.ActivateAsync();
				await textWriter.WriteLineAsync("Exeception occured:");
				await textWriter.WriteLineAsync(ex.Message);
				await textWriter.WriteLineAsync(ex.StackTrace);
				await textWriter.WriteLineAsync(ex.ToString());
			}
		}
		protected override void BeforeQueryStatus(EventArgs e)
		{
			string guidText = ThreadHelper.JoinableTaskFactory.Run(TextUtilFunctions.FindGuidUnderCaretAsync);
			Command.Enabled = !string.IsNullOrEmpty(guidText);
		}
	}
}
