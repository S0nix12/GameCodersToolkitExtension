namespace DataReferenceFinder.Commands
{
	[Command(PackageGuids.ReferenceResultsToolbarCommandSet_GuidString, PackageIds.ClearAllResults)]
	internal sealed class ClearFindResultsCommand : BaseCommand<ClearFindResultsCommand>
	{
		protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
			DataReferenceFinderPackage.FindReferenceResultsStorage.Results.Clear();
		}
	}
}
