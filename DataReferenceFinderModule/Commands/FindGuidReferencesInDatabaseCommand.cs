using GameCodersToolkit.DataReferenceFinderModule.ReferenceDatabase;
using GameCodersToolkit.ReferenceFinder;
using GameCodersToolkit.ReferenceFinder.ToolWindows;
using GameCodersToolkit.Utils;
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

			string searchText = await TextUtilFunctions.SearchForGuidUnderCaretAsync();

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
				await DiagnosticUtils.ReportExceptionFromExtensionAsync(
					"Expection finding Guid in Database",
					ex);
			}
		}
		protected override void BeforeQueryStatus(EventArgs e)
		{
			Command.Enabled = ThreadHelper.JoinableTaskFactory.Run(TextUtilFunctions.HasPotentialGuidUnderCaretAsync);
		}
	}
}
